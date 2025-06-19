namespace ShopBoss.Web.Services;

public class ColumnMappingService
{
    private readonly ILogger<ColumnMappingService> _logger;

    public ColumnMappingService(ILogger<ColumnMappingService> logger)
    {
        _logger = logger;
    }

    public string GetActualColumnName(string tableType, string logicalColumnName)
    {
        var mapping = GetColumnMappings(tableType);
        
        if (mapping.TryGetValue(logicalColumnName, out var actualColumnName))
        {
            return actualColumnName;
        }

        _logger.LogWarning("No mapping found for logical column '{LogicalColumn}' in table type '{TableType}'", 
            logicalColumnName, tableType);
        
        return logicalColumnName;
    }

    public bool HasColumn(string tableType, string logicalColumnName)
    {
        var mapping = GetColumnMappings(tableType);
        return mapping.ContainsKey(logicalColumnName);
    }

    public string GetStringValue(Dictionary<string, object?> data, string tableType, string logicalColumnName)
    {
        var actualColumnName = GetActualColumnName(tableType, logicalColumnName);
        
        if (data.TryGetValue(actualColumnName, out var value) && value != null)
        {
            return value.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    public int GetIntValue(Dictionary<string, object?> data, string tableType, string logicalColumnName)
    {
        var actualColumnName = GetActualColumnName(tableType, logicalColumnName);
        
        if (data.TryGetValue(actualColumnName, out var value) && value != null)
        {
            if (int.TryParse(value.ToString(), out var intValue))
                return intValue;
        }
        return 1; // Default quantity
    }

    public decimal GetDecimalValue(Dictionary<string, object?> data, string tableType, string logicalColumnName)
    {
        var actualColumnName = GetActualColumnName(tableType, logicalColumnName);
        
        if (data.TryGetValue(actualColumnName, out var value) && value != null)
        {
            if (decimal.TryParse(value.ToString(), out var decimalValue))
                return decimalValue;
        }
        return 0;
    }

    private Dictionary<string, string> GetColumnMappings(string tableType)
    {
        return tableType.ToUpperInvariant() switch
        {
            "PRODUCTS" => new Dictionary<string, string>
            {
                // Primary identification
                { "Id", "LinkID" },
                { "ProductId", "LinkID" },
                { "WorkOrderId", "LinkIDWorkOrder" },
                
                // Product properties
                { "Name", "ItemNumber" },
                { "ProductName", "ItemNumber" },
                { "ItemNumber", "ItemNumber" },
                { "WorkOrderName", "WorkOrderName" },
                
                // Dimensions (in millimeters)
                { "Width", "Width" },
                { "Height", "Height" },
                { "Depth", "Depth" },
                { "Length", "Height" }, // Height often represents length in SDF
                
                // Quantities
                { "Quantity", "Quantity" },
                
                // Internal ID
                { "InternalId", "ID" }
            },

            "SUBASSEMBLIES" => new Dictionary<string, string>
            {
                // Primary identification
                { "Id", "LinkID" },
                { "SubassemblyId", "LinkID" },
                
                // Parent relationships
                { "ProductId", "LinkIDParentProduct" },
                { "ParentProductId", "LinkIDParentProduct" },
                { "ParentSubassemblyId", "LinkIDParentSubassembly" },
                { "WorkOrderId", "LinkIDWorkOrder" },
                
                // Subassembly properties
                { "Name", "Name" },
                { "SubassemblyName", "Name" },
                
                // Internal ID
                { "InternalId", "ID" }
            },

            "PARTS" => new Dictionary<string, string>
            {
                // Primary identification
                { "Id", "LinkID" },
                { "PartId", "LinkID" },
                
                // Parent relationships
                { "ProductId", "LinkIDProduct" },
                { "SubassemblyId", "LinkIDSubAssembly" },
                { "WorkOrderId", "LinkIDWorkOrder" },
                { "MaterialId", "LinkIDMaterial" },
                
                // Part properties
                { "Name", "Name" },
                { "PartName", "Name" },
                { "Material", "MaterialName" },
                { "MaterialName", "MaterialName" },
                { "Thickness", "MaterialThickness" },
                { "MaterialThickness", "MaterialThickness" },
                
                // Dimensions (in millimeters)
                { "Width", "Width" },
                { "Length", "Length" },
                { "Height", "Length" }, // Parts typically use Length as height
                
                // Cut dimensions
                { "CutPartWidth", "CutPartWidth" },
                { "CutPartLength", "CutPartLength" },
                { "AdjustedCutPartWidth", "AdjustedCutPartWidth" },
                { "AdjustedCutPartLength", "AdjustedCutPartLength" },
                
                // Edge banding
                { "EdgeBandingTop", "EdgeNameTop" },
                { "EdgeBandingBottom", "EdgeNameBottom" },
                { "EdgeBandingLeft", "EdgeNameLeft" },
                { "EdgeBandingRight", "EdgeNameRight" },
                { "EdgeNameTop", "EdgeNameTop" },
                { "EdgeNameBottom", "EdgeNameBottom" },
                { "EdgeNameLeft", "EdgeNameLeft" },
                { "EdgeNameRight", "EdgeNameRight" },
                
                // Files and face information
                { "FileName", "FileName" },
                { "Face6FileName", "Face6FileName" },
                
                // Quantities and indexing
                { "Quantity", "Quantity" },
                { "Index", "Index" },
                { "RowId", "Row_ID" },
                
                // Internal ID
                { "InternalId", "ID" }
            },

            "HARDWARE" => new Dictionary<string, string>
            {
                // Primary identification
                { "Id", "LinkID" },
                { "HardwareId", "LinkID" },
                
                // Parent relationships
                { "ProductId", "LinkIDProduct" },
                { "WorkOrderId", "LinkIDWorkOrder" },
                { "MaterialId", "LinkIDMaterial" },
                
                // Hardware properties
                { "Name", "Name" },
                { "HardwareName", "Name" },
                
                // Quantities and indexing
                { "Quantity", "Quantity" },
                { "Index", "Index" },
                
                // Internal ID
                { "InternalId", "ID" }
            },

            "PLACEDSHEETS" => new Dictionary<string, string>
            {
                // Primary identification
                { "Id", "LinkID" },
                { "SheetId", "LinkID" },
                
                // Parent relationships
                { "WorkOrderId", "LinkIDWorkOrder" },
                
                // Sheet properties
                { "Name", "Name" },
                { "FileName", "FileName" },
                { "BarCode", "BarCode" },
                
                // Dimensions
                { "Width", "Width" },
                { "Length", "Length" },
                { "Thickness", "Thickness" },
                
                // Quantities and indexing
                { "Quantity", "Quantity" },
                { "Index", "Index" },
                
                // Internal ID
                { "InternalId", "ID" }
            },

            "OPTIMIZATIONRESULTS" => new Dictionary<string, string>
            {
                // Primary identification
                { "Id", "LinkID" },
                
                // Parent relationships
                { "PartId", "LinkIDPart" },
                { "SheetId", "LinkIDSheet" },
                { "WorkOrderId", "LinkIDWorkOrder" },
                
                // Optimization properties
                { "OptimizedQuantity", "OptimizedQuantity" },
                { "Face5FileName", "Face5FileName" },
                { "Face6FileName", "Face6FileName" },
                
                // Dimensions
                { "Width", "Width" },
                { "Length", "Length" },
                
                // Indexing
                { "Index", "Index" },
                
                // Internal ID
                { "InternalId", "ID" }
            },

            _ => new Dictionary<string, string>()
        };
    }
}