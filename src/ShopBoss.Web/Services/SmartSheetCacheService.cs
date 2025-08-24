using System.Data;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace ShopBoss.Web.Services;

public class SmartSheetCacheService
{
    private readonly SmartSheetService _smartSheetService;
    private readonly ILogger<SmartSheetCacheService> _logger;
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SmartSheetCacheService(
        SmartSheetService smartSheetService,
        ILogger<SmartSheetCacheService> logger,
        IWebHostEnvironment environment)
    {
        _smartSheetService = smartSheetService;
        _logger = logger;
        
        // Store cache database in data directory (should be gitignored)
        var dataDirectory = Path.Combine(environment.ContentRootPath, "data");
        Directory.CreateDirectory(dataDirectory);
        
        _databasePath = Path.Combine(dataDirectory, "smartsheet-cache.db");
        _connectionString = $"Data Source={_databasePath}";
        
        InitializeDatabase();
    }

    /// <summary>
    /// Initialize the cache database and create tables if they don't exist
    /// </summary>
    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var createTablesCommand = connection.CreateCommand();
        createTablesCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS Workspaces (
                WorkspaceId INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                LastSynced DATETIME NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Sheets (
                SheetId INTEGER PRIMARY KEY,
                WorkspaceId INTEGER,
                Name TEXT NOT NULL,
                ColumnCount INTEGER,
                RowCount INTEGER,
                LastModified DATETIME,
                LastSynced DATETIME NOT NULL,
                FOREIGN KEY (WorkspaceId) REFERENCES Workspaces(WorkspaceId)
            );

            CREATE TABLE IF NOT EXISTS SheetColumns (
                SheetId INTEGER,
                ColumnId INTEGER,
                Title TEXT,
                ColumnType TEXT,
                ColumnIndex INTEGER,
                Options TEXT, -- JSON
                PRIMARY KEY (SheetId, ColumnId),
                FOREIGN KEY (SheetId) REFERENCES Sheets(SheetId)
            );

            CREATE TABLE IF NOT EXISTS SheetRows (
                SheetId INTEGER,
                RowId INTEGER,
                RowNumber INTEGER,
                CellData TEXT, -- JSON blob of all cell data
                CreatedAt DATETIME,
                ModifiedAt DATETIME,
                PRIMARY KEY (SheetId, RowId),
                FOREIGN KEY (SheetId) REFERENCES Sheets(SheetId)
            );

            -- Create indexes for better query performance
            CREATE INDEX IF NOT EXISTS idx_sheets_workspace ON Sheets(WorkspaceId);
            CREATE INDEX IF NOT EXISTS idx_sheetrows_sheet ON SheetRows(SheetId);
            CREATE INDEX IF NOT EXISTS idx_sheetcolumns_sheet ON SheetColumns(SheetId);
        ";

        createTablesCommand.ExecuteNonQuery();
        _logger.LogInformation("SmartSheet cache database initialized at {DatabasePath}", _databasePath);
    }

