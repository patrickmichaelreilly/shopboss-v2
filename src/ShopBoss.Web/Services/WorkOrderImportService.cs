using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

/// <summary>
/// Phase I2: Service to transform SDF data directly to WorkOrder entities
/// Bypasses Import entities for architectural unity
/// </summary>
public class WorkOrderImportService
{
    private readonly ILogger<WorkOrderImportService> _logger;
    private readonly ColumnMappingService _columnMapping;
    private readonly PartFilteringService _partFilteringService;

    public WorkOrderImportService(ILogger<WorkOrderImportService> logger, ColumnMappingService columnMapping, PartFilteringService partFilteringService)
    {
        _logger = logger;
        _columnMapping = columnMapping;
        _partFilteringService = partFilteringService;
    }

    /// <summary>
    /// Phase I2: Transform raw SDF data directly to WorkOrder entities
    /// Creates in-memory WorkOrder with proper IDs (no prefixes)
    /// </summary>
    public async Task<WorkOrder> TransformToWorkOrderAsync(ImportData rawData, string workOrderName)
    {
        try
        {
            // Extract actual work order name from SDF data if available
            string actualWorkOrderName = ExtractWorkOrderName(rawData, workOrderName);

            // Create WorkOrder entity with proper ID
            var workOrder = new WorkOrder
            {
                Id = Guid.NewGuid().ToString(),
                Name = actualWorkOrderName,
                ImportedDate = DateTime.Now,
                IsArchived = false,
                Products = new List<Product>(),
                Hardware = new List<Hardware>(),
                DetachedProducts = new List<DetachedProduct>(),
                NestSheets = new List<NestSheet>()
            };

            _logger.LogInformation("Phase I2: Starting WorkOrder transformation. Raw data - Products: {ProductCount}, Parts: {PartCount}, Hardware: {HardwareCount}", 
                rawData.Products?.Count ?? 0, rawData.Parts?.Count ?? 0, rawData.Hardware?.Count ?? 0);

            // Transform data with proper entity relationships
            await TransformProductsAsync(rawData, workOrder);
            TransformDetachedProducts(rawData, workOrder);
            TransformHardware(rawData, workOrder);
            TransformNestSheets(rawData, workOrder);

            // Establish nest sheet relationships using OptimizationResults (same as existing system)
            await EstablishNestSheetRelationshipsAsync(rawData, workOrder);

            // Process single-part products as detached products (must be after all transformations)
            ProcessSinglePartProductsAsDetached(workOrder);

            // Phase I3: Apply auto-categorization to all parts (final step of import processing)
            await ApplyAutoCategorizationAsync(workOrder);

            _logger.LogInformation("Phase I2: WorkOrder transformation completed. Created - Products: {ProductCount}, DetachedProducts: {DetachedProductCount}, Parts: {PartCount}, Hardware: {HardwareCount}", 
                workOrder.Products.Count, 
                workOrder.DetachedProducts.Count,
                workOrder.Products.SelectMany(p => p.Parts).Count() + workOrder.Products.SelectMany(p => p.Subassemblies).SelectMany(s => s.Parts).Count(),
                workOrder.Hardware.Count);

            return workOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase I2: Error transforming raw data to WorkOrder entities");
            throw;
        }
    }

    /// <summary>
    /// Extract work order name from SDF data
    /// </summary>
    private string ExtractWorkOrderName(ImportData rawData, string fallbackName)
    {
        if (!string.IsNullOrEmpty(fallbackName) && fallbackName != "New Import Work Order")
        {
            return fallbackName;
        }

        // Try to extract from first product
        var firstProduct = rawData.Products?.FirstOrDefault();
        if (firstProduct != null)
        {
            var extractedName = _columnMapping.GetStringValue(firstProduct, "PRODUCTS", "WorkOrderName");
            if (!string.IsNullOrEmpty(extractedName))
            {
                return extractedName;
            }
        }

        return fallbackName ?? "New Import Work Order";
    }

