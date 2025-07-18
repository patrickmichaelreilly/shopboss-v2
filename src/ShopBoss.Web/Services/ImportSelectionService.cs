using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Models.Import;
using Microsoft.EntityFrameworkCore;

namespace ShopBoss.Web.Services;

public class ImportSelectionService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<ImportSelectionService> _logger;
    private readonly PartFilteringService _partFilteringService;

    public ImportSelectionService(ShopBossDbContext context, ILogger<ImportSelectionService> logger, PartFilteringService partFilteringService)
    {
        _context = context;
        _logger = logger;
        _partFilteringService = partFilteringService;
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

            // Check for duplicate work order and handle gracefully
            var duplicateCheck = await CheckForDuplicateWorkOrder(importData.Id, selection.WorkOrderName);
            if (!duplicateCheck.IsValid && !selection.AllowDuplicates)
            {
                // Return duplicate info for user decision
                result.DuplicateInfo = duplicateCheck.DuplicateInfo;
                result.Errors.AddRange(duplicateCheck.Errors);
                return result;
            }

            // Create the work order entity (with unique ID if allowing duplicates)
            var workOrder = CreateWorkOrderEntity(importData, selection, duplicateCheck.DuplicateInfo);
            
            // Process nest sheets first (they're needed for parts)
            ProcessSelectedNestSheets(importData, selection, workOrder, result);
            
            // Process selected items
            await ProcessSelectedProductsAsync(importData, selection, workOrder, result);
            await ProcessSelectedDetachedProductsAsync(importData, selection, workOrder, result);

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
        var existingByName = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Name == workOrderName);

        if (existingById != null || existingByName != null)
        {
            result.IsValid = false;
            
            // Generate unique suggestions
            var suggestedId = await GenerateUniqueWorkOrderId(workOrderId);
            var suggestedName = await GenerateUniqueWorkOrderName(workOrderName);
            
            result.DuplicateInfo = new DuplicateDetectionResult
            {
                HasDuplicates = true,
                DuplicateWorkOrderId = existingById?.Id,
                DuplicateWorkOrderName = existingByName?.Name,
                ExistingImportDate = existingById?.ImportedDate ?? existingByName?.ImportedDate,
                SuggestedNewId = suggestedId,
                SuggestedNewName = suggestedName
            };

            if (existingById != null)
            {
                result.Errors.Add($"Work order with Microvellum ID '{workOrderId}' already exists (imported as '{existingById.Name}' on {existingById.ImportedDate:yyyy-MM-dd})");
                result.DuplicateInfo.ConflictMessages.Add($"ID conflict: '{workOrderId}' exists");
            }

            if (existingByName != null)
            {
                result.Errors.Add($"Work order with name '{workOrderName}' already exists (Microvellum ID: {existingByName.Id}, imported on {existingByName.ImportedDate:yyyy-MM-dd})");
                result.DuplicateInfo.ConflictMessages.Add($"Name conflict: '{workOrderName}' exists");
            }
        }

        return result;
    }

    private async Task<string> GenerateUniqueWorkOrderId(string baseId)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var suggestedId = $"{baseId}_{timestamp}";
        
        // Ensure it's truly unique
        var existing = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Id == suggestedId);
        if (existing != null)
        {
            suggestedId = $"{baseId}_{timestamp}_{Guid.NewGuid().ToString("N")[..6]}";
        }
        
        return suggestedId;
    }

    private async Task<string> GenerateUniqueWorkOrderName(string baseName)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var suggestedName = $"{baseName} (Reimported {timestamp})";
        
        // Ensure it's truly unique
        var existing = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Name == suggestedName);
        if (existing != null)
        {
            suggestedName = $"{baseName} (Reimported {timestamp} - {Guid.NewGuid().ToString("N")[..6]})";
        }
        
        return suggestedName;
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

    private WorkOrder CreateWorkOrderEntity(ImportWorkOrder importData, SelectionRequest selection, DuplicateDetectionResult? duplicateInfo = null)
    {
        // Use suggested unique values if allowing duplicates
        var workOrderId = importData.Id;
        var workOrderName = selection.WorkOrderName;
        
        if (selection.AllowDuplicates && duplicateInfo?.HasDuplicates == true)
        {
            workOrderId = duplicateInfo.SuggestedNewId;
            workOrderName = duplicateInfo.SuggestedNewName;
            
            _logger.LogInformation("Creating work order with unique identifiers due to duplicates: ID '{OriginalId}' -> '{NewId}', Name '{OriginalName}' -> '{NewName}'",
                importData.Id, workOrderId, selection.WorkOrderName, workOrderName);
        }
        
        return new WorkOrder
        {
            Id = workOrderId,
            Name = workOrderName,
            ImportedDate = DateTime.Now
        };
    }

    private async Task ProcessSelectedProductsAsync(
        ImportWorkOrder importData, 
        SelectionRequest selection, 
        WorkOrder workOrder, 
        ImportConversionResult result)
    {
        // Phase 1: Normalize products into individual instances
        var normalizedProducts = await NormalizeProductQuantitiesAsync(importData, selection, workOrder);
        
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

    private async Task<List<Product>> NormalizeProductQuantitiesAsync(
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
                var product = await ConvertToProductEntityAsync(importProduct, workOrder.Id);
                
                // Make each product instance unique if quantity > 1
                if (productQuantity > 1)
                {
                    product.Id = $"{product.Id}_{i}"; // Use the already unique ID as base
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
                ProductId = product.Id,
                Status = PartStatus.Pending,
                StatusUpdatedDate = DateTime.UtcNow
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
                WorkOrderId = subassembly.ProductId,
                Status = PartStatus.Pending,
                StatusUpdatedDate = DateTime.UtcNow
            };
            
            workOrder.Hardware.Add(hardware);
            result.Statistics.ConvertedHardware++;
        }
    }


    private async Task ProcessSelectedDetachedProductsAsync(
        ImportWorkOrder importData,
        SelectionRequest selection,
        WorkOrder workOrder,
        ImportConversionResult result)
    {
        var selectedDetachedIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "detached_product")
            .ToHashSet();

        foreach (var importDetached in importData.DetachedProducts.Where(d => selectedDetachedIds.Contains(d.Id)))
        {
            // Create DetachedProduct entity (existing functionality)
            var detached = await ConvertToDetachedProductEntityAsync(importDetached, workOrder.Id);
            workOrder.DetachedProducts.Add(detached);
            result.Statistics.ConvertedDetachedProducts++;
            
            // ALSO create Part entity for DetachedProduct so it can go through CNC → Sorting workflow
            var detachedPart = await CreateDetachedProductPartAsync(importDetached, detached, workOrder);
            _context.Parts.Add(detachedPart);
            result.Statistics.ConvertedParts++;
            
            _logger.LogInformation("Created DetachedProduct '{DetachedProductId}' with associated Part '{PartId}'", 
                detached.Id, detachedPart.Id);
        }
        
        if (selectedDetachedIds.Any())
        {
            _logger.LogInformation("Imported {Count} selected detached products with {Count} associated parts", 
                selectedDetachedIds.Count, selectedDetachedIds.Count);
        }
    }

    private async Task<Product> ConvertToProductEntityAsync(ImportProduct importProduct, string workOrderId)
    {
        var uniqueId = await EnsureUniqueIdAsync("Products", importProduct.Id);
        
        return new Product
        {
            Id = uniqueId, // Use unique ID (_1, _2, _3, etc. if duplicates exist)
            ProductNumber = importProduct.ProductNumber,
            Name = importProduct.Name,
            Qty = importProduct.Quantity,
            Length = importProduct.Height, // Height maps to Length
            Width = importProduct.Width,
            WorkOrderId = workOrderId,
            Status = PartStatus.Pending,
            StatusUpdatedDate = DateTime.UtcNow
        };
    }

    private async Task<string> EnsureUniqueIdAsync(string tableName, string originalId)
    {
        var counter = 1;
        var testId = originalId;
        
        while (await IdExistsInDatabaseAsync(tableName, testId))
        {
            testId = $"{originalId}_{counter}";
            counter++;
        }
        
        return testId;
    }

    private async Task<bool> IdExistsInDatabaseAsync(string tableName, string id)
    {
        return tableName switch
        {
            "Products" => await _context.Products.AnyAsync(p => p.Id == id),
            "Parts" => await _context.Parts.AnyAsync(p => p.Id == id),
            "Subassemblies" => await _context.Subassemblies.AnyAsync(s => s.Id == id),
            "DetachedProducts" => await _context.DetachedProducts.AnyAsync(d => d.Id == id),
            "NestSheets" => await _context.NestSheets.AnyAsync(n => n.Id == id),
            "Hardware" => await _context.Hardware.AnyAsync(h => h.Id == id),
            _ => false
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
        
        var part = new Part
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
            Status = PartStatus.Pending, // Set initial status
            StatusUpdatedDate = DateTime.UtcNow // Fix NOT NULL constraint
        };

        // Classify part and store category during import
        part.Category = _partFilteringService.ClassifyPart(part);
        
        return part;
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
            WorkOrderId = workOrderId,
            Status = PartStatus.Pending,
            StatusUpdatedDate = DateTime.UtcNow
        };
    }

    private async Task<DetachedProduct> ConvertToDetachedProductEntityAsync(ImportDetachedProduct importDetached, string workOrderId)
    {
        var uniqueId = await EnsureUniqueIdAsync("DetachedProducts", importDetached.Id);
        
        return new DetachedProduct
        {
            Id = uniqueId, // Use unique ID (_1, _2, _3, etc. if duplicates exist)
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
            WorkOrderId = workOrderId,
            Status = PartStatus.Pending,
            StatusUpdatedDate = DateTime.UtcNow
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
            Status = PartStatus.Pending,
            StatusUpdatedDate = DateTime.UtcNow
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
            Status = PartStatus.Pending,
            StatusUpdatedDate = DateTime.UtcNow
        };

        workOrder.NestSheets.Add(defaultNestSheet);
        return defaultNestSheet;
    }

    private async Task<Part> CreateDetachedProductPartAsync(ImportDetachedProduct importDetached, DetachedProduct detachedProduct, WorkOrder workOrder)
    {
        // Create a Part entity for the DetachedProduct so it can go through CNC → Sorting workflow
        // Use the original part ID from the SDF data for barcode scanning compatibility
        
        // Use OriginalPartId which contains the correct Part LinkID (barcode) from SDF data
        var partId = await EnsureUniqueIdAsync("Parts", importDetached.OriginalPartId);
        
        // Find the nest sheet for this DetachedProduct part (same logic as regular parts)
        var nestSheet = FindNestSheetForDetachedProduct(importDetached, workOrder);
        
        // Ensure we have a valid NestSheetId - use first available or create default
        var nestSheetId = nestSheet?.Id;
        if (string.IsNullOrEmpty(nestSheetId))
        {
            // Use first available nest sheet from work order, or create default
            var existingNestSheet = workOrder.NestSheets.FirstOrDefault();
            if (existingNestSheet != null)
            {
                nestSheetId = existingNestSheet.Id;
            }
            else
            {
                var defaultNestSheet = CreateDefaultNestSheet(workOrder);
                nestSheetId = defaultNestSheet.Id;
            }
        }

        var part = new Part
        {
            Id = partId,
            Name = detachedProduct.Name,
            Qty = detachedProduct.Qty,
            Length = detachedProduct.Length,
            Width = detachedProduct.Width,
            Thickness = detachedProduct.Thickness,
            Material = detachedProduct.Material,
            EdgebandingTop = detachedProduct.EdgebandingTop,
            EdgebandingBottom = detachedProduct.EdgebandingBottom,
            EdgebandingLeft = detachedProduct.EdgebandingLeft,
            EdgebandingRight = detachedProduct.EdgebandingRight,
            ProductId = detachedProduct.Id, // Link Part to DetachedProduct via ProductId (no FK constraint)
            SubassemblyId = null, // DetachedProducts don't have subassemblies
            NestSheetId = nestSheetId,
            Status = PartStatus.Pending, // Set initial status
            StatusUpdatedDate = DateTime.UtcNow // Fix NOT NULL constraint
        };

        // Classify part and store category during import
        part.Category = _partFilteringService.ClassifyPart(part);
        
        return part;
    }

    private NestSheet? FindNestSheetForDetachedProduct(ImportDetachedProduct importDetached, WorkOrder workOrder)
    {
        // Try to find by nest sheet name first
        if (!string.IsNullOrEmpty(importDetached.NestSheetName))
        {
            return workOrder.NestSheets.FirstOrDefault(n => n.Name == importDetached.NestSheetName);
        }

        // If no specific nest sheet name, try to find by nest sheet ID
        if (!string.IsNullOrEmpty(importDetached.NestSheetId))
        {
            return workOrder.NestSheets.FirstOrDefault(n => n.Id == importDetached.NestSheetId);
        }

        return null;
    }

    /// <summary>
    /// Phase I4: Convert WorkOrder entities directly to database
    /// This is adapted from ConvertSelectedItemsAsync but works with already-created WorkOrder entities
    /// </summary>
    public async Task<ImportConversionResult> ConvertWorkOrderToDatabaseAsync(
        WorkOrder workOrder, 
        string workOrderName)
    {
        var result = new ImportConversionResult();
        
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            _logger.LogInformation("Phase I4: Starting conversion of WorkOrder entities to database: {WorkOrderName}", workOrderName);

            // Check for duplicate work order and automatically handle with unique identifiers
            var duplicateCheck = await CheckForDuplicateWorkOrderDirect(workOrder.Id, workOrderName);
            
            // Always automatically resolve duplicates - no user interaction needed
            if (duplicateCheck.DuplicateInfo?.HasDuplicates == true)
            {
                UpdateWorkOrderForDuplicates(workOrder, workOrderName, duplicateCheck.DuplicateInfo);
            }
            else
            {
                // Just update the name if no duplicates
                workOrder.Name = workOrderName;
            }

            // Set import date
            workOrder.ImportedDate = DateTime.Now;

            // Process all entities in the WorkOrder
            await ProcessWorkOrderEntitiesAsync(workOrder, result);

            // Save to database
            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();
            
            // Commit transaction
            await transaction.CommitAsync();

            result.Success = true;
            result.WorkOrderId = workOrder.Id;
            
            _logger.LogInformation("Phase I4: Successfully saved WorkOrder {WorkOrderId} with {ProductCount} products, {PartCount} parts, {SubassemblyCount} subassemblies, {HardwareCount} hardware items",
                workOrder.Id,
                result.Statistics.ConvertedProducts,
                result.Statistics.ConvertedParts,
                result.Statistics.ConvertedSubassemblies,
                result.Statistics.ConvertedHardware);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase I4: Error converting WorkOrder entities to database. Rolling back transaction.");
            await transaction.RollbackAsync();
            result.Errors.Add($"Import error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Phase I4: Check for duplicate WorkOrder without ImportWorkOrder dependency
    /// </summary>
    private async Task<SelectionValidationResult> CheckForDuplicateWorkOrderDirect(string workOrderId, string workOrderName)
    {
        var result = new SelectionValidationResult { IsValid = true };

        // Check for duplicate Microvellum ID
        var existingById = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Id == workOrderId);
        var existingByName = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Name == workOrderName);

        if (existingById != null || existingByName != null)
        {
            result.IsValid = false;
            
            // Generate unique suggestions
            var suggestedId = await GenerateUniqueWorkOrderId(workOrderId);
            var suggestedName = await GenerateUniqueWorkOrderName(workOrderName);
            
            result.DuplicateInfo = new DuplicateDetectionResult
            {
                HasDuplicates = true,
                DuplicateWorkOrderId = existingById?.Id,
                DuplicateWorkOrderName = existingByName?.Name,
                ExistingImportDate = existingById?.ImportedDate ?? existingByName?.ImportedDate,
                SuggestedNewId = suggestedId,
                SuggestedNewName = suggestedName
            };

            if (existingById != null)
            {
                result.Errors.Add($"Work order with Microvellum ID '{workOrderId}' already exists (imported as '{existingById.Name}' on {existingById.ImportedDate:yyyy-MM-dd})");
                result.DuplicateInfo.ConflictMessages.Add($"ID conflict: '{workOrderId}' exists");
            }

            if (existingByName != null)
            {
                result.Errors.Add($"Work order with name '{workOrderName}' already exists (Microvellum ID: {existingByName.Id}, imported on {existingByName.ImportedDate:yyyy-MM-dd})");
                result.DuplicateInfo.ConflictMessages.Add($"Name conflict: '{workOrderName}' exists");
            }
        }

        return result;
    }

    /// <summary>
    /// Phase I4: Update WorkOrder identifiers for duplicates
    /// </summary>
    private void UpdateWorkOrderForDuplicates(WorkOrder workOrder, string workOrderName, DuplicateDetectionResult duplicateInfo)
    {
        var originalId = workOrder.Id;
        var originalName = workOrder.Name;
        
        // Update work order identifiers
        workOrder.Id = duplicateInfo.SuggestedNewId;
        workOrder.Name = duplicateInfo.SuggestedNewName;
        
        _logger.LogInformation("Phase I4: Updated WorkOrder identifiers due to duplicates: ID '{OriginalId}' -> '{NewId}', Name '{OriginalName}' -> '{NewName}'",
            originalId, workOrder.Id, originalName, workOrder.Name);

        // Update all related entity IDs that reference the work order
        UpdateRelatedEntityIds(workOrder, originalId, workOrder.Id);
    }

    /// <summary>
    /// Phase I4: Update related entity IDs when WorkOrder ID changes
    /// </summary>
    private void UpdateRelatedEntityIds(WorkOrder workOrder, string oldWorkOrderId, string newWorkOrderId)
    {
        // Update all Product WorkOrderIds
        foreach (var product in workOrder.Products)
        {
            product.WorkOrderId = newWorkOrderId;
            
            // Update all Part WorkOrderIds through navigation
            foreach (var part in product.Parts)
            {
                // Parts don't have WorkOrderId, they're linked through ProductId
            }
        }

        // Update DetachedProduct WorkOrderIds
        foreach (var detachedProduct in workOrder.DetachedProducts)
        {
            detachedProduct.WorkOrderId = newWorkOrderId;
        }

        // Update Hardware WorkOrderIds
        foreach (var hardware in workOrder.Hardware)
        {
            hardware.WorkOrderId = newWorkOrderId;
        }

        // Update NestSheet WorkOrderIds
        foreach (var nestSheet in workOrder.NestSheets)
        {
            nestSheet.WorkOrderId = newWorkOrderId;
        }
    }

    /// <summary>
    /// Phase I4: Process all entities in the WorkOrder for database persistence
    /// </summary>
    private async Task ProcessWorkOrderEntitiesAsync(WorkOrder workOrder, ImportConversionResult result)
    {
        // Process Products
        foreach (var product in workOrder.Products)
        {
            // Ensure unique product ID
            product.Id = await EnsureUniqueIdAsync("Products", product.Id);
            
            // Process Parts within Product
            foreach (var part in product.Parts)
            {
                part.Id = await EnsureUniqueIdAsync("Parts", part.Id);
                part.ProductId = product.Id; // Update reference
                
                // Classify part if not already classified
                if (part.Category == 0)
                {
                    part.Category = _partFilteringService.ClassifyPart(part);
                }
            }

            // Process Subassemblies within Product
            foreach (var subassembly in product.Subassemblies)
            {
                subassembly.Id = await EnsureUniqueIdAsync("Subassemblies", subassembly.Id);
                subassembly.ProductId = product.Id; // Update reference
                
                // Process Parts within Subassembly
                foreach (var part in subassembly.Parts)
                {
                    part.Id = await EnsureUniqueIdAsync("Parts", part.Id);
                    part.ProductId = product.Id; // Update reference
                    part.SubassemblyId = subassembly.Id; // Update reference
                    
                    // Classify part if not already classified
                    if (part.Category == 0)
                    {
                        part.Category = _partFilteringService.ClassifyPart(part);
                    }
                }
            }

            // Process Hardware within Product
            foreach (var hardware in product.Hardware)
            {
                hardware.Id = await EnsureUniqueIdAsync("Hardware", hardware.Id);
                hardware.ProductId = product.Id; // Update reference
                hardware.WorkOrderId = workOrder.Id; // Update reference
            }
            
            result.Statistics.ConvertedProducts++;
        }

        // Process DetachedProducts
        foreach (var detachedProduct in workOrder.DetachedProducts)
        {
            detachedProduct.Id = await EnsureUniqueIdAsync("DetachedProducts", detachedProduct.Id);
            detachedProduct.WorkOrderId = workOrder.Id; // Update reference
            
            result.Statistics.ConvertedDetachedProducts++;
        }

        // Process Hardware (standalone)
        foreach (var hardware in workOrder.Hardware)
        {
            hardware.Id = await EnsureUniqueIdAsync("Hardware", hardware.Id);
            hardware.WorkOrderId = workOrder.Id; // Update reference
            result.Statistics.ConvertedHardware++;
        }

        // Process NestSheets
        foreach (var nestSheet in workOrder.NestSheets)
        {
            nestSheet.Id = await EnsureUniqueIdAsync("NestSheets", nestSheet.Id);
            nestSheet.WorkOrderId = workOrder.Id; // Update reference
            
            // Process Parts within NestSheet
            foreach (var part in nestSheet.Parts)
            {
                part.Id = await EnsureUniqueIdAsync("Parts", part.Id);
                part.NestSheetId = nestSheet.Id; // Update reference
                
                // Classify part if not already classified
                if (part.Category == 0)
                {
                    part.Category = _partFilteringService.ClassifyPart(part);
                }
            }
            
            result.Statistics.ConvertedNestSheets++;
        }

        // Count total parts and subassemblies
        result.Statistics.ConvertedParts = workOrder.Products.SelectMany(p => p.Parts).Count() +
                                          workOrder.Products.SelectMany(p => p.Subassemblies).SelectMany(s => s.Parts).Count() +
                                          workOrder.NestSheets.SelectMany(n => n.Parts).Count();
        
        result.Statistics.ConvertedSubassemblies = workOrder.Products.SelectMany(p => p.Subassemblies).Count();
    }

}

public class SelectionValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public DuplicateDetectionResult? DuplicateInfo { get; set; }
}