    /// <summary>
    /// Cache a specific sheet's data to the database
    /// </summary>
    public async Task<bool> CacheSheetAsync(long sheetId)
    {
        try
        {
            _logger.LogInformation("Starting to cache sheet {SheetId}", sheetId);

            // Get the full sheet data from SmartSheet API
            var sheetDataResponse = await _smartSheetService.GetSheetDataAsync(sheetId);
            if (sheetDataResponse == null)
            {
                _logger.LogWarning("No data returned for sheet {SheetId}", sheetId);
                return false;
            }

            // Parse the response (it's an anonymous object)
            var sheetDataJson = JsonConvert.SerializeObject(sheetDataResponse);
            var sheetData = JsonConvert.DeserializeObject<dynamic>(sheetDataJson);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Insert/Update sheet metadata
                var insertSheetCommand = connection.CreateCommand();
                insertSheetCommand.CommandText = @"
                    INSERT OR REPLACE INTO Sheets 
                    (SheetId, WorkspaceId, Name, ColumnCount, RowCount, LastModified, LastSynced)
                    VALUES (@sheetId, @workspaceId, @name, @columnCount, @rowCount, @lastModified, @lastSynced)
                ";
                
                insertSheetCommand.Parameters.AddWithValue("@sheetId", (long)sheetData.id);
                insertSheetCommand.Parameters.AddWithValue("@workspaceId", DBNull.Value); // We'll update this when caching workspace
                insertSheetCommand.Parameters.AddWithValue("@name", (string)sheetData.name);
                insertSheetCommand.Parameters.AddWithValue("@columnCount", ((IEnumerable<dynamic>)sheetData.columns).Count());
                insertSheetCommand.Parameters.AddWithValue("@rowCount", (int)sheetData.totalRowCount);
                insertSheetCommand.Parameters.AddWithValue("@lastModified", DateTime.Parse((string)sheetData.modifiedAt));
                insertSheetCommand.Parameters.AddWithValue("@lastSynced", DateTime.UtcNow);
                
                insertSheetCommand.ExecuteNonQuery();

                // Clear existing column and row data for this sheet
                var clearColumnsCommand = connection.CreateCommand();
                clearColumnsCommand.CommandText = "DELETE FROM SheetColumns WHERE SheetId = @sheetId";
                clearColumnsCommand.Parameters.AddWithValue("@sheetId", sheetId);
                clearColumnsCommand.ExecuteNonQuery();

                var clearRowsCommand = connection.CreateCommand();
                clearRowsCommand.CommandText = "DELETE FROM SheetRows WHERE SheetId = @sheetId";
                clearRowsCommand.Parameters.AddWithValue("@sheetId", sheetId);
                clearRowsCommand.ExecuteNonQuery();

                // Insert column data
                var insertColumnCommand = connection.CreateCommand();
                insertColumnCommand.CommandText = @"
                    INSERT INTO SheetColumns 
                    (SheetId, ColumnId, Title, ColumnType, ColumnIndex, Options)
                    VALUES (@sheetId, @columnId, @title, @columnType, @columnIndex, @options)
                ";

                foreach (var column in sheetData.columns)
                {
                    insertColumnCommand.Parameters.Clear();
                    insertColumnCommand.Parameters.AddWithValue("@sheetId", sheetId);
                    insertColumnCommand.Parameters.AddWithValue("@columnId", (long)column.id);
                    insertColumnCommand.Parameters.AddWithValue("@title", (string)column.title);
                    insertColumnCommand.Parameters.AddWithValue("@columnType", (string)column.type);
                    insertColumnCommand.Parameters.AddWithValue("@columnIndex", (int)column.index);
                    insertColumnCommand.Parameters.AddWithValue("@options", 
                        column.options != null ? JsonConvert.SerializeObject(column.options) : DBNull.Value);
                    
                    insertColumnCommand.ExecuteNonQuery();
                }

                // Insert row data
                var insertRowCommand = connection.CreateCommand();
                insertRowCommand.CommandText = @"
                    INSERT INTO SheetRows 
                    (SheetId, RowId, RowNumber, CellData, CreatedAt, ModifiedAt)
                    VALUES (@sheetId, @rowId, @rowNumber, @cellData, @createdAt, @modifiedAt)
                ";

                foreach (var row in sheetData.rows)
                {
                    insertRowCommand.Parameters.Clear();
                    insertRowCommand.Parameters.AddWithValue("@sheetId", sheetId);
                    insertRowCommand.Parameters.AddWithValue("@rowId", (long)row.id);
                    insertRowCommand.Parameters.AddWithValue("@rowNumber", (int)row.rowNumber);
                    insertRowCommand.Parameters.AddWithValue("@cellData", JsonConvert.SerializeObject(row.cells));
                    insertRowCommand.Parameters.AddWithValue("@createdAt", 
                        row.createdAt != null ? DateTime.Parse((string)row.createdAt) : DBNull.Value);
                    insertRowCommand.Parameters.AddWithValue("@modifiedAt", 
                        row.modifiedAt != null ? DateTime.Parse((string)row.modifiedAt) : DBNull.Value);
                    
                    insertRowCommand.ExecuteNonQuery();
                }

                transaction.Commit();
                
                var sheetName = (string)sheetData.name;
                var rowCount = ((IEnumerable<dynamic>)sheetData.rows).Count();
                _logger.LogInformation("Successfully cached sheet {SheetId} ({SheetName}) with {RowCount} rows", 
                    sheetId, sheetName, rowCount);
                
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching sheet {SheetId}", sheetId);
            return false;
        }
    }