    /// <summary>
    /// Transform products with quantity expansion and navigation properties
    /// </summary>
    private async Task TransformProductsAsync(ImportData rawData, WorkOrder workOrder)
    {
        if (rawData.Products == null) return;

        foreach (var productData in rawData.Products)
        {
            var baseProduct = CreateProductFromData(productData, workOrder.Id);
            
            // Phase I2: Handle quantity expansion - create multiple Product instances
            var quantity = _columnMapping.GetIntValue(productData, "PRODUCTS", "Quantity");
            
            if (quantity == 1)
            {
                // Single quantity: use baseProduct directly, no cloning needed
                baseProduct.Qty = 1;
                await PopulateProductChildrenAsync(rawData, baseProduct, baseProduct.Id, null);
                workOrder.Products.Add(baseProduct);
            }
            else
            {
                // Multiple quantity: clone for each instance
                _logger.LogInformation("Converting product '{ProductName}' (ID: {ProductId}) with quantity {Quantity} to {Quantity} individual product instances",
                    baseProduct.Name, baseProduct.Id, quantity, quantity);
                
                for (int i = 1; i <= quantity; i++)
                {
                    var product = CloneProduct(baseProduct, i);
                    product.Id = $"{baseProduct.Id}_{i}"; // Use original ID with instance suffix
                    product.Qty = 1; // Each instance has quantity 1
                    
                    // Add parts, subassemblies, and hardware to this product instance
                    await PopulateProductChildrenAsync(rawData, product, baseProduct.Id, $"_{i}");
                    
                    workOrder.Products.Add(product);
                }
            }
        }
    }

    /// <summary>
    /// Create Product entity from raw data
    /// </summary>
    private Product CreateProductFromData(Dictionary<string, object?> productData, string workOrderId)
    {
        return new Product
        {
            Id = _columnMapping.GetStringValue(productData, "PRODUCTS", "Id"),
            Name = _columnMapping.GetStringValue(productData, "PRODUCTS", "Name"),
            ItemNumber = _columnMapping.GetStringValue(productData, "PRODUCTS", "ItemNumber"),
            Qty = _columnMapping.GetIntValue(productData, "PRODUCTS", "Quantity"),
            WorkOrderId = workOrderId,
            Status = PartStatus.Pending,
            StatusUpdatedDate = DateTime.Now,
            Parts = new List<Part>(),
            Subassemblies = new List<Subassembly>(),
            Hardware = new List<Hardware>()
        };
    }

    /// <summary>
    /// Clone product for quantity expansion with instance suffixing
    /// </summary>
    private Product CloneProduct(Product source, int instanceNumber)
    {
        return new Product
        {
            Id = source.Id, // Will be overridden with unique ID
            Name = $"{source.Name} (Copy {instanceNumber})", // Add copy suffix
            ItemNumber = source.ItemNumber,
            Qty = source.Qty,
            WorkOrderId = source.WorkOrderId,
            Status = source.Status,
            StatusUpdatedDate = source.StatusUpdatedDate,
            Parts = new List<Part>(),
            Subassemblies = new List<Subassembly>(),
            Hardware = new List<Hardware>()
        };
    }

    /// <summary>
    /// Populate product children with proper navigation properties
    /// </summary>
    private async Task PopulateProductChildrenAsync(ImportData rawData, Product product, string originalProductId, string? instanceSuffix = null)
    {
        // Add parts directly under product
        if (rawData.Parts != null)
        {
            var productParts = rawData.Parts
                .Where(p => _columnMapping.GetStringValue(p, "PARTS", "ProductId") == originalProductId)
                .Where(p => string.IsNullOrEmpty(_columnMapping.GetStringValue(p, "PARTS", "SubassemblyId")))
                .ToList();

            foreach (var partData in productParts)
            {
                var part = CreatePartFromData(partData, product.Id, product.WorkOrderId, instanceSuffix);
                product.Parts.Add(part);
            }
        }

        // Add subassemblies to product
        if (rawData.Subassemblies != null)
        {
            var productSubassemblies = rawData.Subassemblies
                .Where(s => _columnMapping.GetStringValue(s, "SUBASSEMBLIES", "ProductId") == originalProductId)
                .Where(s => string.IsNullOrEmpty(_columnMapping.GetStringValue(s, "SUBASSEMBLIES", "ParentSubassemblyId")))
                .ToList();

            foreach (var subassemblyData in productSubassemblies)
            {
                var subassembly = await CreateSubassemblyFromDataAsync(subassemblyData, product.Id, product.WorkOrderId, rawData, instanceSuffix);
                product.Subassemblies.Add(subassembly);
            }
        }

        // Add hardware to product
        if (rawData.Hardware != null)
        {
            var productHardware = rawData.Hardware
                .Where(h => _columnMapping.GetStringValue(h, "HARDWARE", "ProductId") == originalProductId)
                .ToList();

            foreach (var hardwareData in productHardware)
            {
                var hardware = CreateHardwareFromData(hardwareData, product.Id, product.WorkOrderId);
                product.Hardware.Add(hardware);
            }
        }
    }

