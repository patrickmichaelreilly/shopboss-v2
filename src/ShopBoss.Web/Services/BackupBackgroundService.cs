using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class BackupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackupBackgroundService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public BackupBackgroundService(IServiceProvider serviceProvider, ILogger<BackupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackupBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndScheduleBackup();
                
                // Wait for 5 minutes before checking again
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Service is being stopped
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BackupBackgroundService");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("BackupBackgroundService stopped");
    }

    private async Task CheckAndScheduleBackup()
    {
        if (!await _semaphore.WaitAsync(1000))
        {
            return; // Skip if already processing
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<BackupService>();
            
            var config = await backupService.GetBackupConfigurationAsync();
            
            if (!config.EnableAutomaticBackups)
            {
                return;
            }

            var lastBackup = await GetLastSuccessfulBackup(backupService);
            var nextBackupTime = lastBackup?.AddMinutes(config.BackupIntervalMinutes) ?? DateTime.UtcNow.AddMinutes(-1);

            if (DateTime.UtcNow >= nextBackupTime)
            {
                _logger.LogInformation("Starting automatic backup");
                var backupResult = await backupService.CreateBackupAsync(BackupType.Automatic);
                
                if (backupResult.IsSuccessful)
                {
                    _logger.LogInformation("Automatic backup completed successfully: {FilePath}", backupResult.FilePath);
                }
                else
                {
                    _logger.LogError("Automatic backup failed: {ErrorMessage}", backupResult.ErrorMessage);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<DateTime?> GetLastSuccessfulBackup(BackupService backupService)
    {
        var recentBackups = await backupService.GetRecentBackupsAsync(1);
        return recentBackups.FirstOrDefault(b => b.IsSuccessful)?.CreatedDate;
    }

    public override void Dispose()
    {
        _semaphore.Dispose();
        base.Dispose();
    }
}