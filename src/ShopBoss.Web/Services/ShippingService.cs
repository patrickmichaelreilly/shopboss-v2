using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class ShippingService
{
    private readonly ShopBossDbContext _context;
    private readonly WorkOrderService _workOrderService;
    private readonly HardwareGroupingService _hardwareGroupingService;
    private readonly ILogger<ShippingService> _logger;

    public ShippingService(ShopBossDbContext context, WorkOrderService workOrderService, HardwareGroupingService hardwareGroupingService, ILogger<ShippingService> logger)
    {
        _context = context;
        _workOrderService = workOrderService;
        _hardwareGroupingService = hardwareGroupingService;
        _logger = logger;
    }

    public async Task<List<string>> GetProductsReadyForShippingAsync(string workOrderId)
    {
        try
        {
            // Get the shipping station data for this work order
            var shippingStationData = await _workOrderService.GetShippingStationDataAsync(workOrderId);
            
            if (shippingStationData.WorkOrder == null)
            {
                return new List<string>();
            }

            var readyProducts = new List<string>();

            foreach (var product in shippingStationData.Products)
            {
                // Get part status counts for this product
                var productPartStatuses = shippingStationData.PartStatusSummaries
                    .Where(pss => pss.ProductId == product.Id)
                    .ToList();

                // Check if product has parts and all are assembled
                var totalParts = productPartStatuses.Sum(pss => pss.Count);
                var assembledParts = productPartStatuses
                    .Where(pss => pss.Status == PartStatus.Assembled)
                    .Sum(pss => pss.Count);

                if (totalParts > 0 && assembledParts == totalParts)
                {
                    readyProducts.Add(product.Id);
                }
            }

            return readyProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products ready for shipping for work order {WorkOrderId}", workOrderId);
            return new List<string>();
        }
    }

    public async Task<List<string>> GetProductsShippedAsync(string workOrderId)
    {
        try
        {
            var shippedProducts = await _context.Products
                .Where(p => p.WorkOrderId == workOrderId && p.Status == PartStatus.Shipped)
                .Select(p => p.Id)
                .ToListAsync();

            return shippedProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipped products for work order {WorkOrderId}", workOrderId);
            return new List<string>();
        }
    }


    public async Task<bool> MarkProductAsShippedAsync(string productId, string workOrderId)
    {
        try
        {
            var product = await _context.Products
                .Include(p => p.Parts)
                .FirstOrDefaultAsync(p => p.Id == productId && p.WorkOrderId == workOrderId);

            if (product == null)
            {
                return false;
            }

            // Mark all parts as shipped
            foreach (var part in product.Parts)
            {
                if (part.Status == PartStatus.Assembled)
                {
                    part.Status = PartStatus.Shipped;
                    part.StatusUpdatedDate = DateTime.Now;
                }
            }

            // Mark the product itself as shipped
            product.Status = PartStatus.Shipped;
            product.StatusUpdatedDate = DateTime.Now;

            // Explicitly mark the product as modified to ensure EF tracks the change
            _context.Products.Update(product);

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking product {ProductId} as shipped", productId);
            return false;
        }
    }

    public async Task<bool> IsWorkOrderReadyForShippingAsync(string workOrderId)
    {
        try
        {
            // Use optimized query to check if all parts in work order are assembled
            var totalParts = await _context.Parts
                .Where(p => p.Product.WorkOrderId == workOrderId)
                .CountAsync();

            if (totalParts == 0)
            {
                return false;
            }

            var assembledParts = await _context.Parts
                .Where(p => p.Product.WorkOrderId == workOrderId && p.Status == PartStatus.Assembled)
                .CountAsync();

            return assembledParts == totalParts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if work order {WorkOrderId} is ready for shipping", workOrderId);
            return false;
        }
    }


    public async Task<bool> UpdatePartStatusAsync(string partId, PartStatus newStatus, string changedBy = "Manual")
    {
        try
        {
            var part = await _context.Parts.FirstOrDefaultAsync(p => p.Id == partId);
            if (part == null)
            {
                return false;
            }

            var oldStatus = part.Status;
            part.Status = newStatus;
            part.StatusUpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Part {PartId} status updated from {OldStatus} to {NewStatus} by {ChangedBy}", 
                partId, oldStatus, newStatus, changedBy);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating part {PartId} status to {NewStatus}", partId, newStatus);
            return false;
        }
    }

    public async Task<bool> UpdateProductStatusAsync(string productId, PartStatus newStatus, bool cascadeToParts = true)
    {
        try
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return false;
            }

            if (cascadeToParts)
            {
                // Update all parts for this product in optimized bulk operations
                var productParts = await _context.Parts
                    .Where(p => p.ProductId == productId)
                    .ToListAsync();

                var subassemblyParts = await _context.Parts
                    .Where(p => p.Subassembly.ProductId == productId)
                    .ToListAsync();

                // Update all direct parts
                foreach (var part in productParts)
                {
                    part.Status = newStatus;
                    part.StatusUpdatedDate = DateTime.Now;
                }

                // Update all subassembly parts
                foreach (var part in subassemblyParts)
                {
                    part.Status = newStatus;
                    part.StatusUpdatedDate = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Product {ProductId} status updated to {NewStatus} with cascade={CascadeToParts}", 
                productId, newStatus, cascadeToParts);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId} status to {NewStatus}", productId, newStatus);
            return false;
        }
    }

    public async Task<bool> UpdateHardwareStatusAsync(string hardwareId, bool isShipped)
    {
        try
        {
            var hardware = await _context.Hardware.FirstOrDefaultAsync(h => h.Id == hardwareId);
            if (hardware == null)
            {
                return false;
            }

            hardware.Status = isShipped ? PartStatus.Shipped : PartStatus.Pending;
            hardware.StatusUpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Hardware {HardwareId} shipped status updated to {IsShipped}", 
                hardwareId, isShipped);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hardware {HardwareId} shipped status", hardwareId);
            return false;
        }
    }

    public async Task<bool> UpdateDetachedProductStatusAsync(string detachedProductId, bool isShipped)
    {
        try
        {
            var detachedProduct = await _context.DetachedProducts.FirstOrDefaultAsync(d => d.Id == detachedProductId);
            if (detachedProduct == null)
            {
                return false;
            }

            detachedProduct.Status = isShipped ? PartStatus.Shipped : PartStatus.Pending;
            detachedProduct.StatusUpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Detached product {DetachedProductId} shipped status updated to {IsShipped}", 
                detachedProductId, isShipped);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating detached product {DetachedProductId} shipped status", detachedProductId);
            return false;
        }
    }

    public async Task<BulkUpdateResult> UpdateMultipleStatusesAsync(List<StatusUpdateRequest> updates)
    {
        var result = new BulkUpdateResult();
        
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            foreach (var update in updates)
            {
                bool success = false;
                
                switch (update.ItemType.ToLower())
                {
                    case "part":
                        success = await UpdatePartStatusAsync(update.ItemId, update.NewStatus, "Manual Bulk");
                        break;
                    case "product":
                        success = await UpdateProductStatusAsync(update.ItemId, update.NewStatus, update.CascadeToChildren);
                        break;
                    case "hardware":
                        success = await UpdateHardwareStatusAsync(update.ItemId, update.NewStatus == PartStatus.Shipped);
                        break;
                    case "detachedproduct":
                        success = await UpdateDetachedProductStatusAsync(update.ItemId, update.NewStatus == PartStatus.Shipped);
                        break;
                }
                
                if (success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailureCount++;
                    result.FailedItems.Add(update.ItemId);
                }
            }

            await transaction.CommitAsync();
            result.Success = true;
            
            _logger.LogInformation("Bulk update completed: {SuccessCount} successful, {FailureCount} failed", 
                result.SuccessCount, result.FailureCount);
                
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error during bulk status update");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

}

// Data models for shipping dashboard
public class ShippingDashboardData
{
    public WorkOrder WorkOrder { get; set; } = null!;
    public List<string> ReadyProductIds { get; set; } = new();
    public List<string> ShippedProductIds { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public List<HardwareShippingStatus> Hardware { get; set; } = new();
    public List<HardwareGroup> GroupedHardware { get; set; } = new();
    public List<DetachedProductShippingStatus> DetachedProducts { get; set; } = new();
}

public class HardwareShippingStatus
{
    public Hardware Hardware { get; set; } = null!;
    public bool IsShipped { get; set; } // Computed from Status for UI compatibility
}

public class DetachedProductShippingStatus
{
    public DetachedProduct DetachedProduct { get; set; } = null!;
    public bool IsShipped { get; set; } // Computed from Status for UI compatibility
}



public class StatusUpdateRequest
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty; // "Part", "Product", "Hardware", "DetachedProduct"
    public PartStatus NewStatus { get; set; }
    public bool CascadeToChildren { get; set; } = false;
}

public class BulkUpdateResult
{
    public bool Success { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> FailedItems { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
}