    /// <summary>
    /// Create Part entity from raw data with instance suffix support
    /// </summary>
    private Part CreatePartFromData(Dictionary<string, object?> partData, string productId, string workOrderId, string? instanceSuffix = null)
    {
        var partId = _columnMapping.GetStringValue(partData, "PARTS", "Id");
        
        // Apply instance suffix if this part belongs to a cloned product instance
        if (!string.IsNullOrEmpty(instanceSuffix))
        {
            partId = $"{partId}{instanceSuffix}";
        }
        
        return new Part
        {
            Id = partId,
            Name = _columnMapping.GetStringValue(partData, "PARTS", "Name"),
            Qty = _columnMapping.GetIntValue(partData, "PARTS", "Quantity"),
            Width = _columnMapping.GetDecimalValue(partData, "PARTS", "Width"),
            Length = _columnMapping.GetDecimalValue(partData, "PARTS", "Height"),
            Thickness = _columnMapping.GetDecimalValue(partData, "PARTS", "Thickness"),
            Material = _columnMapping.GetStringValue(partData, "PARTS", "Material"),
            ProductId = productId,
            Status = PartStatus.Pending,
            StatusUpdatedDate = DateTime.Now,
            Category = PartCategory.Standard // Will be set by categorization service in Phase I3
        };
    }

    /// <summary>
    /// Create Subassembly entity from raw data with recursive children and instance suffix support
    /// </summary>
    private async Task<Subassembly> CreateSubassemblyFromDataAsync(Dictionary<string, object?> subassemblyData, string productId, string workOrderId, ImportData rawData, string? instanceSuffix = null)
    {
        var subassemblyId = _columnMapping.GetStringValue(subassemblyData, "SUBASSEMBLIES", "Id");
        
        // Apply instance suffix if this subassembly belongs to a cloned product instance
        if (!string.IsNullOrEmpty(instanceSuffix))
        {
            subassemblyId = $"{subassemblyId}{instanceSuffix}";
        }
        
        var subassembly = new Subassembly
        {
            Id = subassemblyId,
            Name = _columnMapping.GetStringValue(subassemblyData, "SUBASSEMBLIES", "Name"),
            Qty = _columnMapping.GetIntValue(subassemblyData, "SUBASSEMBLIES", "Quantity"),
            ProductId = productId,
            Parts = new List<Part>(),
            ChildSubassemblies = new List<Subassembly>()
        };

        // Add parts to subassembly
        if (rawData.Parts != null)
        {
            var subassemblyParts = rawData.Parts
                .Where(p => _columnMapping.GetStringValue(p, "PARTS", "SubassemblyId") == subassembly.Id)
                .ToList();

            foreach (var partData in subassemblyParts)
            {
                var part = CreatePartFromData(partData, productId, workOrderId, instanceSuffix);
                part.SubassemblyId = subassembly.Id;
                subassembly.Parts.Add(part);
            }
        }

        // Add nested subassemblies
        if (rawData.Subassemblies != null)
        {
            var nestedSubassemblies = rawData.Subassemblies
                .Where(s => _columnMapping.GetStringValue(s, "SUBASSEMBLIES", "ParentSubassemblyId") == subassembly.Id)
                .ToList();

            foreach (var nestedData in nestedSubassemblies)
            {
                var nested = await CreateSubassemblyFromDataAsync(nestedData, productId, workOrderId, rawData, instanceSuffix);
                nested.ParentSubassemblyId = subassembly.Id;
                subassembly.ChildSubassemblies.Add(nested);
            }
        }

        // Note: Subassembly model doesn't have Hardware collection in current schema
        // Hardware is managed at Product level only

        return subassembly;
    }

    /// <summary>
    /// Create Hardware entity from raw data
    /// </summary>
    private Hardware CreateHardwareFromData(Dictionary<string, object?> hardwareData, string productId, string workOrderId)
    {
        return new Hardware
        {
            Id = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Id"),
            Name = _columnMapping.GetStringValue(hardwareData, "HARDWARE", "Name"),
            Qty = _columnMapping.GetIntValue(hardwareData, "HARDWARE", "Quantity"),
            ProductId = productId,
            WorkOrderId = workOrderId,
            Status = PartStatus.Pending,
            StatusUpdatedDate = DateTime.Now
        };
    }