    /// <summary>
    /// Cache all sheets in a workspace
    /// </summary>
    public async Task<CacheWorkspaceResult> CacheWorkspaceAsync(long workspaceId)
    {
        try
        {
            _logger.LogInformation("Starting to cache workspace {WorkspaceId}", workspaceId);

            // Get workspace details
            var workspace = await _smartSheetService.GetWorkspaceByIdAsync(workspaceId);
            if (workspace == null)
            {
                return new CacheWorkspaceResult { Success = false, Message = "Workspace not found" };
            }

            var workspaceData = JsonConvert.DeserializeObject<dynamic>(JsonConvert.SerializeObject(workspace));
            var sheets = workspaceData.sheets;

            var result = new CacheWorkspaceResult
            {
                Success = true,
                WorkspaceName = workspaceData.name,
                TotalSheets = ((IEnumerable<dynamic>)sheets).Count(),
                CachedSheets = new List<string>(),
                FailedSheets = new List<string>()
            };

            // Update workspace metadata
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var insertWorkspaceCommand = connection.CreateCommand();
                insertWorkspaceCommand.CommandText = @"
                    INSERT OR REPLACE INTO Workspaces (WorkspaceId, Name, LastSynced)
                    VALUES (@workspaceId, @name, @lastSynced)
                ";
                insertWorkspaceCommand.Parameters.AddWithValue("@workspaceId", workspaceId);
                insertWorkspaceCommand.Parameters.AddWithValue("@name", (string)workspaceData.name);
                insertWorkspaceCommand.Parameters.AddWithValue("@lastSynced", DateTime.UtcNow);
                insertWorkspaceCommand.ExecuteNonQuery();
            }

            // Cache each sheet
            foreach (var sheet in sheets)
            {
                try
                {
                    var sheetId = (long)sheet.id;
                    var sheetName = (string)sheet.name;
                    
                    _logger.LogInformation("Caching sheet: {SheetName} ({SheetId})", sheetName, sheetId);
                    
                    var success = await CacheSheetAsync(sheetId);
                    if (success)
                    {
                        result.CachedSheets.Add(sheetName);
                    }
                    else
                    {
                        result.FailedSheets.Add(sheetName);
                    }
                    
                    // Add a small delay to avoid hitting API rate limits
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    var sheetName = (string)sheet.name;
                    _logger.LogError(ex, "Failed to cache sheet {SheetName}", sheetName);
                    result.FailedSheets.Add(sheetName);
                }
            }

            _logger.LogInformation("Workspace caching completed. Cached: {CachedCount}, Failed: {FailedCount}", 
                result.CachedSheets.Count, result.FailedSheets.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching workspace {WorkspaceId}", workspaceId);
            return new CacheWorkspaceResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Execute a SQL query against the cache database
    /// </summary>
    public List<Dictionary<string, object>> ExecuteQuery(string sql)
    {
        try
        {
            var results = new List<Dictionary<string, object>>();
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var fieldName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[fieldName] = value;
                }
                results.Add(row);
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {SQL}", sql);
            throw;
        }
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStatsResult GetCacheStats()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var stats = new CacheStatsResult();

            // Get workspace count
            var workspaceCommand = connection.CreateCommand();
            workspaceCommand.CommandText = "SELECT COUNT(*) FROM Workspaces";
            stats.WorkspaceCount = Convert.ToInt32(workspaceCommand.ExecuteScalar());

            // Get sheet count
            var sheetCommand = connection.CreateCommand();
            sheetCommand.CommandText = "SELECT COUNT(*) FROM Sheets";
            stats.SheetCount = Convert.ToInt32(sheetCommand.ExecuteScalar());

            // Get row count
            var rowCommand = connection.CreateCommand();
            rowCommand.CommandText = "SELECT COUNT(*) FROM SheetRows";
            stats.RowCount = Convert.ToInt32(rowCommand.ExecuteScalar());

            // Get database file size
            if (File.Exists(_databasePath))
            {
                var fileInfo = new FileInfo(_databasePath);
                stats.DatabaseSizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2);
            }

            stats.DatabasePath = _databasePath;
            
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache stats");
            return new CacheStatsResult { Error = ex.Message };
        }
    }

    /// <summary>
    /// Clear all cached data
    /// </summary>
    public bool ClearCache()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var clearCommand = connection.CreateCommand();
            clearCommand.CommandText = @"
                DELETE FROM SheetRows;
                DELETE FROM SheetColumns;
                DELETE FROM Sheets;
                DELETE FROM Workspaces;
                VACUUM;
            ";
            clearCommand.ExecuteNonQuery();

            _logger.LogInformation("Cache cleared successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return false;
        }
    }
}

public class CacheWorkspaceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string WorkspaceName { get; set; } = string.Empty;
    public int TotalSheets { get; set; }
    public List<string> CachedSheets { get; set; } = new();
    public List<string> FailedSheets { get; set; } = new();
}

public class CacheStatsResult
{
    public int WorkspaceCount { get; set; }
    public int SheetCount { get; set; }
    public int RowCount { get; set; }
    public double DatabaseSizeMB { get; set; }
    public string DatabasePath { get; set; } = string.Empty;
    public string? Error { get; set; }
}