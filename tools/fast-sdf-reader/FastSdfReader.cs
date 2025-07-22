using System.Data;
using System.Runtime.InteropServices;

namespace FastSdfReader;

public class FastSdfReader
{
    private readonly string _sdfPath;

    public FastSdfReader(string sdfPath)
    {
        _sdfPath = sdfPath ?? throw new ArgumentNullException(nameof(sdfPath));
        
        if (!File.Exists(_sdfPath))
        {
            throw new FileNotFoundException($"SDF file not found: {_sdfPath}");
        }
    }

    public async Task<SdfData> ReadAsync()
    {
        var data = new SdfData();
        var startTime = DateTime.Now;

        Console.WriteLine($"FastSdfReader starting: {_sdfPath}");
        Console.WriteLine($"File size: {new FileInfo(_sdfPath).Length / 1024 / 1024:F1} MB");

        // Direct SqlCeConnection access to SDF file
        await ReadSdfDirectlyAsync(data);

        var totalTime = (DateTime.Now - startTime).TotalSeconds;
        Console.WriteLine($"FastSdfReader completed in {totalTime:F2} seconds");
        
        return data;
    }

    private async Task ReadSdfDirectlyAsync(SdfData data)
    {        
        try
        {
            // Use OLE DB to connect to SQL CE
            using var connection = new SqlCeConnectionWrapper(_sdfPath);
            await connection.OpenAsync();

            Console.WriteLine("Connected to SDF file successfully");

            
            // Read each table with ONLY the columns specified in phases.md - skip ALL BLOBs
            data.Products = await ReadTableWithColumnsAsync(connection, "Products", 
                "ID, Name, ItemNumber, Quantity");
            data.Parts = await ReadTableWithColumnsAsync(connection, "Parts", 
                "ID, Name, Width, Length, Thickness, MaterialName, LinkIDProduct, LinkIDSubAssembly");
            data.PlacedSheets = await ReadTableWithColumnsAsync(connection, "PlacedSheets", 
                "ID, FileName, LinkIDMaterial, Length, Width, Thickness");
            data.Hardware = await ReadTableWithColumnsAsync(connection, "Hardware", 
                "ID, Name, Quantity, LinkIDProduct, LinkIDSubAssembly");
            data.Subassemblies = await ReadTableWithColumnsAsync(connection, "Subassemblies", 
                "ID, Name, Quantity, LinkIDParentProduct, LinkIDParentSubassembly");
            data.OptimizationResults = await ReadTableWithColumnsAsync(connection, "OptimizationResults", 
                "LinkIDPart, LinkIDSheet");

            Console.WriteLine("SDF direct reading completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading SDF: {ex.Message}");
        }
    }

    private async Task<List<Dictionary<string, object?>>> ReadTableAsync(
        SqlCeConnectionWrapper connection, 
        string tableName, 
        string columns)
    {
        var results = new List<Dictionary<string, object?>>();
        var startTime = DateTime.Now;

        try
        {
            var query = $"SELECT {columns} FROM [{tableName}]";
            var records = await connection.ExecuteQueryAsync(query);
            
            results.AddRange(records);
            
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"{tableName}: {results.Count} records in {elapsed:F0}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {tableName}: {ex.Message}");
        }

        return results;
    }

    private async Task<List<Dictionary<string, object?>>> ReadTableWithColumnsAsync(
        SqlCeConnectionWrapper connection, 
        string tableName, 
        string columns)
    {
        var results = new List<Dictionary<string, object?>>();
        var startTime = DateTime.Now;

        try
        {
            var query = $"SELECT {columns} FROM [{tableName}]";
            var records = await connection.ExecuteQueryAsync(query);
            
            results.AddRange(records);
            
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"{tableName}: {results.Count} records in {elapsed:F0}ms");
            
            // Show column information
            if (results.Count > 0)
            {
                var columnNames = string.Join(", ", results[0].Keys);
                Console.WriteLine($"  Columns: {columnNames}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {tableName}: {ex.Message}");
        }

        return results;
    }



    public async Task PrintSummaryAsync()
    {
        try
        {
            var data = await ReadAsync();
            
            Console.WriteLine("\n=== SDF READING SUMMARY ===");
            Console.WriteLine($"Products: {data.Products.Count} records");
            Console.WriteLine($"Parts: {data.Parts.Count} records");
            Console.WriteLine($"PlacedSheets: {data.PlacedSheets.Count} records");
            Console.WriteLine($"Hardware: {data.Hardware.Count} records");
            Console.WriteLine($"Subassemblies: {data.Subassemblies.Count} records");
            Console.WriteLine($"OptimizationResults: {data.OptimizationResults.Count} records");
            
            // Show sample data from Products table
            if (data.Products.Count > 0)
            {
                Console.WriteLine("\n=== SAMPLE PRODUCT DATA ===");
                var sample = data.Products.First();
                foreach (var kvp in sample)
                {
                    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
            }
            
            // Show sample data from Parts table
            if (data.Parts.Count > 0)
            {
                Console.WriteLine("\n=== SAMPLE PART DATA ===");
                var sample = data.Parts.First();
                foreach (var kvp in sample)
                {
                    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

public class SdfData
{
    public List<Dictionary<string, object?>> Products { get; set; } = new();
    public List<Dictionary<string, object?>> Parts { get; set; } = new();
    public List<Dictionary<string, object?>> PlacedSheets { get; set; } = new();
    public List<Dictionary<string, object?>> Hardware { get; set; } = new();
    public List<Dictionary<string, object?>> Subassemblies { get; set; } = new();
    public List<Dictionary<string, object?>> OptimizationResults { get; set; } = new();
}