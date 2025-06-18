using ShopBoss.Web.Models.Import;
using System.Text.Json;

namespace ShopBoss.Web.Services;

public class ImportDataTransformService
{
    private readonly ILogger<ImportDataTransformService> _logger;

    public ImportDataTransformService(ILogger<ImportDataTransformService> logger)
    {
        _logger = logger;
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
                .Where(p => !string.IsNullOrEmpty(GetStringValue(p, "ProductId")))
                .GroupBy(p => GetStringValue(p, "ProductId"))
                .ToDictionary(g => g.Key, g => g.First());
            
            var partLookup = (rawData.Parts ?? new List<Dictionary<string, object?>>())
                .Where(p => !string.IsNullOrEmpty(GetStringValue(p, "PartId")))
                .GroupBy(p => GetStringValue(p, "PartId"))
                .ToDictionary(g => g.Key, g => g.First());
            
            var subassemblyLookup = (rawData.Subassemblies ?? new List<Dictionary<string, object?>>())
                .Where(s => !string.IsNullOrEmpty(GetStringValue(s, "SubassemblyId")))
                .GroupBy(s => GetStringValue(s, "SubassemblyId"))
                .ToDictionary(g => g.Key, g => g.First());
            
            var hardwareLookup = (rawData.Hardware ?? new List<Dictionary<string, object?>>())
                .Where(h => !string.IsNullOrEmpty(GetStringValue(h, "HardwareId")))
                .GroupBy(h => GetStringValue(h, "HardwareId"))
                .ToDictionary(g => g.Key, g => g.First());

            // Transform products
            foreach (var productData in rawData.Products ?? new List<Dictionary<string, object?>>())
            {
                var product = TransformProduct(productData, workOrder.Id);
                
                // Add parts to product
                var productParts = (rawData.Parts ?? new List<Dictionary<string, object?>>())
                    .Where(p => GetStringValue(p, "ProductId") == product.Id).ToList();
                foreach (var partData in productParts)
                {
                    if (string.IsNullOrEmpty(GetStringValue(partData, "SubassemblyId")))
                    {
                        product.Parts.Add(TransformPart(partData, product.Id, null));
                    }
                }

                // Add subassemblies to product
                var productSubassemblies = (rawData.Subassemblies ?? new List<Dictionary<string, object?>>())
                    .Where(s => GetStringValue(s, "ProductId") == product.Id && 
                               string.IsNullOrEmpty(GetStringValue(s, "ParentSubassemblyId")) &&
                               !string.IsNullOrEmpty(GetStringValue(s, "SubassemblyId")))
                    .ToList();
                
                foreach (var subassemblyData in productSubassemblies)
                {
                    product.Subassemblies.Add(TransformSubassembly(subassemblyData, product.Id, null, rawData));
                }

                // Add hardware to product
                var productHardware = (rawData.Hardware ?? new List<Dictionary<string, object?>>())
                    .Where(h => GetStringValue(h, "ProductId") == product.Id).ToList();
                foreach (var hardwareData in productHardware)
                {
                    if (string.IsNullOrEmpty(GetStringValue(hardwareData, "SubassemblyId")))
                    {
                        product.Hardware.Add(TransformHardware(hardwareData, workOrder.Id, product.Id, null));
                    }
                }

                workOrder.Products.Add(product);
            }

            // Transform standalone hardware
            var standaloneHardware = (rawData.Hardware ?? new List<Dictionary<string, object?>>())
                .Where(h => string.IsNullOrEmpty(GetStringValue(h, "ProductId")))
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
            Id = GetStringValue(productData, "ProductId"),
            Name = GetStringValue(productData, "Name") ?? GetStringValue(productData, "ProductName"),
            Description = GetStringValue(productData, "Description") ?? string.Empty,
            Quantity = GetIntValue(productData, "Quantity"),
            Width = GetDecimalValue(productData, "Width"),
            Height = GetDecimalValue(productData, "Height"),
            Depth = GetDecimalValue(productData, "Depth"),
            Material = GetStringValue(productData, "Material") ?? string.Empty,
            WorkOrderId = workOrderId
        };
    }

    private ImportPart TransformPart(Dictionary<string, object?> partData, string productId, string? subassemblyId)
    {
        return new ImportPart
        {
            Id = GetStringValue(partData, "PartId"),
            Name = GetStringValue(partData, "Name") ?? GetStringValue(partData, "PartName"),
            Description = GetStringValue(partData, "Description") ?? string.Empty,
            Quantity = GetIntValue(partData, "Quantity"),
            Width = GetDecimalValue(partData, "Width"),
            Height = GetDecimalValue(partData, "Height"),
            Thickness = GetDecimalValue(partData, "Thickness"),
            Material = GetStringValue(partData, "Material") ?? string.Empty,
            EdgeBanding = GetStringValue(partData, "EdgeBanding") ?? string.Empty,
            ProductId = productId,
            SubassemblyId = subassemblyId,
            GrainDirection = GetStringValue(partData, "GrainDirection") ?? string.Empty,
            Notes = GetStringValue(partData, "Notes") ?? string.Empty
        };
    }

    private ImportSubassembly TransformSubassembly(Dictionary<string, object?> subassemblyData, string productId, string? parentSubassemblyId, ImportData rawData, HashSet<string>? processedSubassemblies = null)
    {
        // Initialize circular reference tracking
        processedSubassemblies ??= new HashSet<string>();
        
        var subassemblyId = GetStringValue(subassemblyData, "SubassemblyId");
        
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
            Name = GetStringValue(subassemblyData, "Name") ?? GetStringValue(subassemblyData, "SubassemblyName"),
            Description = GetStringValue(subassemblyData, "Description") ?? string.Empty,
            Quantity = GetIntValue(subassemblyData, "Quantity"),
            Width = GetDecimalValue(subassemblyData, "Width"),
            Height = GetDecimalValue(subassemblyData, "Height"),
            Depth = GetDecimalValue(subassemblyData, "Depth"),
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
            .Where(p => GetStringValue(p, "SubassemblyId") == subassembly.Id).ToList();
        foreach (var partData in subassemblyParts)
        {
            subassembly.Parts.Add(TransformPart(partData, productId, subassembly.Id));
        }

        // Add nested subassemblies (max 2 levels)
        if (string.IsNullOrEmpty(parentSubassemblyId)) // Only add nested if we're not already nested
        {
            var nestedSubassemblies = (rawData.Subassemblies ?? new List<Dictionary<string, object?>>())
                .Where(s => GetStringValue(s, "ParentSubassemblyId") == subassembly.Id &&
                           !string.IsNullOrEmpty(GetStringValue(s, "SubassemblyId")))
                .ToList();
            
            foreach (var nestedData in nestedSubassemblies)
            {
                subassembly.NestedSubassemblies.Add(TransformSubassembly(nestedData, productId, subassembly.Id, rawData, processedSubassemblies));
            }
        }

        // Add hardware to subassembly
        var subassemblyHardware = (rawData.Hardware ?? new List<Dictionary<string, object?>>())
            .Where(h => GetStringValue(h, "SubassemblyId") == subassembly.Id).ToList();
        foreach (var hardwareData in subassemblyHardware)
        {
            subassembly.Hardware.Add(TransformHardware(hardwareData, string.Empty, productId, subassembly.Id));
        }

        // Remove from processed set to allow processing in different branches
        processedSubassemblies.Remove(subassembly.Id);

        return subassembly;
    }

    private ImportHardware TransformHardware(Dictionary<string, object?> hardwareData, string workOrderId, string? productId, string? subassemblyId)
    {
        return new ImportHardware
        {
            Id = GetStringValue(hardwareData, "HardwareId"),
            Name = GetStringValue(hardwareData, "Name") ?? GetStringValue(hardwareData, "HardwareName"),
            Description = GetStringValue(hardwareData, "Description") ?? string.Empty,
            Quantity = GetIntValue(hardwareData, "Quantity"),
            Category = GetStringValue(hardwareData, "Category") ?? string.Empty,
            Manufacturer = GetStringValue(hardwareData, "Manufacturer") ?? string.Empty,
            PartNumber = GetStringValue(hardwareData, "PartNumber") ?? string.Empty,
            WorkOrderId = workOrderId,
            ProductId = productId,
            SubassemblyId = subassemblyId,
            Size = GetStringValue(hardwareData, "Size") ?? string.Empty,
            Finish = GetStringValue(hardwareData, "Finish") ?? string.Empty,
            Notes = GetStringValue(hardwareData, "Notes") ?? string.Empty
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

    private string GetStringValue(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    private int GetIntValue(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value != null)
        {
            if (int.TryParse(value.ToString(), out var intValue))
                return intValue;
        }
        return 1; // Default quantity
    }

    private decimal GetDecimalValue(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value != null)
        {
            if (decimal.TryParse(value.ToString(), out var decimalValue))
                return decimalValue;
        }
        return 0;
    }
}