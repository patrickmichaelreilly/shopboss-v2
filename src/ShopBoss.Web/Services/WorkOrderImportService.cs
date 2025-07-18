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

    public WorkOrderImportService(ILogger<WorkOrderImportService> logger, ColumnMappingService columnMapping)
    {
        _logger = logger;
        _columnMapping = columnMapping;
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
                WorkOrderId = workOrder.Id,
                Status = PartStatus.Pending,
                StatusUpdatedDate = DateTime.Now,
                Parts = new List<Part>()
            };

            // Add parts associated with this nest sheet
            if (rawData.Parts != null)
            {
                var nestSheetParts = rawData.Parts
                    .Where(p => _columnMapping.GetStringValue(p, "PARTS", "NestSheetId") == nestSheet.Id)
                    .ToList();

                foreach (var partData in nestSheetParts)
                {
                    var part = CreatePartFromData(partData, "", workOrder.Id);
                    part.NestSheetId = nestSheet.Id;
                    nestSheet.Parts.Add(part);
                }
            }

            workOrder.NestSheets.Add(nestSheet);
        }
    }
}