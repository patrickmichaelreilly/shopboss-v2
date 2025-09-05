using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using System.Data;
using System.Text.Json;

namespace ShopBoss.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class MigrationController : ControllerBase
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<MigrationController> _logger;
    private readonly IConfiguration _configuration;
    private static SqliteConnection? _oldDbConnection;
    private static string? _oldDbPath;

    public MigrationController(
        ShopBossDbContext context,
        ILogger<MigrationController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    private bool IsMigrationApiEnabled()
    {
        return _configuration.GetValue<bool>("EnableMigrationApi", false);
    }

    [HttpPost("connect")]
    public async Task<IActionResult> ConnectToOldDatabase([FromBody] ConnectRequest request)
    {
        if (!IsMigrationApiEnabled())
            return Forbid("Migration API is disabled. Set EnableMigrationApi=true in configuration.");

        try
        {
            // Close existing connection if any
            if (_oldDbConnection != null)
            {
                await _oldDbConnection.DisposeAsync();
                _oldDbConnection = null;
            }

            if (!System.IO.File.Exists(request.DatabasePath))
                return BadRequest($"Database file not found: {request.DatabasePath}");

            var connectionString = $"Data Source={request.DatabasePath};Mode=ReadOnly;";
            _oldDbConnection = new SqliteConnection(connectionString);
            await _oldDbConnection.OpenAsync();
            _oldDbPath = request.DatabasePath;

            _logger.LogInformation("Connected to old database: {Path}", request.DatabasePath);
            
            // Get basic info about the database
            var tables = await GetTableListAsync(_oldDbConnection);
            return Ok(new { 
                Success = true, 
                DatabasePath = request.DatabasePath,
                TableCount = tables.Count,
                Tables = tables 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to old database: {Path}", request.DatabasePath);
            return StatusCode(500, $"Failed to connect: {ex.Message}");
        }
    }

    [HttpGet("schema/compare")]
    public async Task<IActionResult> CompareSchemas()
    {
        if (!IsMigrationApiEnabled())
            return Forbid("Migration API is disabled");

        if (_oldDbConnection == null)
            return BadRequest("No old database connected. Use /connect first.");

        try
        {
            var comparison = new Dictionary<string, object>();
            
            // Get tables from both databases
            var oldTables = await GetTableListAsync(_oldDbConnection);
            var newTables = await GetCurrentDbTablesAsync();

            comparison["OldTables"] = oldTables;
            comparison["NewTables"] = newTables;
            comparison["CommonTables"] = oldTables.Intersect(newTables).ToList();
            comparison["MissingInNew"] = oldTables.Except(newTables).ToList();
            comparison["NewTablesOnly"] = newTables.Except(oldTables).ToList();

            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare schemas");
            return StatusCode(500, $"Schema comparison failed: {ex.Message}");
        }
    }

    [HttpGet("schema/{tableName}")]
    public async Task<IActionResult> GetTableSchema(string tableName, [FromQuery] string database = "both")
    {
        if (!IsMigrationApiEnabled())
            return Forbid("Migration API is disabled");

        try
        {
            var result = new Dictionary<string, object>();

            if (database == "old" || database == "both")
            {
                if (_oldDbConnection == null)
                    return BadRequest("No old database connected");

                result["OldSchema"] = await GetTableSchemaAsync(_oldDbConnection, tableName);
            }

            if (database == "new" || database == "both")
            {
                result["NewSchema"] = await GetCurrentTableSchemaAsync(tableName);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schema for table: {TableName}", tableName);
            return StatusCode(500, $"Schema retrieval failed: {ex.Message}");
        }
    }

    [HttpPost("query")]
    public async Task<IActionResult> QueryOldDatabase([FromBody] QueryRequest request)
    {
        if (!IsMigrationApiEnabled())
            return Forbid("Migration API is disabled");

        if (_oldDbConnection == null)
            return BadRequest("No old database connected");

        // Security: Only allow SELECT statements
        var sql = request.Sql.Trim();
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !sql.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only SELECT and PRAGMA statements are allowed");
        }

        try
        {
            using var command = new SqliteCommand(sql, _oldDbConnection);
            using var reader = await command.ExecuteReaderAsync();

            var results = new List<Dictionary<string, object>>();
            var columnNames = new List<string>();

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            // Read data
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[columnNames[i]] = value == DBNull.Value ? null! : value;
                }
                results.Add(row);
            }

            _logger.LogInformation("Executed query: {Sql}, returned {Count} rows", sql, results.Count);

            return Ok(new 
            { 
                Sql = sql,
                Columns = columnNames,
                RowCount = results.Count,
                Data = results.Take(request.Limit ?? 100) // Limit results for safety
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query failed: {Sql}", sql);
            return StatusCode(500, $"Query failed: {ex.Message}");
        }
    }

    [HttpPost("copy")]
    public async Task<IActionResult> CopyTableData([FromBody] CopyRequest request)
    {
        if (!IsMigrationApiEnabled())
            return Forbid("Migration API is disabled");

        if (_oldDbConnection == null)
            return BadRequest("No old database connected");

        try
        {
            var copyResult = await CopyTableDataAsync(request);
            return Ok(copyResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy failed for table: {TableName}", request.TableName);
            return StatusCode(500, $"Copy operation failed: {ex.Message}");
        }
    }

    [HttpPost("copy-custom")]
    public async Task<IActionResult> CopyCustomQuery([FromBody] CustomCopyRequest request)
    {
        if (!IsMigrationApiEnabled())
            return Forbid("Migration API is disabled");

        if (_oldDbConnection == null)
            return BadRequest("No old database connected");

        try
        {
            var copyResult = await CopyCustomQueryAsync(request);
            return Ok(copyResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Custom copy failed for table: {TableName}", request.TableName);
            return StatusCode(500, $"Custom copy operation failed: {ex.Message}");
        }
    }

    [HttpGet("validate")]
    public async Task<IActionResult> ValidateMigration()
    {
        if (!IsMigrationApiEnabled())
            return Forbid("Migration API is disabled");

        if (_oldDbConnection == null)
            return BadRequest("No old database connected");

        try
        {
            var validation = new Dictionary<string, object>();
            var oldTables = await GetTableListAsync(_oldDbConnection);

            foreach (var table in oldTables)
            {
                var oldCount = await GetRowCountAsync(_oldDbConnection, table);
                var newCount = await GetCurrentTableRowCountAsync(table);
                
                validation[table] = new 
                {
                    OldRows = oldCount,
                    NewRows = newCount,
                    Match = oldCount == newCount
                };
            }

            return Ok(validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed");
            return StatusCode(500, $"Validation failed: {ex.Message}");
        }
    }

    private async Task<List<string>> GetTableListAsync(SqliteConnection connection)
    {
        var tables = new List<string>();
        using var command = new SqliteCommand(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name", 
            connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private async Task<List<string>> GetCurrentDbTablesAsync()
    {
        // Get actual tables from current database
        var tables = new List<string>();
        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%' ORDER BY name";
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private async Task<object> GetTableSchemaAsync(SqliteConnection connection, string tableName)
    {
        var schema = new List<Dictionary<string, object>>();
        using var command = new SqliteCommand($"PRAGMA table_info({tableName})", connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            schema.Add(new Dictionary<string, object>
            {
                ["cid"] = reader.GetInt32(0),
                ["name"] = reader.GetString(1),
                ["type"] = reader.GetString(2),
                ["notnull"] = reader.GetBoolean(3),
                ["dflt_value"] = reader.IsDBNull(4) ? null! : reader.GetValue(4),
                ["pk"] = reader.GetBoolean(5)
            });
        }

        return schema;
    }

    private async Task<object> GetCurrentTableSchemaAsync(string tableName)
    {
        var schema = new List<Dictionary<string, object>>();
        using var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            schema.Add(new Dictionary<string, object>
            {
                ["cid"] = reader.GetInt32(0),
                ["name"] = reader.GetString(1),
                ["type"] = reader.GetString(2),
                ["notnull"] = reader.GetBoolean(3),
                ["dflt_value"] = reader.IsDBNull(4) ? null! : reader.GetValue(4),
                ["pk"] = reader.GetBoolean(5)
            });
        }

        return schema;
    }

    private async Task<int> GetRowCountAsync(SqliteConnection connection, string tableName)
    {
        using var command = new SqliteCommand($"SELECT COUNT(*) FROM [{tableName}]", connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task<int> GetCurrentTableRowCountAsync(string tableName)
    {
        try
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM [{tableName}]";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch
        {
            // Table might not exist in new database
            return -1;
        }
    }

    private async Task<object> CopyCustomQueryAsync(CustomCopyRequest request)
    {
        if (_oldDbConnection == null)
            throw new InvalidOperationException("No old database connected");

        var result = new Dictionary<string, object>
        {
            ["TableName"] = request.TableName,
            ["CustomQuery"] = request.SelectQuery,
            ["RowsProcessed"] = 0,
            ["RowsSuccess"] = 0,
            ["RowsFailed"] = 0,
            ["Errors"] = new List<string>()
        };

        try
        {
            // Execute custom query on old database
            using var selectCommand = new SqliteCommand(request.SelectQuery, _oldDbConnection);
            using var reader = await selectCommand.ExecuteReaderAsync();

            // Clear target table if requested
            if (request.ClearTargetFirst)
            {
                using var clearConnection = _context.Database.GetDbConnection();
                await clearConnection.OpenAsync();
                using var clearCommand = clearConnection.CreateCommand();
                clearCommand.CommandText = $"DELETE FROM [{request.TableName}]";
                await clearCommand.ExecuteNonQueryAsync();
                _logger.LogInformation("Cleared target table: {TableName}", request.TableName);
            }

            var rowsProcessed = 0;
            var rowsSuccess = 0;
            var rowsFailed = 0;
            var errors = new List<string>();

            // Get column names from reader
            var columnNames = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            // Copy data row by row
            while (await reader.ReadAsync())
            {
                rowsProcessed++;
                
                try
                {
                    var placeholders = string.Join(", ", columnNames.Select((_, i) => $"@param{i}"));
                    var insertSql = $"INSERT INTO [{request.TableName}] ({string.Join(", ", columnNames.Select(f => $"[{f}]"))}) VALUES ({placeholders})";

                    using var insertConnection = _context.Database.GetDbConnection();
                    await insertConnection.OpenAsync();
                    using var insertCommand = insertConnection.CreateCommand();
                    insertCommand.CommandText = insertSql;

                    // Add parameters for each column
                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        var parameter = insertCommand.CreateParameter();
                        parameter.ParameterName = $"@param{i}";
                        parameter.Value = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                        insertCommand.Parameters.Add(parameter);
                    }

                    await insertCommand.ExecuteNonQueryAsync();
                    rowsSuccess++;
                }
                catch (Exception ex)
                {
                    rowsFailed++;
                    var error = $"Row {rowsProcessed}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning("Failed to copy row {RowNumber}: {Error}", rowsProcessed, ex.Message);
                }

                // Log progress every 100 rows
                if (rowsProcessed % 100 == 0)
                {
                    _logger.LogInformation("Copied {Success}/{Total} rows for {Table}", rowsSuccess, rowsProcessed, request.TableName);
                }
            }

            result["RowsProcessed"] = rowsProcessed;
            result["RowsSuccess"] = rowsSuccess;
            result["RowsFailed"] = rowsFailed;
            result["Errors"] = errors;
            result["Status"] = rowsFailed == 0 ? "Success" : "Partial Success";

            _logger.LogInformation("Custom copy completed for {Table}: {Success}/{Total} rows succeeded", 
                request.TableName, rowsSuccess, rowsProcessed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Custom copy operation failed for table: {TableName}", request.TableName);
            result["Status"] = "Failed";
            result["Error"] = ex.Message;
            return result;
        }
    }

    private async Task<object> CopyTableDataAsync(CopyRequest request)
    {
        if (_oldDbConnection == null)
            throw new InvalidOperationException("No old database connected");

        var result = new Dictionary<string, object>
        {
            ["TableName"] = request.TableName,
            ["FieldMappings"] = request.FieldMappings,
            ["RowsProcessed"] = 0,
            ["RowsSuccess"] = 0,
            ["RowsFailed"] = 0,
            ["Errors"] = new List<string>()
        };

        try
        {
            // Get old table data
            var oldFields = request.FieldMappings.Keys.ToList();
            var selectSql = $"SELECT {string.Join(", ", oldFields.Select(f => $"[{f}]"))} FROM [{request.TableName}]";
            
            using var selectCommand = new SqliteCommand(selectSql, _oldDbConnection);
            using var reader = await selectCommand.ExecuteReaderAsync();

            // Clear target table if requested
            if (request.ClearTargetFirst)
            {
                using var clearConnection = _context.Database.GetDbConnection();
                await clearConnection.OpenAsync();
                using var clearCommand = clearConnection.CreateCommand();
                clearCommand.CommandText = $"DELETE FROM [{request.TableName}]";
                await clearCommand.ExecuteNonQueryAsync();
                _logger.LogInformation("Cleared target table: {TableName}", request.TableName);
            }

            var rowsProcessed = 0;
            var rowsSuccess = 0;
            var rowsFailed = 0;
            var errors = new List<string>();

            // Copy data row by row
            while (await reader.ReadAsync())
            {
                rowsProcessed++;
                
                try
                {
                    var newFields = request.FieldMappings.Values.ToList();
                    var placeholders = string.Join(", ", newFields.Select((_, i) => $"@param{i}"));
                    var insertSql = $"INSERT INTO [{request.TableName}] ({string.Join(", ", newFields.Select(f => $"[{f}]"))}) VALUES ({placeholders})";

                    using var insertConnection = _context.Database.GetDbConnection();
                    await insertConnection.OpenAsync();
                    using var insertCommand = insertConnection.CreateCommand();
                    insertCommand.CommandText = insertSql;

                    // Map field values
                    for (int i = 0; i < oldFields.Count; i++)
                    {
                        var parameter = insertCommand.CreateParameter();
                        parameter.ParameterName = $"@param{i}";
                        parameter.Value = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                        insertCommand.Parameters.Add(parameter);
                    }

                    await insertCommand.ExecuteNonQueryAsync();
                    rowsSuccess++;
                }
                catch (Exception ex)
                {
                    rowsFailed++;
                    var error = $"Row {rowsProcessed}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning("Failed to copy row {RowNumber}: {Error}", rowsProcessed, ex.Message);
                }

                // Log progress every 100 rows
                if (rowsProcessed % 100 == 0)
                {
                    _logger.LogInformation("Copied {Success}/{Total} rows for {Table}", rowsSuccess, rowsProcessed, request.TableName);
                }
            }

            result["RowsProcessed"] = rowsProcessed;
            result["RowsSuccess"] = rowsSuccess;
            result["RowsFailed"] = rowsFailed;
            result["Errors"] = errors;
            result["Status"] = rowsFailed == 0 ? "Success" : "Partial Success";

            _logger.LogInformation("Copy completed for {Table}: {Success}/{Total} rows succeeded", 
                request.TableName, rowsSuccess, rowsProcessed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy operation failed for table: {TableName}", request.TableName);
            result["Status"] = "Failed";
            result["Error"] = ex.Message;
            return result;
        }
    }
}

public class ConnectRequest
{
    public string DatabasePath { get; set; } = string.Empty;
}

public class QueryRequest  
{
    public string Sql { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

public class CopyRequest
{
    public string TableName { get; set; } = string.Empty;
    public Dictionary<string, string> FieldMappings { get; set; } = new();
    public bool ClearTargetFirst { get; set; } = false;
}

public class CustomCopyRequest
{
    public string TableName { get; set; } = string.Empty;
    public string SelectQuery { get; set; } = string.Empty;
    public bool ClearTargetFirst { get; set; } = false;
}