    /// <summary>
    /// Transform detached products - Note: DetachedProducts are not in SDF data
    /// They are identified and converted from single-part Products by ProcessSinglePartProductsAsDetached
    /// </summary>
    private void TransformDetachedProducts(ImportData rawData, WorkOrder workOrder)
    {
        // DetachedProducts are not present in SDF data
        // They are created by converting single-part Products in ProcessSinglePartProductsAsDetached()
    }

    /// <summary>
    /// Process single-part products and convert them to detached products
    /// Based on the original ImportDataTransformService.ProcessSinglePartProductsAsDetached logic
    /// </summary>
    private void ProcessSinglePartProductsAsDetached(WorkOrder workOrder)
    {
        // Find products with exactly 1 part and no subassemblies
        var singlePartProducts = workOrder.Products
            .Where(p => p.Parts.Count == 1 && p.Subassemblies.Count == 0)
            .ToList();
        
        // Create list to track products to remove after conversion
        var productsToRemove = new List<Product>();
        
        foreach (var product in singlePartProducts)
        {
            var singlePart = product.Parts.First();
            
            // Create a detached product from this single-part product
            var detachedProduct = new DetachedProduct
            {
                Id = $"{product.Id}_detached_{Guid.NewGuid().ToString()[..8]}", // Create unique ID to avoid conflicts
                ItemNumber = product.ItemNumber,
                Name = product.Name,
                Qty = product.Qty,
                Length = singlePart.Length,
                Width = singlePart.Width,
                Thickness = singlePart.Thickness,
                Material = singlePart.Material ?? "Unknown",
                EdgebandingTop = singlePart.EdgebandingTop ?? "",
                EdgebandingBottom = singlePart.EdgebandingBottom ?? "",
                EdgebandingLeft = singlePart.EdgebandingLeft ?? "",
                EdgebandingRight = singlePart.EdgebandingRight ?? "",
                WorkOrderId = workOrder.Id,
                Status = singlePart.Status, // Inherit status from the part
                StatusUpdatedDate = singlePart.StatusUpdatedDate ?? DateTime.Now,
                Parts = new List<Part>()
            };
            
            // Transfer the Part to DetachedProduct (don't delete it)
            singlePart.ProductId = detachedProduct.Id; // Update foreign key
            detachedProduct.Parts.Add(singlePart);
            
            // Remove Part from Product before deleting Product
            product.Parts.Clear();
            
            workOrder.DetachedProducts.Add(detachedProduct);
            productsToRemove.Add(product);
        }
        
        // Remove the original single-part products (now empty of Parts)
        foreach (var product in productsToRemove)
        {
            workOrder.Products.Remove(product);
        }
        
        if (singlePartProducts.Any())
        {
            _logger.LogInformation("Identified {Count} single-part products as detached products during transformation", singlePartProducts.Count);
        }
    }

    /// <summary>
    /// Transform hardware entities
    /// </summary>
    private void TransformHardware(ImportData rawData, WorkOrder workOrder)
    {
        if (rawData.Hardware == null) return;

        // Only add hardware that's not associated with products/subassemblies
        var standaloneHardware = rawData.Hardware
            .Where(h => string.IsNullOrEmpty(_columnMapping.GetStringValue(h, "HARDWARE", "ProductId")) &&
                       string.IsNullOrEmpty(_columnMapping.GetStringValue(h, "HARDWARE", "SubassemblyId")))
            .ToList();

        foreach (var hardwareData in standaloneHardware)
        {
            var hardware = CreateHardwareFromData(hardwareData, "", workOrder.Id);
            workOrder.Hardware.Add(hardware);
        }
    }

