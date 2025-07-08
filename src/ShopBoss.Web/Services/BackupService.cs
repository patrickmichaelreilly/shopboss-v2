using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using System.Data;

namespace ShopBoss.Web.Services;

public class BackupService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<BackupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AuditTrailService _auditTrailService;

    public BackupService(
        ShopBossDbContext context, 
        ILogger<BackupService> logger, 
        IConfiguration configuration,
        AuditTrailService auditTrailService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _auditTrailService = auditTrailService;
    }

    public async Task<BackupConfiguration> GetBackupConfigurationAsync()
    {
        var config = await _context.BackupConfigurations.FirstOrDefaultAsync();
        if (config == null)
        {
            // Create default configuration
            config = new BackupConfiguration();
            _context.BackupConfigurations.Add(config);
            await _context.SaveChangesAsync();
        }
        return config;
    }

    public async Task<bool> UpdateBackupConfigurationAsync(BackupConfiguration configuration)
    {
        try
        {
            var existingConfig = await _context.BackupConfigurations.FirstOrDefaultAsync();
            if (existingConfig == null)
            {
                _context.BackupConfigurations.Add(configuration);
            }
            else
            {
                existingConfig.BackupIntervalMinutes = configuration.BackupIntervalMinutes;
                existingConfig.MaxBackupRetention = configuration.MaxBackupRetention;
                existingConfig.EnableCompression = configuration.EnableCompression;
                existingConfig.BackupDirectoryPath = configuration.BackupDirectoryPath;
                existingConfig.EnableAutomaticBackups = configuration.EnableAutomaticBackups;
                existingConfig.LastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await _auditTrailService.LogAsync("BackupConfiguration", "Updated", 
                $"Backup configuration updated: Interval={configuration.BackupIntervalMinutes}min, Retention={configuration.MaxBackupRetention}, Compression={configuration.EnableCompression}");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating backup configuration");
            return false;
        }
    }

    public async Task<BackupStatus> CreateBackupAsync(BackupType backupType = BackupType.Manual)
    {
        var backupStatus = new BackupStatus
        {
            BackupType = backupType,
            CreatedDate = DateTime.UtcNow
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var config = await GetBackupConfigurationAsync();
            
            // Ensure backup directory exists
            var backupDirectory = Path.IsPathRooted(config.BackupDirectoryPath) 
                ? config.BackupDirectoryPath 
                : Path.Combine(Directory.GetCurrentDirectory(), config.BackupDirectoryPath);
            
            Directory.CreateDirectory(backupDirectory);

            // Get database file path
            var connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=shopboss.db;Cache=Shared;Foreign Keys=False";
            var dbPath = ExtractDbPathFromConnectionString(connectionString);
            
            if (!File.Exists(dbPath))
            {
                throw new FileNotFoundException($"Database file not found at: {dbPath}");
            }

            // Create backup filename with timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"shopboss_backup_{timestamp}.db";
            var backupFilePath = Path.Combine(backupDirectory, backupFileName);

            // Get original file size
            var originalFileInfo = new FileInfo(dbPath);
            backupStatus.OriginalSize = originalFileInfo.Length;

            // Create backup using SQLite backup API
            if (config.EnableCompression)
            {
                backupFilePath += ".gz";
                await CreateCompressedSqliteBackupAsync(dbPath, backupFilePath);
            }
            else
            {
                await CreateSqliteBackupAsync(dbPath, backupFilePath);
            }

            // Get backup file size
            var backupFileInfo = new FileInfo(backupFilePath);
            backupStatus.BackupSize = backupFileInfo.Length;
            backupStatus.FilePath = backupFilePath;
            backupStatus.IsSuccessful = true;

            _logger.LogInformation("Backup created successfully: {BackupFilePath} (Type: {BackupType})", 
                backupFilePath, backupType);

            await _auditTrailService.LogAsync("Backup", "Created", 
                $"{backupType} backup created: {backupFileName} ({FormatFileSize(backupStatus.BackupSize)})");

            // Clean up old backups
            await CleanupOldBackupsAsync(config, backupDirectory);
        }
        catch (Exception ex)
        {
            backupStatus.IsSuccessful = false;
            backupStatus.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error creating backup");
            
            await _auditTrailService.LogAsync("Backup", "Failed", 
                $"{backupType} backup failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            backupStatus.Duration = stopwatch.Elapsed;
        }

        // Save backup status to database
        _context.BackupStatuses.Add(backupStatus);
        await _context.SaveChangesAsync();

        return backupStatus;
    }

    public async Task<List<BackupStatus>> GetRecentBackupsAsync(int count = 10)
    {
        return await _context.BackupStatuses
            .OrderByDescending(b => b.CreatedDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<bool> DeleteBackupAsync(int backupId)
    {
        try
        {
            var backup = await _context.BackupStatuses.FindAsync(backupId);
            if (backup == null)
            {
                return false;
            }

            // Delete physical file if it exists
            if (File.Exists(backup.FilePath))
            {
                File.Delete(backup.FilePath);
                _logger.LogInformation("Deleted backup file: {FilePath}", backup.FilePath);
            }

            // Remove from database
            _context.BackupStatuses.Remove(backup);
            await _context.SaveChangesAsync();

            await _auditTrailService.LogAsync("Backup", "Deleted", 
                $"Backup deleted: {Path.GetFileName(backup.FilePath)}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup {BackupId}", backupId);
            return false;
        }
    }

    public async Task<bool> RestoreBackupAsync(int backupId)
    {
        try
        {
            var backup = await _context.BackupStatuses.FindAsync(backupId);
            if (backup == null || !backup.IsSuccessful)
            {
                return false;
            }

            if (!File.Exists(backup.FilePath))
            {
                _logger.LogWarning("Backup file not found: {FilePath}", backup.FilePath);
                return false;
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=shopboss.db;Cache=Shared;Foreign Keys=False";
            var dbPath = ExtractDbPathFromConnectionString(connectionString);

            // Close all database connections
            await _context.Database.CloseConnectionAsync();

            // Restore from backup using SQLite-aware methods
            if (backup.FilePath.EndsWith(".gz"))
            {
                await RestoreCompressedSqliteBackupAsync(backup.FilePath, dbPath);
            }
            else
            {
                await RestoreSqliteBackupAsync(backup.FilePath, dbPath);
            }

            _logger.LogInformation("Database restored from backup: {BackupFilePath}", backup.FilePath);
            await _auditTrailService.LogAsync("Backup", "Restored", 
                $"Database restored from backup: {Path.GetFileName(backup.FilePath)}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup {BackupId}", backupId);
            return false;
        }
    }

    private async Task CreateSqliteBackupAsync(string sourcePath, string destinationPath)
    {
        // Use the existing EF Core connection for the source
        var sourceConnection = (SqliteConnection)_context.Database.GetDbConnection();
        
        // Ensure source connection is open
        if (sourceConnection.State != ConnectionState.Open)
        {
            await sourceConnection.OpenAsync();
        }

        // Use VACUUM INTO which is specifically designed for backing up SQLite databases
        // This works even with active connections and WAL files
        var command = sourceConnection.CreateCommand();
        command.CommandText = $"VACUUM INTO '{destinationPath.Replace("'", "''")}'";
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateCompressedSqliteBackupAsync(string sourcePath, string destinationPath)
    {
        // Create temporary uncompressed backup first
        var tempBackupPath = Path.GetTempFileName();
        
        try
        {
            // Create SQLite backup to temporary file
            await CreateSqliteBackupAsync(sourcePath, tempBackupPath);

            // Ensure the temp file is fully written and closed before compression
            await Task.Delay(100);

            // Compress the temporary backup to final destination
            using (var sourceStream = File.OpenRead(tempBackupPath))
            using (var destinationStream = File.Create(destinationPath))
            using (var compressionStream = new GZipStream(destinationStream, CompressionMode.Compress))
            {
                await sourceStream.CopyToAsync(compressionStream);
            }
        }
        finally
        {
            // Clean up temporary file with retry logic
            await DeleteTempFileWithRetry(tempBackupPath);
        }
    }

    private async Task RestoreSqliteBackupAsync(string sourcePath, string destinationPath)
    {
        var sourceConnectionString = $"Data Source={sourcePath};";
        var destinationConnectionString = $"Data Source={destinationPath};";

        // For restore, we need to close EF Core connection and use direct file access
        await _context.Database.CloseConnectionAsync();
        
        // Wait a moment for connection to fully close
        await Task.Delay(100);

        using var sourceConnection = new SqliteConnection(sourceConnectionString);
        using var destinationConnection = new SqliteConnection(destinationConnectionString);

        await sourceConnection.OpenAsync();
        await destinationConnection.OpenAsync();

        // Use SQLite backup API to restore
        sourceConnection.BackupDatabase(destinationConnection);
    }

    private async Task RestoreCompressedSqliteBackupAsync(string sourcePath, string destinationPath)
    {
        // Create temporary uncompressed backup first
        var tempBackupPath = Path.GetTempFileName();
        
        try
        {
            // Decompress to temporary file
            using (var sourceStream = File.OpenRead(sourcePath))
            using (var decompressionStream = new GZipStream(sourceStream, CompressionMode.Decompress))
            using (var destinationStream = File.Create(tempBackupPath))
            {
                await decompressionStream.CopyToAsync(destinationStream);
            }

            // Ensure the temp file is fully written and closed before restore
            await Task.Delay(100);

            // Restore SQLite backup from temporary file
            await RestoreSqliteBackupAsync(tempBackupPath, destinationPath);
        }
        finally
        {
            // Clean up temporary file with retry logic
            await DeleteTempFileWithRetry(tempBackupPath);
        }
    }

    private async Task CleanupOldBackupsAsync(BackupConfiguration config, string backupDirectory)
    {
        try
        {
            var allBackups = await _context.BackupStatuses
                .Where(b => b.IsSuccessful)
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();

            var backupsToDelete = allBackups.Skip(config.MaxBackupRetention).ToList();

            foreach (var backup in backupsToDelete)
            {
                if (File.Exists(backup.FilePath))
                {
                    File.Delete(backup.FilePath);
                    _logger.LogInformation("Deleted old backup file: {FilePath}", backup.FilePath);
                }

                _context.BackupStatuses.Remove(backup);
            }

            if (backupsToDelete.Any())
            {
                await _context.SaveChangesAsync();
                await _auditTrailService.LogAsync("Backup", "Cleanup", 
                    $"Cleaned up {backupsToDelete.Count} old backup files");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old backups");
        }
    }

    private string ExtractDbPathFromConnectionString(string connectionString)
    {
        // Parse SQLite connection string to extract database path
        var parts = connectionString.Split(';');
        var dataSourcePart = parts.FirstOrDefault(p => p.Trim().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));
        
        if (dataSourcePart != null)
        {
            var dbPath = dataSourcePart.Substring("Data Source=".Length).Trim();
            return Path.IsPathRooted(dbPath) ? dbPath : Path.Combine(Directory.GetCurrentDirectory(), dbPath);
        }
        
        return Path.Combine(Directory.GetCurrentDirectory(), "shopboss.db");
    }

    private async Task DeleteTempFileWithRetry(string filePath)
    {
        const int maxRetries = 3;
        const int delayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return;
                }
            }
            catch (IOException ex) when (i < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Failed to delete temporary file {FilePath}, retrying in {DelayMs}ms", filePath, delayMs);
                await Task.Delay(delayMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting temporary file {FilePath}", filePath);
                break;
            }
        }
    }

    private string FormatFileSize(long bytes)
    {
        const int scale = 1024;
        string[] orders = { "GB", "MB", "KB", "Bytes" };
        long max = (long)Math.Pow(scale, orders.Length - 1);

        foreach (string order in orders)
        {
            if (bytes > max)
                return $"{decimal.Divide(bytes, max):##.##} {order}";

            max /= scale;
        }
        return "0 Bytes";
    }
}