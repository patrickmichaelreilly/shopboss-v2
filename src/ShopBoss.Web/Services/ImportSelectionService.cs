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
            
            // Track processed hardware to avoid duplicates
            var processedHardwareIds = new HashSet<string>();
            
            // Process selected items
            ProcessSelectedProducts(importData, selection, workOrder, processedHardwareIds, result);
            ProcessSelectedHardware(importData, selection, workOrder, processedHardwareIds, result);
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
        HashSet<string> processedHardwareIds,
        ImportConversionResult result)
    {
        var selectedProductIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "product")
            .ToHashSet();

        foreach (var importProduct in importData.Products.Where(p => selectedProductIds.Contains(p.Id)))
        {
            var product = ConvertToProductEntity(importProduct, workOrder.Id);
            workOrder.Products.Add(product);

            // Process selected parts for this product
            ProcessSelectedPartsForProduct(importProduct, selection, product, result);
            
            // Process selected subassemblies for this product
            ProcessSelectedSubassembliesForProduct(importProduct, selection, product, result);
            
            // Process selected hardware for this product
            ProcessSelectedHardwareForProduct(importProduct, selection, product, workOrder, processedHardwareIds, result);

            result.Statistics.ConvertedProducts++;
        }
    }

    private void ProcessSelectedPartsForProduct(
        ImportProduct importProduct,
        SelectionRequest selection,
        Product product,
        ImportConversionResult result)
    {
        var selectedPartIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "part")
            .ToHashSet();

        foreach (var importPart in importProduct.Parts.Where(p => selectedPartIds.Contains(p.Id)))
        {
            var part = ConvertToPartEntity(importPart, product.Id, null);
            product.Parts.Add(part);
            result.Statistics.ConvertedParts++;
        }
    }

    private void ProcessSelectedSubassembliesForProduct(
        ImportProduct importProduct,
        SelectionRequest selection,
        Product product,
        ImportConversionResult result)
    {
        var selectedSubassemblyIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "subassembly")
            .ToHashSet();

        foreach (var importSubassembly in importProduct.Subassemblies.Where(s => selectedSubassemblyIds.Contains(s.Id)))
        {
            var subassembly = ConvertToSubassemblyEntity(importSubassembly, product.Id, null);
            product.Subassemblies.Add(subassembly);

            // Recursively process selected items within this subassembly
            ProcessSelectedItemsInSubassembly(importSubassembly, selection, subassembly, result);

            result.Statistics.ConvertedSubassemblies++;
        }
    }

    private void ProcessSelectedHardwareForProduct(
        ImportProduct importProduct,
        SelectionRequest selection,
        Product product,
        WorkOrder workOrder,
        HashSet<string> processedHardwareIds,
        ImportConversionResult result)
    {
        var selectedHardwareIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "hardware")
            .ToHashSet();

        foreach (var importHardware in importProduct.Hardware.Where(h => selectedHardwareIds.Contains(h.Id)))
        {
            // Skip if this hardware has already been processed
            if (processedHardwareIds.Contains(importHardware.Id))
            {
                continue;
            }
            
            // Hardware can be associated with either work order or product
            // For product-level hardware, we'll create it as work order hardware with reference
            var hardware = ConvertToHardwareEntity(importHardware, product.WorkOrderId);
            
            // Add to work order's hardware collection
            workOrder.Hardware.Add(hardware);
            processedHardwareIds.Add(importHardware.Id);
            result.Statistics.ConvertedHardware++;
        }
    }

    private void ProcessSelectedItemsInSubassembly(
        ImportSubassembly importSubassembly,
        SelectionRequest selection,
        Subassembly subassembly,
        ImportConversionResult result)
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
            var part = ConvertToPartEntity(importPart, subassembly.ProductId, subassembly.Id);
            subassembly.Parts.Add(part);
            result.Statistics.ConvertedParts++;
        }

        // Process nested subassemblies
        foreach (var importNested in importSubassembly.NestedSubassemblies.Where(s => selectedSubassemblyIds.Contains(s.Id)))
        {
            var nested = ConvertToSubassemblyEntity(importNested, subassembly.ProductId, subassembly.Id);
            subassembly.ChildSubassemblies.Add(nested);

            // Recursively process items in nested subassembly
            ProcessSelectedItemsInSubassembly(importNested, selection, nested, result);

            result.Statistics.ConvertedSubassemblies++;
        }
    }

    private void ProcessSelectedHardware(
        ImportWorkOrder importData,
        SelectionRequest selection,
        WorkOrder workOrder,
        HashSet<string> processedHardwareIds,
        ImportConversionResult result)
    {
        var selectedHardwareIds = selection.SelectedItemIds
            .Where(id => selection.SelectionDetails.ContainsKey(id) && 
                        selection.SelectionDetails[id].ItemType == "hardware")
            .ToHashSet();

        foreach (var importHardware in importData.Hardware.Where(h => selectedHardwareIds.Contains(h.Id)))
        {
            // Skip if this hardware has already been processed
            if (processedHardwareIds.Contains(importHardware.Id))
            {
                continue;
            }
            
            var hardware = ConvertToHardwareEntity(importHardware, workOrder.Id);
            workOrder.Hardware.Add(hardware);
            processedHardwareIds.Add(importHardware.Id);
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

    private Part ConvertToPartEntity(ImportPart importPart, string? productId, string? subassemblyId)
    {
        return new Part
        {
            Id = importPart.Id, // Preserve Microvellum ID
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
            SubassemblyId = subassemblyId
        };
    }

    private Subassembly ConvertToSubassemblyEntity(ImportSubassembly importSubassembly, string? productId, string? parentSubassemblyId)
    {
        return new Subassembly
        {
            Id = importSubassembly.Id, // Preserve Microvellum ID
            Name = importSubassembly.Name,
            Qty = importSubassembly.Quantity,
            Length = importSubassembly.Height, // Height maps to Length
            Width = importSubassembly.Width,
            ProductId = productId,
            ParentSubassemblyId = parentSubassemblyId
        };
    }

    private Hardware ConvertToHardwareEntity(ImportHardware importHardware, string workOrderId)
    {
        return new Hardware
        {
            Id = importHardware.Id, // Preserve Microvellum ID
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

    private void ProcessSinglePartProductsAsDetached(WorkOrder workOrder, ImportConversionResult result)
    {
        // Find products with exactly 1 part and treat them as detached products
        var singlePartProducts = workOrder.Products.Where(p => p.Parts.Count == 1).ToList();
        
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
            result.Statistics.ConvertedDetachedProducts++;
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