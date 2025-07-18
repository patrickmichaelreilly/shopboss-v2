using ShopBoss.Web.Models;
using ShopBoss.Web.Models.Import;

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
            await TransformDetachedProductsAsync(rawData, workOrder);
            await TransformHardwareAsync(rawData, workOrder);
            await TransformNestSheetsAsync(rawData, workOrder);

            // Establish nest sheet relationships using OptimizationResults (same as existing system)
            await EstablishNestSheetRelationshipsAsync(rawData, workOrder);

            // Phase I3: Apply auto-categorization to all parts (final step of import processing)
            await ApplyAutoCategorizationAsync(workOrder);

            _logger.LogInformation("Phase I2: WorkOrder transformation completed. Created - Products: {ProductCount}, Parts: {PartCount}, Hardware: {HardwareCount}", 
                workOrder.Products.Count, 
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
            for (int i = 0; i < quantity; i++)
            {
                var product = CloneProduct(baseProduct);
                product.Id = Guid.NewGuid().ToString(); // Each instance gets unique ID
                product.Qty = 1; // Each instance has quantity 1
                
                // Add parts, subassemblies, and hardware to this product instance
                await PopulateProductChildrenAsync(rawData, product, baseProduct.Id);
                
                workOrder.Products.Add(product);
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
            ProductNumber = _columnMapping.GetStringValue(productData, "PRODUCTS", "ProductNumber"),
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
    /// Clone product for quantity expansion
    /// </summary>
    private Product CloneProduct(Product source)
    {
        return new Product
        {
            Id = source.Id, // Will be overridden with unique ID
            Name = source.Name,
            ProductNumber = source.ProductNumber,
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
    private async Task PopulateProductChildrenAsync(ImportData rawData, Product product, string originalProductId)
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
                var part = CreatePartFromData(partData, product.Id, product.WorkOrderId);
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
                var subassembly = await CreateSubassemblyFromDataAsync(subassemblyData, product.Id, product.WorkOrderId, rawData);
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
    /// Create Part entity from raw data
    /// </summary>
    private Part CreatePartFromData(Dictionary<string, object?> partData, string productId, string workOrderId)
    {
        return new Part
        {
            Id = _columnMapping.GetStringValue(partData, "PARTS", "Id"),
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
    /// Create Subassembly entity from raw data with recursive children
    /// </summary>
    private async Task<Subassembly> CreateSubassemblyFromDataAsync(Dictionary<string, object?> subassemblyData, string productId, string workOrderId, ImportData rawData)
    {
        var subassembly = new Subassembly
        {
            Id = _columnMapping.GetStringValue(subassemblyData, "SUBASSEMBLIES", "Id"),
            Name = _columnMapping.GetStringValue(subassemblyData, "SUBASSEMBLIES", "Name"),
            Qty = _columnMapping.GetIntValue(subassemblyData, "SUBASSEMBLIES", "Qty"),
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
                var part = CreatePartFromData(partData, productId, workOrderId);
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
                var nested = await CreateSubassemblyFromDataAsync(nestedData, productId, workOrderId, rawData);
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
    /// They are identified later as single-part Products
    /// </summary>
    private async Task TransformDetachedProductsAsync(ImportData rawData, WorkOrder workOrder)
    {
        // DetachedProducts are not present in SDF data
        // They will be identified later through categorization
        await Task.CompletedTask;
    }

    /// <summary>
    /// Transform hardware entities
    /// </summary>
    private async Task TransformHardwareAsync(ImportData rawData, WorkOrder workOrder)
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
    private async Task TransformNestSheetsAsync(ImportData rawData, WorkOrder workOrder)
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
    private async Task EstablishNestSheetRelationshipsAsync(ImportData rawData, WorkOrder workOrder)
    {
        try
        {
            _logger.LogInformation("Phase I3: Establishing nest sheet relationships using OptimizationResults");

            // Create a mapping of LinkIDPart to LinkIDSheet from OptimizationResults
            var partToSheetMapping = new Dictionary<string, string>();
            
            foreach (var optimizationResult in rawData.OptimizationResults ?? new List<Dictionary<string, object?>>())
            {
                var linkIdPart = optimizationResult.TryGetValue("LinkIDPart", out var partValue) ? partValue?.ToString() : null;
                var linkIdSheet = optimizationResult.TryGetValue("LinkIDSheet", out var sheetValue) ? sheetValue?.ToString() : null;
                
                if (!string.IsNullOrEmpty(linkIdPart) && !string.IsNullOrEmpty(linkIdSheet))
                {
                    partToSheetMapping[linkIdPart] = linkIdSheet;
                }
            }

            _logger.LogInformation("Created part to sheet mapping with {Count} entries", partToSheetMapping.Count);

            // Create a lookup of nest sheet LinkID to NestSheet entity
            var sheetIdToNestSheetMapping = new Dictionary<string, NestSheet>();
            foreach (var nestSheet in workOrder.NestSheets)
            {
                sheetIdToNestSheetMapping[nestSheet.Id] = nestSheet;
            }

            // Update parts with proper nest sheet associations
            await UpdatePartsWithNestSheetInfoAsync(workOrder, partToSheetMapping, sheetIdToNestSheetMapping);

            _logger.LogInformation("Established nest sheet relationships for {PartCount} parts using OptimizationResults", 
                partToSheetMapping.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error establishing nest sheet part relationships");
        }
    }

    /// <summary>
    /// Update all parts in the WorkOrder with nest sheet information
    /// </summary>
    private async Task UpdatePartsWithNestSheetInfoAsync(WorkOrder workOrder, 
        Dictionary<string, string> partToSheetMapping, 
        Dictionary<string, NestSheet> sheetIdToNestSheetMapping)
    {
        foreach (var product in workOrder.Products)
        {
            // Update parts in product
            foreach (var part in product.Parts)
            {
                UpdatePartNestSheetInfo(part, partToSheetMapping, sheetIdToNestSheetMapping);
            }

            // Update parts in subassemblies
            foreach (var subassembly in product.Subassemblies)
            {
                await UpdatePartsInSubassemblyAsync(subassembly, partToSheetMapping, sheetIdToNestSheetMapping);
            }
        }
    }

    /// <summary>
    /// Recursively update parts in subassemblies with nest sheet information
    /// </summary>
    private async Task UpdatePartsInSubassemblyAsync(Subassembly subassembly, 
        Dictionary<string, string> partToSheetMapping, 
        Dictionary<string, NestSheet> sheetIdToNestSheetMapping)
    {
        foreach (var part in subassembly.Parts)
        {
            UpdatePartNestSheetInfo(part, partToSheetMapping, sheetIdToNestSheetMapping);
        }

        // Handle nested subassemblies
        foreach (var nested in subassembly.ChildSubassemblies)
        {
            await UpdatePartsInSubassemblyAsync(nested, partToSheetMapping, sheetIdToNestSheetMapping);
        }
    }

    /// <summary>
    /// Update a single part with nest sheet information
    /// </summary>
    private void UpdatePartNestSheetInfo(Part part, 
        Dictionary<string, string> partToSheetMapping, 
        Dictionary<string, NestSheet> sheetIdToNestSheetMapping)
    {
        if (partToSheetMapping.ContainsKey(part.Id))
        {
            var nestSheetId = partToSheetMapping[part.Id];
            part.NestSheetId = nestSheetId;
            
            // Add the part to the nest sheet's Parts collection
            if (sheetIdToNestSheetMapping.ContainsKey(nestSheetId))
            {
                var nestSheet = sheetIdToNestSheetMapping[nestSheetId];
                if (!nestSheet.Parts.Any(p => p.Id == part.Id))
                {
                    nestSheet.Parts.Add(part);
                }
            }
        }
    }
}