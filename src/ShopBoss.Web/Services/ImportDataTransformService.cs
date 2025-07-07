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
        // Try to extract actual work order name from SDF data
        string actualWorkOrderName = workOrderName;
        if (string.IsNullOrEmpty(workOrderName) || workOrderName == "Imported Work Order")
        {
            var firstProduct = rawData.Products?.FirstOrDefault();
            if (firstProduct != null)
            {
                var extractedName = _columnMapping.GetStringValue(firstProduct, "PRODUCTS", "WorkOrderName");
                if (!string.IsNullOrEmpty(extractedName))
                {
                    actualWorkOrderName = extractedName;
                }
            }
        }

        var workOrder = new ImportWorkOrder
        {
            Id = Guid.NewGuid().ToString(),
            Name = actualWorkOrderName ?? "Imported Work Order",
            ImportedDate = DateTime.Now
        };

        try
        {
            _logger.LogInformation("Starting data transformation. Products: {ProductCount}, Parts: {PartCount}, Subassemblies: {SubassemblyCount}, Hardware: {HardwareCount}, NestSheets: {NestSheetCount}", 
                rawData.Products?.Count ?? 0, rawData.Parts?.Count ?? 0, rawData.Subassemblies?.Count ?? 0, rawData.Hardware?.Count ?? 0, rawData.NestSheets?.Count ?? 0);

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
            
            var nestSheetLookup = (rawData.NestSheets ?? new List<Dictionary<string, object?>>())
                .Where(n => !string.IsNullOrEmpty(_columnMapping.GetStringValue(n, "PLACEDSHEETS", "FileName")))
                .GroupBy(n => _columnMapping.GetStringValue(n, "PLACEDSHEETS", "FileName"))
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

            // Transform nest sheets
            foreach (var nestSheetData in rawData.NestSheets ?? new List<Dictionary<string, object?>>())
            {
                workOrder.NestSheets.Add(TransformNestSheet(nestSheetData, workOrder.Id));
            }

            // Establish nest sheet to part relationships using OptimizationResults
            EstablishNestSheetPartRelationships(rawData, workOrder);

            // Process single-part products as detached products
            ProcessSinglePartProductsAsDetached(workOrder);

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
        var itemNumber = _columnMapping.GetStringValue(productData, "PRODUCTS", "ItemNumber"); // ItemNumber from SDF
        var productName = _columnMapping.GetStringValue(productData, "PRODUCTS", "Name"); // Product Name from SDF
        
        return new ImportProduct
        {
            Id = _columnMapping.GetStringValue(productData, "PRODUCTS", "Id"),
            ProductNumber = itemNumber, // ItemNumber from SDF
            Name = productName, // Product Name from SDF
            Description = string.Empty, // Description not available in PRODUCTS table
            Quantity = _columnMapping.GetIntValue(productData, "PRODUCTS", "Quantity"),
            Width = _columnMapping.GetDecimalValue(productData, "PRODUCTS", "Width"),
            Height = _columnMapping.GetDecimalValue(productData, "PRODUCTS", "Height"),
            Depth = _columnMapping.GetDecimalValue(productData, "PRODUCTS", "Depth"),
            Material = string.Empty, // Material not available in PRODUCTS table  
            WorkOrderId = workOrderId
        };
    }

    private ImportPart TransformPart(Dictionary<string, object?> partData, string productId, string? subassemblyId, Dictionary<string, string>? partToNestSheetMapping = null)
    {
        var partId = _columnMapping.GetStringValue(partData, "PARTS", "Id");
        
        // Get nest sheet information from OptimizationResults mapping if available
        var nestSheetName = string.Empty;
        var nestSheetId = string.Empty;
        
        if (partToNestSheetMapping != null && !string.IsNullOrEmpty(partId) && partToNestSheetMapping.ContainsKey(partId))
        {
            nestSheetId = partToNestSheetMapping[partId];
            // We'll set the name later when we have access to the nest sheet data
        }
        else
        {
            // Fallback: try to get nest sheet name from part data directly
            nestSheetName = _columnMapping.GetStringValue(partData, "PARTS", "SheetName") ?? 
                           _columnMapping.GetStringValue(partData, "PARTS", "NestSheet") ??
                           _columnMapping.GetStringValue(partData, "PARTS", "PlacedSheet") ??
                           string.Empty;
        }

        return new ImportPart
        {
            Id = partId,
            Name = _columnMapping.GetStringValue(partData, "PARTS", "Name"),
            Description = string.Empty, // Description not available in PARTS table
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
            NestSheetName = nestSheetName,
            NestSheetId = nestSheetId,
            GrainDirection = string.Empty, // GrainDirection not available in PARTS table
            Notes = string.Empty // Notes not available in PARTS table
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
            Description = string.Empty, // Description not available in SUBASSEMBLIES table
            Quantity = _columnMapping.GetIntValue(subassemblyData, "SUBASSEMBLIES", "Quantity"), // Extract actual quantity from SDF data
            Width = 0, // Width not available in SUBASSEMBLIES table
            Height = 0, // Height not available in SUBASSEMBLIES table
            Depth = 0, // Depth not available in SUBASSEMBLIES table
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
            Description = string.Empty, // Description not available in HARDWARE table
            Quantity = _columnMapping.GetIntValue(hardwareData, "HARDWARE", "Quantity"),
            Category = string.Empty, // Category not available in HARDWARE table
            Manufacturer = string.Empty, // Manufacturer not available in HARDWARE table
            PartNumber = string.Empty, // PartNumber not available in HARDWARE table
            WorkOrderId = workOrderId,
            ProductId = productId,
            SubassemblyId = subassemblyId,
            Size = string.Empty, // Size not available in HARDWARE table
            Finish = string.Empty, // Finish not available in HARDWARE table
            Notes = string.Empty // Notes not available in HARDWARE table
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
            TotalDetachedProducts = workOrder.DetachedProducts.Count,
            TotalNestSheets = workOrder.NestSheets.Count
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

    private ImportNestSheet TransformNestSheet(Dictionary<string, object?> nestSheetData, string workOrderId)
    {
        var nestSheetName = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "Name"); // Material name
        var nestSheetFileName = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "FileName");
        var nestSheetId = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "Id");
        
        return new ImportNestSheet
        {
            Id = nestSheetId ?? Guid.NewGuid().ToString(), // Use LinkID from SDF or generate new ID
            Name = nestSheetFileName, // Use FileName for the actual nest sheet name
            Material = nestSheetName, // Use Name field for material (e.g., "19 Plywood")
            Length = _columnMapping.GetDecimalValue(nestSheetData, "PLACEDSHEETS", "Length"),
            Width = _columnMapping.GetDecimalValue(nestSheetData, "PLACEDSHEETS", "Width"),
            Thickness = _columnMapping.GetDecimalValue(nestSheetData, "PLACEDSHEETS", "Thickness"),
            Barcode = nestSheetFileName, // Use FileName as barcode for scanning
            Description = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "Description") ?? string.Empty,
            Notes = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "Notes") ?? string.Empty
        };
    }

    private void EstablishNestSheetPartRelationships(ImportData rawData, ImportWorkOrder workOrder)
    {
        try
        {
            _logger.LogInformation("Processing OptimizationResults. Total records: {Count}", 
                rawData.OptimizationResults?.Count ?? 0);

            // Create a mapping of LinkIDPart to LinkIDSheet from OptimizationResults
            var partToSheetMapping = new Dictionary<string, string>();
            
            foreach (var optimizationResult in rawData.OptimizationResults ?? new List<Dictionary<string, object?>>())
            {
                // Debug: Log the keys available in optimization result
                _logger.LogDebug("OptimizationResult keys: {Keys}", string.Join(", ", optimizationResult.Keys));

                var linkIdPart = _columnMapping.GetStringValue(optimizationResult, "OPTIMIZATIONRESULTS", "PartId");
                var linkIdSheet = _columnMapping.GetStringValue(optimizationResult, "OPTIMIZATIONRESULTS", "SheetId");
                
                _logger.LogDebug("LinkIDPart: {Part}, LinkIDSheet: {Sheet}", linkIdPart, linkIdSheet);
                
                if (!string.IsNullOrEmpty(linkIdPart) && !string.IsNullOrEmpty(linkIdSheet))
                {
                    partToSheetMapping[linkIdPart] = linkIdSheet;
                }
            }

            _logger.LogInformation("Created part to sheet mapping with {Count} entries", partToSheetMapping.Count);

            // Create a lookup of nest sheet LinkID to nest sheet name
            var sheetIdToNameMapping = new Dictionary<string, string>();
            foreach (var nestSheet in workOrder.NestSheets)
            {
                if (!string.IsNullOrEmpty(nestSheet.Id))
                {
                    sheetIdToNameMapping[nestSheet.Id] = nestSheet.Name;
                    _logger.LogDebug("Nest sheet mapping: {Id} -> {Name}", nestSheet.Id, nestSheet.Name);
                }
            }

            // Update parts with proper nest sheet information
            UpdatePartsWithNestSheetInfo(workOrder.Products, partToSheetMapping, sheetIdToNameMapping);

            _logger.LogInformation("Established nest sheet relationships for {PartCount} parts using OptimizationResults", 
                partToSheetMapping.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error establishing nest sheet part relationships");
        }
    }

    private void UpdatePartsWithNestSheetInfo(
        List<ImportProduct> products, 
        Dictionary<string, string> partToSheetMapping, 
        Dictionary<string, string> sheetIdToNameMapping)
    {
        foreach (var product in products)
        {
            // Update parts in product
            foreach (var part in product.Parts)
            {
                UpdatePartNestSheetInfo(part, partToSheetMapping, sheetIdToNameMapping);
            }

            // Update parts in subassemblies
            foreach (var subassembly in product.Subassemblies)
            {
                UpdatePartsInSubassembly(subassembly, partToSheetMapping, sheetIdToNameMapping);
            }
        }
    }

    private void UpdatePartsInSubassembly(
        ImportSubassembly subassembly, 
        Dictionary<string, string> partToSheetMapping, 
        Dictionary<string, string> sheetIdToNameMapping)
    {
        foreach (var part in subassembly.Parts)
        {
            UpdatePartNestSheetInfo(part, partToSheetMapping, sheetIdToNameMapping);
        }

        // Handle nested subassemblies
        foreach (var nested in subassembly.NestedSubassemblies)
        {
            UpdatePartsInSubassembly(nested, partToSheetMapping, sheetIdToNameMapping);
        }
    }

    private void UpdatePartNestSheetInfo(
        ImportPart part, 
        Dictionary<string, string> partToSheetMapping, 
        Dictionary<string, string> sheetIdToNameMapping)
    {
        if (partToSheetMapping.ContainsKey(part.Id))
        {
            part.NestSheetId = partToSheetMapping[part.Id];
            
            if (sheetIdToNameMapping.ContainsKey(part.NestSheetId))
            {
                part.NestSheetName = sheetIdToNameMapping[part.NestSheetId];
            }
        }
    }

    private void ProcessSinglePartProductsAsDetached(ImportWorkOrder workOrder)
    {
        // Find products with exactly 1 part and convert them to detached products
        var singlePartProducts = workOrder.Products
            .Where(p => p.Parts.Count == 1 && p.Subassemblies.Count == 0)
            .ToList();
        
        // Create list to track products to remove after conversion
        var productsToRemove = new List<ImportProduct>();
        
        foreach (var product in singlePartProducts)
        {
            var singlePart = product.Parts.First();
            
            // Create a detached product from this single-part product
            var detachedProduct = new ImportDetachedProduct
            {
                Id = $"{product.Id}_detached_{Guid.NewGuid().ToString()[..8]}", // Create unique ID to avoid conflicts
                Name = product.Name,
                Quantity = product.Quantity,
                Height = singlePart.Height,
                Width = singlePart.Width,
                Thickness = singlePart.Thickness,
                Material = singlePart.Material,
                EdgeBanding = singlePart.EdgeBanding,
                WorkOrderId = workOrder.Id,
                // Copy NestSheet information from the original part
                NestSheetName = singlePart.NestSheetName,
                NestSheetId = singlePart.NestSheetId,
                // Store original part ID for barcode scanning compatibility
                OriginalPartId = singlePart.Id
            };
            
            workOrder.DetachedProducts.Add(detachedProduct);
            productsToRemove.Add(product);
        }
        
        // Remove the original single-part products
        foreach (var product in productsToRemove)
        {
            workOrder.Products.Remove(product);
        }
        
        if (singlePartProducts.Any())
        {
            _logger.LogInformation("Identified {Count} single-part products as detached products during transformation", singlePartProducts.Count);
        }
    }

}