    /// <summary>
    /// Transform nest sheets
    /// </summary>
    private void TransformNestSheets(ImportData rawData, WorkOrder workOrder)
    {
        if (rawData.NestSheets == null) return;

        foreach (var nestSheetData in rawData.NestSheets)
        {
            var nestSheet = new NestSheet
            {
                Id = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "Id"),
                Name = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "FileName"),
                Material = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "Material"),
                Length = _columnMapping.GetDecimalValue(nestSheetData, "PLACEDSHEETS", "Length"),
                Width = _columnMapping.GetDecimalValue(nestSheetData, "PLACEDSHEETS", "Width"),
                Thickness = _columnMapping.GetDecimalValue(nestSheetData, "PLACEDSHEETS", "Thickness"),
                Barcode = _columnMapping.GetStringValue(nestSheetData, "PLACEDSHEETS", "FileName"),
                WorkOrderId = workOrder.Id,
                Status = PartStatus.Pending,
                StatusUpdatedDate = DateTime.Now,
                Parts = new List<Part>()
            };

            // Note: Parts are associated with nest sheets later via OptimizationResults
            // See EstablishNestSheetRelationshipsAsync method

            workOrder.NestSheets.Add(nestSheet);
        }
    }

    /// <summary>
    /// Phase I3: Apply auto-categorization to all parts in the WorkOrder
    /// Runs as final step of import processing to ensure all parts are categorized
    /// </summary>
    private async Task ApplyAutoCategorizationAsync(WorkOrder workOrder)
    {
        var totalParts = 0;
        var categorizedParts = 0;

        _logger.LogInformation("Phase I3: Starting auto-categorization for WorkOrder {WorkOrderId}", workOrder.Id);

        // Categorize parts in Products
        foreach (var product in workOrder.Products)
        {
            foreach (var part in product.Parts)
            {
                totalParts++;
                var originalCategory = part.Category;
                part.Category = _partFilteringService.ClassifyPart(part);
                
                if (part.Category != originalCategory)
                {
                    categorizedParts++;
                    _logger.LogDebug("Part '{PartName}' categorized as {Category} (was {OriginalCategory})", 
                        part.Name, part.Category, originalCategory);
                }
            }

            // Categorize parts in Subassemblies
            foreach (var subassembly in product.Subassemblies)
            {
                var result = await CategorizeSubassemblyPartsAsync(subassembly, totalParts, categorizedParts);
                totalParts = result.totalParts;
                categorizedParts = result.categorizedParts;
            }
        }

        // Categorize parts in NestSheets
        foreach (var nestSheet in workOrder.NestSheets)
        {
            foreach (var part in nestSheet.Parts)
            {
                totalParts++;
                var originalCategory = part.Category;
                part.Category = _partFilteringService.ClassifyPart(part);
                
                if (part.Category != originalCategory)
                {
                    categorizedParts++;
                    _logger.LogDebug("NestSheet part '{PartName}' categorized as {Category} (was {OriginalCategory})", 
                        part.Name, part.Category, originalCategory);
                }
            }
        }

        _logger.LogInformation("Phase I3: Auto-categorization completed. Processed {TotalParts} parts, {CategorizedParts} changed from default", 
            totalParts, categorizedParts);
    }

    /// <summary>
    /// Recursively categorize parts in subassemblies
    /// </summary>
    private async Task<(int totalParts, int categorizedParts)> CategorizeSubassemblyPartsAsync(Subassembly subassembly, int totalParts, int categorizedParts)
    {
        foreach (var part in subassembly.Parts)
        {
            totalParts++;
            var originalCategory = part.Category;
            part.Category = _partFilteringService.ClassifyPart(part);
            
            if (part.Category != originalCategory)
            {
                categorizedParts++;
                _logger.LogDebug("Subassembly part '{PartName}' categorized as {Category} (was {OriginalCategory})", 
                    part.Name, part.Category, originalCategory);
            }
        }

        // Recursively process nested subassemblies
        foreach (var childSubassembly in subassembly.ChildSubassemblies)
        {
            var result = await CategorizeSubassemblyPartsAsync(childSubassembly, totalParts, categorizedParts);
            totalParts = result.totalParts;
            categorizedParts = result.categorizedParts;
        }

        return (totalParts, categorizedParts);
    }

    /// <summary>
    /// Establish nest sheet relationships using OptimizationResults (same approach as existing system)
    /// Parts are associated with nest sheets through the OptimizationResults table, not direct column mapping
    /// </summary>
    private Task EstablishNestSheetRelationshipsAsync(ImportData rawData, WorkOrder workOrder)
    {
        try
        {
            _logger.LogInformation("Phase I3: Establishing nest sheet relationships using sequential OptimizationResults processing");

            // Create a lookup of nest sheet LinkID to NestSheet entity
            var sheetIdToNestSheetMapping = new Dictionary<string, NestSheet>();
            foreach (var nestSheet in workOrder.NestSheets)
            {
                sheetIdToNestSheetMapping[nestSheet.Id] = nestSheet;
            }

            // Track assigned parts to prevent duplicate assignments
            var assignedPartIds = new HashSet<string>();
            int assignedCount = 0;

            // Process OptimizationResults sequentially - each row assigns to one part instance
            foreach (var optimizationResult in rawData.OptimizationResults ?? new List<Dictionary<string, object?>>())
            {
                var linkIdPart = optimizationResult.TryGetValue("LinkIDPart", out var partValue) ? partValue?.ToString() : null;
                var linkIdSheet = optimizationResult.TryGetValue("LinkIDSheet", out var sheetValue) ? sheetValue?.ToString() : null;
                
                if (string.IsNullOrEmpty(linkIdPart) || string.IsNullOrEmpty(linkIdSheet))
                    continue;

                // Find first unassigned part with this original ID (handles suffixed parts)
                var unassignedPart = FindFirstUnassignedPartInWorkOrder(workOrder, linkIdPart, assignedPartIds);
                
                if (unassignedPart != null && sheetIdToNestSheetMapping.ContainsKey(linkIdSheet))
                {
                    // Assign part to nest sheet
                    unassignedPart.NestSheetId = linkIdSheet;
                    var nestSheet = sheetIdToNestSheetMapping[linkIdSheet];
                    
                    // Add to nest sheet's Parts collection
                    if (!nestSheet.Parts.Any(p => p.Id == unassignedPart.Id))
                    {
                        nestSheet.Parts.Add(unassignedPart);
                    }
                    
                    // Mark as assigned
                    assignedPartIds.Add(unassignedPart.Id);
                    assignedCount++;
                    
                    _logger.LogDebug("Assigned part '{PartId}' to nest sheet '{SheetId}'", unassignedPart.Id, linkIdSheet);
                }
                else
                {
                    _logger.LogWarning("Could not assign OptimizationResult: LinkIDPart='{LinkIdPart}' LinkIDSheet='{LinkIdSheet}' - Part not found or sheet missing", 
                        linkIdPart, linkIdSheet);
                }
            }

            _logger.LogInformation("Established nest sheet relationships for {AssignedCount} parts using sequential OptimizationResults processing", 
                assignedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error establishing nest sheet part relationships");
        }
        return Task.CompletedTask;
    }


    /// <summary>
    /// Extract original part ID from suffixed ID (e.g., "PART123_1" → "PART123")
    /// </summary>
    private string ExtractOriginalPartId(string partId)
    {
        if (partId.Contains('_') && char.IsDigit(partId.Last()))
        {
            var lastUnderscoreIndex = partId.LastIndexOf('_');
            return partId.Substring(0, lastUnderscoreIndex);
        }
        return partId;
    }

    /// <summary>
    /// Find first unassigned part in work order with matching original ID
    /// </summary>
    private Part? FindFirstUnassignedPartInWorkOrder(WorkOrder workOrder, string originalPartId, HashSet<string> assignedPartIds)
    {
        // Search through all products
        foreach (var product in workOrder.Products)
        {
            // Check parts directly under product
            foreach (var part in product.Parts)
            {
                if (ExtractOriginalPartId(part.Id) == originalPartId && 
                    !assignedPartIds.Contains(part.Id) && 
                    string.IsNullOrEmpty(part.NestSheetId))
                {
                    return part;
                }
            }

            // Check parts in subassemblies
            foreach (var subassembly in product.Subassemblies)
            {
                var foundPart = FindFirstUnassignedPartInSubassembly(subassembly, originalPartId, assignedPartIds);
                if (foundPart != null)
                {
                    return foundPart;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Recursively search for unassigned parts in subassemblies
    /// </summary>
    private Part? FindFirstUnassignedPartInSubassembly(Subassembly subassembly, string originalPartId, HashSet<string> assignedPartIds)
    {
        // Check parts in this subassembly
        foreach (var part in subassembly.Parts)
        {
            if (ExtractOriginalPartId(part.Id) == originalPartId && 
                !assignedPartIds.Contains(part.Id) && 
                string.IsNullOrEmpty(part.NestSheetId))
            {
                return part;
            }
        }

        // Check nested subassemblies
        foreach (var nested in subassembly.ChildSubassemblies)
        {
            var foundPart = FindFirstUnassignedPartInSubassembly(nested, originalPartId, assignedPartIds);
            if (foundPart != null)
            {
                return foundPart;
            }
        }
        return null;
    }
}