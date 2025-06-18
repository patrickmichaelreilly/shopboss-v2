using System.Data;
using System.Data.SQLite;
using System.Text.Json;

namespace SdfImporter;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--self-check")
            {
                JsonStructureTest.RunSelfCheck();
                return 0;
            }
            
            string? outputPath = null;
            string? inputPath = null;
            
            // Parse arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--output" && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                    i++; // Skip next argument as it's the output path
                }
                else if (!args[i].StartsWith("--"))
                {
                    inputPath = args[i];
                }
            }
            
            if (string.IsNullOrEmpty(inputPath))
            {
                Console.Error.WriteLine("Usage: dotnet run -- <path-to-sdf-or-sqlite-file> [--output <output-path>]");
                Console.Error.WriteLine("       dotnet run -- --self-check");
                Console.Error.WriteLine("");
                Console.Error.WriteLine("Output options:");
                Console.Error.WriteLine("  --output file.json    Write JSON to file");
                Console.Error.WriteLine("  --output file.sqlite  Write SQLite database directly");
                Console.Error.WriteLine("  (no --output)         Write JSON to stdout");
                return 1;
            }

            string filePath = inputPath;
            
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                return 1;
            }

            var importer = new SdfImporter();
            
            // Check if output should be SQLite directly
            if (!string.IsNullOrEmpty(outputPath) && outputPath.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqliteResult = await importer.ImportToSqliteAsync(filePath, outputPath);
                var fileSize = new FileInfo(outputPath).Length;
                Console.Error.WriteLine($"SQLite database created: {outputPath} ({fileSize / 1024.0 / 1024.0:F1} MB)");
                Console.Error.WriteLine($"Processed {sqliteResult.TablesProcessed} tables, {sqliteResult.TotalRows:N0} rows in {sqliteResult.TotalTime:F1}s");
                Console.Error.WriteLine("Import completed successfully");
                return 0;
            }
            
            var result = await importer.ImportAsync(filePath);
            
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            // Output JSON
            if (!string.IsNullOrEmpty(outputPath))
            {
                await File.WriteAllTextAsync(outputPath, json);
                var fileSize = new FileInfo(outputPath).Length;
                Console.Error.WriteLine($"JSON written to: {outputPath} ({fileSize / 1024.0 / 1024.0:F1} MB)");
            }
            else
            {
                Console.WriteLine(json);
            }
            
            // Verify the output structure (only show success for file output)
            if (!JsonStructureTest.VerifyJsonStructure(json))
            {
                Console.Error.WriteLine("JSON structure validation failed");
                return 1;
            }
            
            // Only show success message for file output (stdout is the data itself)
            if (!string.IsNullOrEmpty(outputPath))
            {
                Console.Error.WriteLine("Import completed successfully");
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

public class SdfImporter
{
    private readonly SdfToSqliteConverter _converter = new();
    private static readonly string[] RequiredTables = 
    {
        "Products", "Parts", "PlacedSheets", "Hardware", "Subassemblies", "OptimizationResults"
    };
    
    private static readonly string[] BinaryColumnNames = 
    {
        "JPegStream", "TiffStream", "WMFStream", "WorkBook", "Image", "Thumbnail", 
        "BinaryData", "ImageData", "FileData", "StreamData"
    };

    public async Task<ImportResult> ImportAsync(string filePath)
    {
        var sqliteFilePath = await GetSqliteFilePathAsync(filePath);
        var connectionString = $"Data Source={sqliteFilePath};Version=3;";
        
        using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync();
        
        var startTime = DateTime.Now;
        Console.Error.WriteLine("Extracting data from 6 required tables...");
        
        var result = new ImportResult();
        var totalRows = 0;
        
        foreach (var tableName in RequiredTables)
        {
            var tableData = await ReadTableAsync(connection, tableName);
            totalRows += tableData.Count;
            
            // Assign to appropriate property
            switch (tableName)
            {
                case "Products": result.Products = tableData; break;
                case "Parts": result.Parts = tableData; break;
                case "PlacedSheets": result.PlacedSheets = tableData; break;
                case "Hardware": result.Hardware = tableData; break;
                case "Subassemblies": result.Subassemblies = tableData; break;
                case "OptimizationResults": result.OptimizationResults = tableData; break;
            }
        }
        
        var totalTime = (DateTime.Now - startTime).TotalSeconds;
        Console.Error.WriteLine($"Processed 6 tables, {totalRows:N0} total rows in {totalTime:F1}s");
        
        return result;
    }
    
    public async Task<SqliteImportResult> ImportToSqliteAsync(string inputPath, string outputPath)
    {
        var startTime = DateTime.Now;
        var sqliteFilePath = await GetSqliteFilePathAsync(inputPath);
        
        // If we converted from SDF, copy to output path
        if (sqliteFilePath != inputPath)
        {
            File.Copy(sqliteFilePath, outputPath, true);
        }
        else if (inputPath != outputPath)
        {
            File.Copy(inputPath, outputPath, true);
        }
        
        // Count rows in output database
        var connectionString = $"Data Source={outputPath};Version=3;";
        using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync();
        
        var totalRows = 0;
        var tablesProcessed = 0;
        
        foreach (var tableName in RequiredTables)
        {
            try
            {
                using var command = new SQLiteCommand($"SELECT COUNT(*) FROM [{tableName}]", connection);
                var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                totalRows += count;
                tablesProcessed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Could not count rows in {tableName}: {ex.Message}");
            }
        }
        
        var totalTime = (DateTime.Now - startTime).TotalSeconds;
        
        return new SqliteImportResult
        {
            TablesProcessed = tablesProcessed,
            TotalRows = totalRows,
            TotalTime = totalTime
        };
    }
    
    private async Task<string> GetSqliteFilePathAsync(string filePath)
    {
        if (Path.GetExtension(filePath).ToLowerInvariant() == ".sdf")
        {
            return await _converter.ConvertAsync(filePath);
        }
        else
        {
            return filePath;
        }
    }
    
    private async Task<List<Dictionary<string, object?>>> ReadTableAsync(SQLiteConnection connection, string tableName)
    {
        var results = new List<Dictionary<string, object?>>();
        
        try
        {
            // First, get table schema to identify non-binary columns
            var allowedColumns = await GetAllowedColumnsAsync(connection, tableName);
            
            if (allowedColumns.Count == 0)
            {
                Console.Error.WriteLine($"Warning: No allowed columns found in table '{tableName}'");
                return results;
            }
            
            // Use explicit column selection instead of SELECT *
            var columnList = string.Join(", ", allowedColumns.Select(col => $"[{col}]"));
            var sql = $"SELECT {columnList} FROM [{tableName}]";
            
            using var command = new SQLiteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnName] = value;
                }
                results.Add(row);
            }
        }
        catch (SQLiteException ex)
        {
            Console.Error.WriteLine($"Warning: Table '{tableName}' not found or error reading: {ex.Message}");
        }
        
        return results;
    }
    
    private async Task<List<string>> GetAllowedColumnsAsync(SQLiteConnection connection, string tableName)
    {
        var allowedColumns = new List<string>();
        
        try
        {
            using var command = new SQLiteCommand($"PRAGMA table_info([{tableName}])", connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString("name");
                var columnType = reader.GetString("type").ToUpperInvariant();
                
                // Skip binary/blob columns and known binary column names
                if (IsBinaryColumn(columnName, columnType))
                {
                    continue;
                }
                
                allowedColumns.Add(columnName);
            }
        }
        catch (SQLiteException ex)
        {
            Console.Error.WriteLine($"Warning: Could not get schema for table '{tableName}': {ex.Message}");
        }
        
        return allowedColumns;
    }
    
    private bool IsBinaryColumn(string columnName, string columnType)
    {
        // Check by column name
        if (BinaryColumnNames.Any(binaryName => 
            columnName.Contains(binaryName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        
        // Check by column type
        return columnType.Contains("BLOB") || 
               columnType.Contains("BINARY") || 
               columnType.Contains("VARBINARY") ||
               columnType.Contains("IMAGE");
    }
}

public class ImportResult
{
    public List<Dictionary<string, object?>> Products { get; set; } = new();
    public List<Dictionary<string, object?>> Parts { get; set; } = new();
    public List<Dictionary<string, object?>> PlacedSheets { get; set; } = new();
    public List<Dictionary<string, object?>> Hardware { get; set; } = new();
    public List<Dictionary<string, object?>> Subassemblies { get; set; } = new();
    public List<Dictionary<string, object?>> OptimizationResults { get; set; } = new();
}

public class SqliteImportResult
{
    public int TablesProcessed { get; set; }
    public int TotalRows { get; set; }
    public double TotalTime { get; set; }
}