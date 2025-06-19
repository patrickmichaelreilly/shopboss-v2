using ShopBoss.Web.Models.Import;
using System.Text.Json;

namespace ShopBoss.Web.Services;

public class ImportDataTransformService
{
    private readonly ILogger<ImportDataTransformService> _logger;
    private readonly ColumnMappingService _columnMapping;

    public ImportDataTransformService(ILogger<ImportDataTransformService> logger, ColumnMappingService columnMapping)
    {
        _logger = logger;
        _columnMapping = columnMapping;
    }

    public ImportWorkOrder TransformToImportWorkOrder(ImportData rawData, string workOrderName)
    {
        var workOrder = new ImportWorkOrder
        {
            Id = Guid.NewGuid().ToString(),
            Name = workOrderName,
            ImportedDate = DateTime.Now
        };

        try
        {
            _logger.LogInformation("Starting data transformation. Products: {ProductCount}, Parts: {PartCount}, Subassemblies: {SubassemblyCount}, Hardware: {HardwareCount}", 
                rawData.Products?.Count ?? 0, rawData.Parts?.Count ?? 0, rawData.Subassemblies?.Count ?? 0, rawData.Hardware?.Count ?? 0);

            // Transform Products - handle potential duplicate keys and null collections
            var productLookup = (rawData.Products ?? new List<Dictionary<string, object?>>())
                .Where(p => !string.IsNullOrEmpty(_columnMapping.GetStringValue(p, "PRODUCTS", "Id")))
                .GroupBy(p => _columnMapping.GetStringValue(p, "PRODUCTS", "Id"))
                .ToDictionary(g => g.Key, g => g.First());
            
            var partLookup = (rawData.Parts ?? new List<Dictionary<string, object?>>())
                .Where(p => !string.IsNullOrEmpty(_columnMapping.GetStringValue(p, "PARTS", "Id")))
                .GroupBy(p => _columnMapping.GetStringValue(p, "PARTS", "Id"))
                .ToDictionary(g => g.Key, g => g.First());
            
            var subassemblyLookup = (rawData.Subassemblies ?? new List<Dictionary<string, object?>>())
                .Where(s => !string.IsNullOrEmpty(_columnMapping.GetStringValue(s, "SUBASSEMBLIES", "Id")))
                .GroupBy(s => _columnMapping.GetStringValue(s, "SUBASSEMBLIES", "Id"))
                .ToDictionary(g => g.Key, g => g.First());
            
            var hardwareLookup = (rawData.Hardware ?? new List<Dictionary<string, object?>>())
                .Where(h => !string.IsNullOrEmpty(_columnMapping.GetStringValue(h, "HARDWARE", "Id")))
                .GroupBy(h => _columnMapping.GetStringValue(h, "HARDWARE", "Id"))
                .ToDictionary(g => g.Key, g => g.First());

            // Transform products
            foreach (var productData in rawData.Products ?? new List<Dictionary<string, object?>>())
            {
                var product = TransformProduct(productData, workOrder.Id);
                
                // Add parts to product
                var productParts = (rawData.Parts ?? new List<Dictionary<string, object?>>())
                    .Where(p => _columnMapping.GetStringValue(p, "PARTS", "ProductId") == product.Id).ToList();
                foreach (var partData in productParts)
                {
                    if (string.IsNullOrEmpty(_columnMapping.GetStringValue(partData, "PARTS", "SubassemblyId")))
                    {
                        product.Parts.Add(TransformPart(partData, product.Id, null));
                    }
                }

                // Add subassemblies to product
                var productSubassemblies = (rawData.Subassemblies ?? new List<Dictionary<string, object?>>())
                    .Where(s => _columnMapping.GetStringValue(s, "SUBASSEMBLIES", "ProductId") == product.Id && 
                               string.IsNullOrEmpty(_columnMapping.GetStringValue(s, "SUBASSEMBLIES", "ParentSubassemblyId")) &&
                               !string.IsNullOrEmpty(_columnMapping.GetStringValue(s, "SUBASSEMBLIES", "Id")))
                    .ToList();
                
                foreach (var subassemblyData in productSubassemblies)
                {
                    product.Subassemblies.Add(TransformSubassembly(subassemblyData, product.Id, null, rawData));
                }

                // Add hardware to product
                var productHardware = (rawData.Hardware ?? new List<Dictionary<string, object?>>())
                    .Where(h => _columnMapping.GetStringValue(h, "HARDWARE", "ProductId") == product.Id).ToList();
                foreach (var hardwareData in productHardware)
                {
                    product.Hardware.Add(TransformHardware(hardwareData, workOrder.Id, product.Id, null));
                }

                workOrder.Products.Add(product);
            }

            // Transform standalone hardware
            var standaloneHardware = (rawData.Hardware ?? new List<Dictionary<string, object?>>())
                .Where(h => string.IsNullOrEmpty(_columnMapping.GetStringValue(h, "HARDWARE", "ProductId")))
                .ToList();
            
            foreach (var hardwareData in standaloneHardware)
            {
                workOrder.Hardware.Add(TransformHardware(hardwareData, workOrder.Id, null, null));
            }

            // Calculate statistics
            workOrder.Statistics = CalculateStatistics(workOrder);

            _logger.LogInformation("Successfully transformed import data for work order {WorkOrderName}", workOrderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transforming import data for work order {WorkOrderName}", workOrderName);
            workOrder.Statistics.Warnings.Add($"Error during transformation: {ex.Message}");
        }

        return workOrder;
    }

    private ImportProduct TransformProduct(Dictionary<string, object?> productData, string workOrderId)
    {
        return new ImportProduct
        {
            Id = _columnMapping.GetStringValue(productData, "PRODUCTS", "Id"),
            Name = _columnMapping.GetStringValue(productData, "PRODUCTS", "Name"),
            Description = _columnMapping.GetStringValue(productData, "PRODUCTS", "Description"),
            Quantity = _columnMapping.GetIntValue(productData, "PRODUCTS", "Quantity"),
            Width = _columnMapping.GetDecimalValue(productData, "PRODUCTS", "Width"),
            Height = _columnMapping.GetDecimalValue(productData, "PRODUCTS", "Height"),
            Depth = _columnMapping.GetDecimalValue(productData, "PRODUCTS", "Depth"),
            Material = _columnMapping.GetStringValue(productData, "PRODUCTS", "Material"),
            WorkOrderId = workOrderId
        };
    }

    private ImportPart TransformPart(Dictionary<string, object?> partData, string productId, string? subassemblyId)
    {
        return new ImportPart
        {
            Id = _columnMapping.GetStringValue(partData, "PARTS", "Id"),
            Name = _columnMapping.GetStringValue(partData, "PARTS", "Name"),
            Description = _columnMapping.GetStringValue(partData, "PARTS", "Description"),
            Quantity = _columnMapping.GetIntValue(partData, "PARTS", "Quantity"),
            Width = _columnMapping.GetDecimalValue(partData, "PARTS", "Width"),
            Height = _columnMapping.GetDecimalValue(partData, "PARTS", "Height"),
            Thickness = _columnMapping.GetDecimalValue(partData, "PARTS", "Thickness"),
            Material = _columnMapping.GetStringValue(partData, "PARTS", "Material"),
            EdgeBanding = _columnMapping.GetStringValue(partData, "PARTS", "EdgeBandingTop") + "|" + 
                         _columnMapping.GetStringValue(partData, "PARTS", "EdgeBandingBottom") + "|" +
                         _columnMapping.GetStringValue(partData, "PARTS", "EdgeBandingLeft") + "|" +
                         _columnMapping.GetStringValue(partData, "PARTS", "EdgeBandingRight"),
            ProductId = productId,
            SubassemblyId = subassemblyId,
            GrainDirection = _columnMapping.GetStringValue(partData, "PARTS", "GrainDirection"),
            Notes = _columnMapping.GetStringValue(partData, "PARTS", "Notes")
        };
    }

    private ImportSubassembly TransformSubassembly(Dictionary<string, object?> subassemblyData, string productId, string? parentSubassemblyId, ImportData rawData, HashSet<string>? processedSubassemblies = null)
    {
        // Initialize circular reference tracking
        processedSubassemblies ??= new HashSet<string>();
        
        var subassemblyId = _columnMapping.GetStringValue(subassemblyData, "SUBASSEMBLIES", "Id");
        
        // Skip subassemblies with empty/null IDs to avoid false circular reference detection
        if (string.IsNullOrEmpty(subassemblyId))
        {
            _logger.LogWarning("Skipping subassembly with empty/null ID in product {ProductId}", productId);
            // Generate a unique ID for display purposes
            subassemblyId = $"unknown-subassembly-{Guid.NewGuid().ToString()[..8]}";
        }
        
        var subassembly = new ImportSubassembly
        {
            Id = subassemblyId,
            Name = _columnMapping.GetStringValue(subassemblyData, "SUBASSEMBLIES", "Name"),
            Description = _columnMapping.GetStringValue(subassemblyData, "SUBASSEMBLIES", "Description"),
            Quantity = _columnMapping.GetIntValue(subassemblyData, "SUBASSEMBLIES", "Quantity"),
            Width = _columnMapping.GetDecimalValue(subassemblyData, "SUBASSEMBLIES", "Width"),
            Height = _columnMapping.GetDecimalValue(subassemblyData, "SUBASSEMBLIES", "Height"),
            Depth = _columnMapping.GetDecimalValue(subassemblyData, "SUBASSEMBLIES", "Depth"),
            ProductId = productId,
            ParentSubassemblyId = parentSubassemblyId
        };

        // Check for circular reference (only for valid IDs)
        if (processedSubassemblies.Contains(subassembly.Id))
        {
            _logger.LogWarning("Circular reference detected for subassembly {SubassemblyId}. Skipping nested processing.", subassembly.Id);
            return subassembly;
        }

        // Add to processed set
        processedSubassemblies.Add(subassembly.Id);

        // Add parts to subassembly
        var subassemblyParts = (rawData.Parts ?? new List<Dictionary<string, object?>>())
            .Where(p => _columnMapping.GetStringValue(p, "PARTS", "SubassemblyId") == subassembly.Id).ToList();
        foreach (var partData in subassemblyParts)
        {
            subassembly.Parts.Add(TransformPart(partData, productId, subassembly.Id));
        }

        // Add nested subassemblies (max 2 levels)
        if (string.IsNullOrEmpty(parentSubassemblyId)) // Only add nested if we're not already nested
        {
            var nestedSubassemblies = (rawData.Subassemblies ?? new List<Dictionary<string, object?>>())
                .Where(s => _columnMapping.GetStringValue(s, "SUBASSEMBLIES", "ParentSubassemblyId") == subassembly.Id &&
                           !string.IsNullOrEmpty(_columnMapping.GetStringValue(s, "SUBASSEMBLIES", "Id")))
                .ToList();
            
            foreach (var nestedData in nestedSubassemblies)
            {
                subassembly.NestedSubassemblies.Add(TransformSubassembly(nestedData, productId, subassembly.Id, rawData, processedSubassemblies));
            }
        }

        // Add hardware to subassembly - NOTE: Hardware table doesn't have SubassemblyId according to SDF analysis
        // We'll skip this for now as it may not be needed based on actual SDF structure

        // Remove from processed set to allow processing in different branches
        processedSubassemblies.Remove(subassembly.Id);

        return subassembly;
    }

    private ImportHardware TransformHardware(Dictionary<string, object?> hardwareData, string workOrderId, string? productId, string? subassemblyId)
    {
        return new ImportHardware
        {
            Id = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Id"),
            Name = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Name"),
            Description = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Description"),
            Quantity = _columnMapping.GetIntValue(hardwareData, "HARDWARE", "Quantity"),
            Category = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Category"),
            Manufacturer = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Manufacturer"),
            PartNumber = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "PartNumber"),
            WorkOrderId = workOrderId,
            ProductId = productId,
            SubassemblyId = subassemblyId,
            Size = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Size"),
            Finish = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Finish"),
            Notes = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Notes")
        };
    }

    private ImportStatistics CalculateStatistics(ImportWorkOrder workOrder)
    {
        var stats = new ImportStatistics
        {
            TotalProducts = workOrder.Products.Count,
            TotalHardware = workOrder.Hardware.Count + 
                           workOrder.Products.Sum(p => p.Hardware.Count + 
                                                     p.Subassemblies.Sum(s => s.Hardware.Count + 
                                                                              s.NestedSubassemblies.Sum(ns => ns.Hardware.Count))),
            TotalDetachedProducts = workOrder.DetachedProducts.Count
        };

        foreach (var product in workOrder.Products)
        {
            stats.TotalParts += product.Parts.Count;
            stats.TotalSubassemblies += product.Subassemblies.Count;
            
            foreach (var subassembly in product.Subassemblies)
            {
                stats.TotalParts += subassembly.Parts.Count;
                stats.TotalSubassemblies += subassembly.NestedSubassemblies.Count;
                
                foreach (var nested in subassembly.NestedSubassemblies)
                {
                    stats.TotalParts += nested.Parts.Count;
                }
            }
        }

        return stats;
    }

}