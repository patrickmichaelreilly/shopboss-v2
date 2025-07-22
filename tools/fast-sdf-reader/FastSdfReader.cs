using System.Data;
using System.Runtime.InteropServices;
using System.Text.Json;

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
        await ReadSdfDirectlyAsync(data);
        return data;
    }

    private async Task ReadSdfDirectlyAsync(SdfData data)
    {        
        // Use OLE DB to connect to SQL CE
        using var connection = new SqlCeConnectionWrapper(_sdfPath);
        await connection.OpenAsync();

        
        // Read each table with column names matching ColumnMappingService expectations
        // Use LinkID (not ID) for primary keys to match ColumnMappingService mappings
        data.Products = await ReadTableWithColumnsAsync(connection, "Products", 
            "LinkID, Name, ItemNumber, Quantity, WorkOrderName");
        data.Parts = await ReadTableWithColumnsAsync(connection, "Parts", 
            "LinkID, Name, Width, Length, Thickness, MaterialName, LinkIDProduct, LinkIDSubAssembly");
        data.PlacedSheets = await ReadTableWithColumnsAsync(connection, "PlacedSheets", 
            "LinkID, FileName, Name, LinkIDMaterial, Length, Width, Thickness");
        data.Hardware = await ReadTableWithColumnsAsync(connection, "Hardware", 
            "LinkID, Name, Quantity, LinkIDProduct, LinkIDSubAssembly");
        data.Subassemblies = await ReadTableWithColumnsAsync(connection, "Subassemblies", 
            "LinkID, Name, Quantity, LinkIDParentProduct, LinkIDParentSubassembly");
        data.OptimizationResults = await ReadTableWithColumnsAsync(connection, "OptimizationResults", 
            "LinkIDPart, LinkIDSheet");
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
            
            // Silent operation for JSON output
        }
        catch
        {
            // Silent failure
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
            
            // Silent operation for JSON output
            
            // Show column information
            if (results.Count > 0)
            {
                // Silent operation for JSON output
            }
        }
        catch
        {
            // Silent failure
        }

        return results;
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