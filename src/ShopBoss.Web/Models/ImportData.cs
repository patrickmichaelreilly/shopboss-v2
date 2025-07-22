using System.Collections.Generic;

namespace ShopBoss.Web.Models;

/// <summary>
/// Data structure for JSON output from FastSdfReader.exe
/// Represents the raw data extracted from SDF files before transformation to WorkOrder entities
/// </summary>
public class ImportData
{
    public List<Dictionary<string, object?>> Products { get; set; } = new();
    public List<Dictionary<string, object?>> Parts { get; set; } = new();
    public List<Dictionary<string, object?>> PlacedSheets { get; set; } = new();
    public List<Dictionary<string, object?>> Hardware { get; set; } = new();
    public List<Dictionary<string, object?>> Subassemblies { get; set; } = new();
    public List<Dictionary<string, object?>> OptimizationResults { get; set; } = new();
    
    /// <summary>
    /// Alias for PlacedSheets to maintain compatibility with WorkOrderImportService
    /// PlacedSheets from SDF become NestSheets in the WorkOrder domain
    /// </summary>
    public List<Dictionary<string, object?>> NestSheets => PlacedSheets;
}