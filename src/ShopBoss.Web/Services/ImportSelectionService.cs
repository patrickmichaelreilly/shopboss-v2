using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Models.Import;
using Microsoft.EntityFrameworkCore;

namespace ShopBoss.Web.Services;

public class ImportSelectionService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<ImportSelectionService> _logger;

    public ImportSelectionService(ShopBossDbContext context, ILogger<ImportSelectionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ImportConversionResult> ConvertSelectedItemsAsync(
        ImportWorkOrder importData, 
        SelectionRequest selection)
    {
        var result = new ImportConversionResult();
        
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            _logger.LogInformation("Starting conversion of selected items for work order: {WorkOrderName}", selection.WorkOrderName);

            // Validate selection
            var validationResult = ValidateSelection(importData, selection);
            if (!validationResult.IsValid)
            {
                result.Errors.AddRange(validationResult.Errors);
                return result;
            }

            // Check for duplicate work order
            var duplicateCheck = await CheckForDuplicateWorkOrder(importData.Id, selection.WorkOrderName);
            if (!duplicateCheck.IsValid)
            {
                result.Errors.AddRange(duplicateCheck.Errors);
                return result;
            }

            // Create the work order entity
            var workOrder = CreateWorkOrderEntity(importData, selection);
            
            // Process nest sheets first (they're needed for parts)
            ProcessSelectedNestSheets(importData, selection, workOrder, result);
            
            // Process selected items
            ProcessSelectedProducts(importData, selection, workOrder, result);
            ProcessSelectedDetachedProducts(importData, selection, workOrder, result);
            
            // Identify products with only 1 part as detached products
            ProcessSinglePartProductsAsDetached(workOrder, result);

            // Save to database
            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();
            
            // Commit transaction
            await transaction.CommitAsync();

            result.Success = true;
            result.WorkOrderId = workOrder.Id;
            
            _logger.LogInformation("Successfully saved work order {WorkOrderId} with {ProductCount} products, {PartCount} parts, {SubassemblyCount} subassemblies, {HardwareCount} hardware items",
                workOrder.Id,
                result.Statistics.ConvertedProducts,
                result.Statistics.ConvertedParts,
                result.Statistics.ConvertedSubassemblies,
                result.Statistics.ConvertedHardware);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting and saving selected items. Rolling back transaction.");
            await transaction.RollbackAsync();
            result.Errors.Add($"Import error: {ex.Message}");
        }

        return result;
    }

    private async Task<SelectionValidationResult> CheckForDuplicateWorkOrder(string workOrderId, string workOrderName)
    {
        var result = new SelectionValidationResult { IsValid = true };

        // Check for duplicate Microvellum ID
        var existingById = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Id == workOrderId);
        if (existingById != null)
        {
            result.Errors.Add($"Work order with Microvellum ID '{workOrderId}' already exists (imported as '{existingById.Name}' on {existingById.ImportedDate:yyyy-MM-dd})");
            result.IsValid = false;
        }

        // Check for duplicate name
        var existingByName = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Name == workOrderName);
        if (existingByName != null)
        {
            result.Errors.Add($"Work order with name '{workOrderName}' already exists (Microvellum ID: {existingByName.Id}, imported on {existingByName.ImportedDate:yyyy-MM-dd})");
            result.IsValid = false;
        }

        return result;
    }

    private SelectionValidationResult ValidateSelection(ImportWorkOrder importData, SelectionRequest selection)
    {
        var result = new SelectionValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(selection.WorkOrderName))
        {
            result.Errors.Add("Work order name is required");
            result.IsValid = false;
        }

        if (!selection.SelectedItemIds.Any())
        {
            result.Errors.Add("At least one item must be selected for import");
            result.IsValid = false;
        }

        // Validate that selected items exist in import data
        var allImportItemIds = new HashSet<string>();
        
        // Collect all valid item IDs from import data
        foreach (var product in importData.Products)
        {
            allImportItemIds.Add(product.Id);
            CollectItemIds(product, allImportItemIds);
        }
        
        foreach (var hardware in importData.Hardware)
        {
            allImportItemIds.Add(hardware.Id);
        }
        
        foreach (var detached in importData.DetachedProducts)
        {
            allImportItemIds.Add(detached.Id);
        }
        
        foreach (var nestSheet in importData.NestSheets)
        {
            allImportItemIds.Add(nestSheet.Id);
        }

        // Check for invalid selection IDs
        var invalidIds = selection.SelectedItemIds.Where(id => !allImportItemIds.Contains(id)).ToList();
        if (invalidIds.Any())
        {
            result.Errors.Add($"Invalid item IDs selected: {string.Join(", ", invalidIds)}");
            result.IsValid = false;
        }

        return result;
    }

    private void CollectItemIds(ImportProduct product, HashSet<string> itemIds)
    {
        foreach (var part in product.Parts)
        {
            itemIds.Add(part.Id);
        }

        foreach (var subassembly in product.Subassemblies)
        {
            itemIds.Add(subassembly.Id);
            CollectItemIds(subassembly, itemIds);
        }

        foreach (var hardware in product.Hardware)
        {
            itemIds.Add(hardware.Id);
        }
    }

    private void CollectItemIds(ImportSubassembly subassembly, HashSet<string> itemIds)
    {
        foreach (var part in subassembly.Parts)
        {
            itemIds.Add(part.Id);
        }

        foreach (var nested in subassembly.NestedSubassemblies)
        {
            itemIds.Add(nested.Id);
            CollectItemIds(nested, itemIds);
        }

        foreach (var hardware in subassembly.Hardware)
        {
            itemIds.Add(hardware.Id);
        }
    }

    private WorkOrder CreateWorkOrderEntity(ImportWorkOrder importData, SelectionRequest selection)
    {
        return new WorkOrder
        {
            Id = importData.Id, // Preserve Microvellum ID
            Name = selection.WorkOrderName,
            ImportedDate = DateTime.Now
        };
    }

    private void ProcessSelectedProducts(
        ImportWorkOrder importData, 
        SelectionRequest selection, 
        WorkOrder workOrder, 
        ImportConversionResult result)
    {
        // Phase 1: Normalize products into individual instances
        var normalizedProducts = NormalizeProductQuantities(importData, selection, workOrder);
        
        // Phase 2: Process content for each individual product
        foreach (var product in normalizedProducts)
        {
            workOrder.Products.Add(product);
            
            // Find the original import product for this normalized product
            var originalProductId = product.Id.Contains("_") ? 
                product.Id.Substring(0, product.Id.LastIndexOf('_')) : 
                product.Id;
            
            var importProduct = importData.Products.First(p => p.Id == originalProductId);
            
            // Process content for this individual product (no global tracking)
            ProcessProductContent(importProduct, selection, product, workOrder, result);
            
            result.Statistics.ConvertedProducts++;
        }
    }

    private List<Product> NormalizeProductQuantities(
        ImportWorkOrder importData, 
        SelectionRequest selection, 
        WorkOrder workOrder)
    {
        var normalizedProducts = new List<Product>();
        
        var selectedProductIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "product")
            .ToHashSet();

        foreach (var importProduct in importData.Products.Where(p => selectedProductIds.Contains(p.Id)))
        {
            // Handle multiple product quantities by creating individual product instances
            // Business Rule: Products with Qty > 1 are normalized to multiple Qty = 1 products
            // This simplifies assembly/shipping tracking (each product tracked individually)
            var productQuantity = importProduct.Quantity;
            
            if (productQuantity > 1)
            {
                _logger.LogInformation("Converting product '{ProductName}' (ID: {ProductId}) with quantity {Quantity} to {Quantity} individual product instances",
                    importProduct.Name, importProduct.Id, productQuantity, productQuantity);
            }
            
            for (int i = 1; i <= productQuantity; i++)
            {
                // Create individual product instance with Qty = 1
                var product = ConvertToProductEntity(importProduct, workOrder.Id);
                
                // Make each product instance unique if quantity > 1
                if (productQuantity > 1)
                {
                    product.Id = $"{importProduct.Id}_{i}";
                    product.Name = $"{importProduct.Name} (Instance {i})";
                }
                product.Qty = 1; // Each instance is quantity 1
                
                normalizedProducts.Add(product);
            }
        }
        
        return normalizedProducts;
    }

    private List<Subassembly> NormalizeSubassemblyQuantities(ImportSubassembly importSubassembly, string? productId)
    {
        var normalizedSubassemblies = new List<Subassembly>();
        var subassemblyQuantity = importSubassembly.Quantity;
        
        if (subassemblyQuantity > 1)
        {
            _logger.LogInformation("Converting subassembly '{SubassemblyName}' (ID: {SubassemblyId}) with quantity {Quantity} to {Quantity} individual subassembly instances",
                importSubassembly.Name, importSubassembly.Id, subassemblyQuantity, subassemblyQuantity);
        }
        
        for (int i = 1; i <= subassemblyQuantity; i++)
        {
            // Create individual subassembly instance with Qty = 1
            var subassembly = ConvertToSubassemblyEntity(importSubassembly, productId, null, productId);
            
            // Make each subassembly instance unique if quantity > 1
            if (subassemblyQuantity > 1)
            {
                subassembly.Id = $"{importSubassembly.Id}_{i}";
                subassembly.Name = $"{importSubassembly.Name} (Instance {i})";
            }
            subassembly.Qty = 1; // Each instance is quantity 1
            
            normalizedSubassemblies.Add(subassembly);
        }
        
        return normalizedSubassemblies;
    }

    private void ProcessProductContent(
        ImportProduct importProduct,
        SelectionRequest selection,
        Product product,
        WorkOrder workOrder,
        ImportConversionResult result)
    {
        // Process selected parts for this individual product
        ProcessSelectedPartsForProduct(importProduct, selection, product, workOrder, result);
        
        // Process selected subassemblies for this individual product
        ProcessSelectedSubassembliesForProduct(importProduct, selection, product, workOrder, result);
        
        // Process selected hardware for this individual product (no global tracking)
        ProcessSelectedHardwareForProduct(importProduct, selection, product, workOrder, result);
    }

    private void ProcessSelectedPartsForProduct(
        ImportProduct importProduct,
        SelectionRequest selection,
        Product product,
        WorkOrder workOrder,
        ImportConversionResult result)
    {
        var selectedPartIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "part")
            .ToHashSet();

        foreach (var importPart in importProduct.Parts.Where(p => selectedPartIds.Contains(p.Id)))
        {
            var part = ConvertToPartEntity(importPart, product.Id, null, workOrder, product.Id);
            product.Parts.Add(part);
            result.Statistics.ConvertedParts++;
        }
    }

    private void ProcessSelectedSubassembliesForProduct(
        ImportProduct importProduct,
        SelectionRequest selection,
        Product product,
        WorkOrder workOrder,
        ImportConversionResult result)
    {
        var selectedSubassemblyIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "subassembly")
            .ToHashSet();

        foreach (var importSubassembly in importProduct.Subassemblies.Where(s => selectedSubassemblyIds.Contains(s.Id)))
        {
            // Apply two-phase processing: normalize subassembly quantities
            var normalizedSubassemblies = NormalizeSubassemblyQuantities(importSubassembly, product.Id);
            
            foreach (var subassembly in normalizedSubassemblies)
            {
                product.Subassemblies.Add(subassembly);

                // Recursively process selected items within this subassembly
                ProcessSelectedItemsInSubassembly(importSubassembly, selection, subassembly, workOrder, result, product.Id);

                result.Statistics.ConvertedSubassemblies++;
            }
        }
    }

    private void ProcessSelectedHardwareForProduct(
        ImportProduct importProduct,
        SelectionRequest selection,
        Product product,
        WorkOrder workOrder,
        ImportConversionResult result)
    {
        var selectedHardwareIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "hardware")
            .ToHashSet();

        // Process hardware for this individual product instance (no global tracking)
        foreach (var importHardware in importProduct.Hardware.Where(h => selectedHardwareIds.Contains(h.Id)))
        {
            var hardware = new Hardware
            {
                Id = Guid.NewGuid().ToString(),
                MicrovellumId = importHardware.Id,
                Name = importHardware.Name,
                Qty = importHardware.Quantity, // Original quantity per product
                WorkOrderId = product.WorkOrderId,
                ProductId = product.Id
            };
            
            product.Hardware.Add(hardware);
            result.Statistics.ConvertedHardware++;
        }
    }

    private void ProcessSelectedItemsInSubassembly(
        ImportSubassembly importSubassembly,
        SelectionRequest selection,
        Subassembly subassembly,
        WorkOrder workOrder,
        ImportConversionResult result,
        string? productInstanceId = null)
    {
        var selectedPartIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "part")
            .ToHashSet();

        var selectedSubassemblyIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "subassembly")
            .ToHashSet();

        // Process parts in this subassembly
        foreach (var importPart in importSubassembly.Parts.Where(p => selectedPartIds.Contains(p.Id)))
        {
            var part = ConvertToPartEntity(importPart, subassembly.ProductId, subassembly.Id, workOrder, productInstanceId);
            subassembly.Parts.Add(part);
            result.Statistics.ConvertedParts++;
        }

        // Process nested subassemblies with two-phase processing
        foreach (var importNested in importSubassembly.NestedSubassemblies.Where(s => selectedSubassemblyIds.Contains(s.Id)))
        {
            // Apply two-phase processing: normalize nested subassembly quantities
            var normalizedNestedSubassemblies = NormalizeSubassemblyQuantities(importNested, subassembly.ProductId);
            
            foreach (var normalizedNested in normalizedNestedSubassemblies)
            {
                subassembly.ChildSubassemblies.Add(normalizedNested);

                // Recursively process items in each normalized nested subassembly instance
                ProcessSelectedItemsInSubassembly(importNested, selection, normalizedNested, workOrder, result, productInstanceId);

                result.Statistics.ConvertedSubassemblies++;
            }
        }

        // Process hardware in this subassembly
        var selectedHardwareIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "hardware")
            .ToHashSet();

        foreach (var importHardware in importSubassembly.Hardware.Where(h => selectedHardwareIds.Contains(h.Id)))
        {
            var hardware = new Hardware
            {
                Id = Guid.NewGuid().ToString(),
                MicrovellumId = importHardware.Id,
                Name = importHardware.Name,
                Qty = importHardware.Quantity, // Original quantity per subassembly instance
                WorkOrderId = subassembly.ProductId
            };
            
            workOrder.Hardware.Add(hardware);
            result.Statistics.ConvertedHardware++;
        }
    }


    private void ProcessSelectedDetachedProducts(
        ImportWorkOrder importData,
        SelectionRequest selection,
        WorkOrder workOrder,
        ImportConversionResult result)
    {
        var selectedDetachedIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "detached")
            .ToHashSet();

        foreach (var importDetached in importData.DetachedProducts.Where(d => selectedDetachedIds.Contains(d.Id)))
        {
            var detached = ConvertToDetachedProductEntity(importDetached, workOrder.Id);
            workOrder.DetachedProducts.Add(detached);
            result.Statistics.ConvertedDetachedProducts++;
        }
    }

    private Product ConvertToProductEntity(ImportProduct importProduct, string workOrderId)
    {
        return new Product
        {
            Id = importProduct.Id, // Preserve Microvellum ID
            ProductNumber = importProduct.ProductNumber,
            Name = importProduct.Name,
            Qty = importProduct.Quantity,
            Length = importProduct.Height, // Height maps to Length
            Width = importProduct.Width,
            WorkOrderId = workOrderId
        };
    }

    private Part ConvertToPartEntity(ImportPart importPart, string? productId, string? subassemblyId, WorkOrder workOrder, string? productInstanceId = null)
    {
        // Find the nest sheet for this part
        var nestSheet = FindNestSheetForPart(importPart, workOrder);
        
        // Create unique part ID for product instances
        var partId = importPart.Id;
        
        // Check if we need to make IDs unique (product instance has "_" suffix)
        if (!string.IsNullOrEmpty(productInstanceId) && productInstanceId.Contains("_"))
        {
            // Extract instance suffix from product ID (e.g., "PROD_1" -> "_1")
            var instanceSuffix = productInstanceId.Substring(productInstanceId.LastIndexOf('_'));
            partId = $"{importPart.Id}{instanceSuffix}";
        }
        
        return new Part
        {
            Id = partId, // Use unique part ID for product instances
            Name = importPart.Name,
            Qty = importPart.Quantity,
            Length = importPart.Height, // Height maps to Length
            Width = importPart.Width,
            Thickness = importPart.Thickness,
            Material = importPart.Material,
            EdgebandingTop = importPart.EdgeBanding?.Contains("Top") == true ? "Yes" : string.Empty,
            EdgebandingBottom = importPart.EdgeBanding?.Contains("Bottom") == true ? "Yes" : string.Empty,
            EdgebandingLeft = importPart.EdgeBanding?.Contains("Left") == true ? "Yes" : string.Empty,
            EdgebandingRight = importPart.EdgeBanding?.Contains("Right") == true ? "Yes" : string.Empty,
            ProductId = productId,
            SubassemblyId = subassemblyId,
            NestSheetId = nestSheet?.Id ?? CreateDefaultNestSheet(workOrder).Id,
            Status = PartStatus.Pending // Set initial status
        };
    }

    private Subassembly ConvertToSubassemblyEntity(ImportSubassembly importSubassembly, string? productId, string? parentSubassemblyId, string? productInstanceId = null)
    {
        // Create unique subassembly ID for product instances
        var subassemblyId = importSubassembly.Id;
        var updatedParentSubassemblyId = parentSubassemblyId;
        
        // Check if we need to make IDs unique (product instance has "_" suffix)
        if (!string.IsNullOrEmpty(productInstanceId) && productInstanceId.Contains("_"))
        {
            // Extract instance suffix from product ID (e.g., "PROD_1" -> "_1")
            var instanceSuffix = productInstanceId.Substring(productInstanceId.LastIndexOf('_'));
            subassemblyId = $"{importSubassembly.Id}{instanceSuffix}";
            
            // Also update parent subassembly ID if it exists
            if (!string.IsNullOrEmpty(parentSubassemblyId))
            {
                updatedParentSubassemblyId = $"{parentSubassemblyId}{instanceSuffix}";
            }
        }
        
        return new Subassembly
        {
            Id = subassemblyId, // Use unique subassembly ID for product instances
            Name = importSubassembly.Name,
            Qty = importSubassembly.Quantity,
            Length = importSubassembly.Height, // Height maps to Length
            Width = importSubassembly.Width,
            ProductId = productId,
            ParentSubassemblyId = updatedParentSubassemblyId
        };
    }

    private Hardware ConvertToHardwareEntity(ImportHardware importHardware, string workOrderId)
    {
        return new Hardware
        {
            Id = Guid.NewGuid().ToString(), // Use auto-generated GUID
            MicrovellumId = importHardware.Id, // Preserve Microvellum ID
            Name = importHardware.Name,
            Qty = importHardware.Quantity,
            WorkOrderId = workOrderId
        };
    }

    private DetachedProduct ConvertToDetachedProductEntity(ImportDetachedProduct importDetached, string workOrderId)
    {
        return new DetachedProduct
        {
            Id = importDetached.Id, // Preserve Microvellum ID
            ProductNumber = importDetached.Name, // ImportDetachedProduct doesn't have ProductNumber, use Name
            Name = importDetached.Name,
            Qty = importDetached.Quantity,
            Length = importDetached.Height, // Height maps to Length
            Width = importDetached.Width,
            Thickness = importDetached.Thickness,
            Material = importDetached.Material,
            EdgebandingTop = importDetached.EdgeBanding?.Contains("Top") == true ? "Yes" : string.Empty,
            EdgebandingBottom = importDetached.EdgeBanding?.Contains("Bottom") == true ? "Yes" : string.Empty,
            EdgebandingLeft = importDetached.EdgeBanding?.Contains("Left") == true ? "Yes" : string.Empty,
            EdgebandingRight = importDetached.EdgeBanding?.Contains("Right") == true ? "Yes" : string.Empty,
            WorkOrderId = workOrderId
        };
    }

    private void ProcessSelectedNestSheets(
        ImportWorkOrder importData,
        SelectionRequest selection,
        WorkOrder workOrder,
        ImportConversionResult result)
    {
        var selectedNestSheetIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "nestsheet")
            .ToHashSet();

        foreach (var importNestSheet in importData.NestSheets.Where(n => selectedNestSheetIds.Contains(n.Id)))
        {
            var nestSheet = ConvertToNestSheetEntity(importNestSheet, workOrder.Id);
            workOrder.NestSheets.Add(nestSheet);
            result.Statistics.ConvertedNestSheets++;
        }
    }

    private NestSheet ConvertToNestSheetEntity(ImportNestSheet importNestSheet, string workOrderId)
    {
        return new NestSheet
        {
            Id = importNestSheet.Id,
            Name = importNestSheet.Name,
            Material = importNestSheet.Material ?? string.Empty,
            Length = importNestSheet.Length,
            Width = importNestSheet.Width,
            Thickness = importNestSheet.Thickness,
            Barcode = importNestSheet.Barcode ?? importNestSheet.Name,
            WorkOrderId = workOrderId,
            CreatedDate = DateTime.UtcNow,
            IsProcessed = false
        };
    }

    private NestSheet? FindNestSheetForPart(ImportPart importPart, WorkOrder workOrder)
    {
        // Try to find by nest sheet name first
        if (!string.IsNullOrEmpty(importPart.NestSheetName))
        {
            return workOrder.NestSheets.FirstOrDefault(n => n.Name == importPart.NestSheetName);
        }

        // If no specific nest sheet name, try to find by nest sheet ID
        if (!string.IsNullOrEmpty(importPart.NestSheetId))
        {
            return workOrder.NestSheets.FirstOrDefault(n => n.Id == importPart.NestSheetId);
        }

        return null;
    }

    private NestSheet CreateDefaultNestSheet(WorkOrder workOrder)
    {
        // Check if default nest sheet already exists
        var defaultNestSheet = workOrder.NestSheets.FirstOrDefault(n => n.Name == "Default Nest Sheet");
        if (defaultNestSheet != null)
        {
            return defaultNestSheet;
        }

        // Create a default nest sheet for orphaned parts
        defaultNestSheet = new NestSheet
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Default Nest Sheet",
            Material = "Unknown",
            Barcode = "DEFAULT",
            WorkOrderId = workOrder.Id,
            CreatedDate = DateTime.UtcNow,
            IsProcessed = false
        };

        workOrder.NestSheets.Add(defaultNestSheet);
        return defaultNestSheet;
    }

    private void ProcessSinglePartProductsAsDetached(WorkOrder workOrder, ImportConversionResult result)
    {
        // Find products with exactly 1 part and treat them as detached products
        // This applies to both original products and normalized product instances
        var singlePartProducts = workOrder.Products
            .Where(p => p.Parts.Count == 1)
            .ToList();
        
        // Create list to track products to remove after conversion
        var productsToRemove = new List<Product>();
        
        foreach (var product in singlePartProducts)
        {
            var singlePart = product.Parts.First();
            
            // Create a detached product from this single-part product
            var detachedProduct = new DetachedProduct
            {
                Id = $"{product.Id}_detached", // Create unique ID to avoid conflicts
                ProductNumber = product.ProductNumber,
                Name = product.Name,
                Qty = product.Qty,
                Length = singlePart.Length,
                Width = singlePart.Width,
                Thickness = singlePart.Thickness,
                Material = singlePart.Material,
                EdgebandingTop = singlePart.EdgebandingTop,
                EdgebandingBottom = singlePart.EdgebandingBottom,
                EdgebandingLeft = singlePart.EdgebandingLeft,
                EdgebandingRight = singlePart.EdgebandingRight,
                WorkOrderId = workOrder.Id
            };
            
            workOrder.DetachedProducts.Add(detachedProduct);
            productsToRemove.Add(product);
            result.Statistics.ConvertedDetachedProducts++;
        }
        
        // Remove the original single-part products to avoid Entity Framework tracking conflicts
        foreach (var product in productsToRemove)
        {
            workOrder.Products.Remove(product);
        }
        
        if (singlePartProducts.Any())
        {
            _logger.LogInformation("Identified {Count} single-part products as detached products", singlePartProducts.Count);
        }
    }
}

public class SelectionValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}