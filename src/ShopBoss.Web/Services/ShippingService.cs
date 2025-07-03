using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class ShippingService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<ShippingService> _logger;

    public ShippingService(ShopBossDbContext context, ILogger<ShippingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<string>> GetProductsReadyForShippingAsync(string workOrderId)
    {
        try
        {
            var products = await _context.Products
                .Include(p => p.Parts)
                .Where(p => p.WorkOrderId == workOrderId)
                .ToListAsync();

            var readyProducts = new List<string>();

            foreach (var product in products)
            {
                // A product is ready for shipping if all its parts are assembled
                var allPartsAssembled = product.Parts.All(p => p.Status == PartStatus.Assembled);
                
                if (allPartsAssembled && product.Parts.Any())
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

    public async Task<ShippingDashboardData> GetShippingDashboardDataAsync(string workOrderId)
    {
        try
        {
            var workOrder = await _context.WorkOrders
                .Include(w => w.Products)
                    .ThenInclude(p => p.Parts)
                .Include(w => w.Hardware)
                .Include(w => w.DetachedProducts)
                .FirstOrDefaultAsync(w => w.Id == workOrderId);

            if (workOrder == null)
            {
                return new ShippingDashboardData();
            }

            var readyProductIds = await GetProductsReadyForShippingAsync(workOrderId);

            return new ShippingDashboardData
            {
                WorkOrder = workOrder,
                ReadyProductIds = readyProductIds,
                Products = workOrder.Products.Select(p => new ProductShippingStatus
                {
                    Product = p,
                    IsReadyForShipping = readyProductIds.Contains(p.Id),
                    IsShipped = p.Parts.Any() && p.Parts.All(part => part.Status == PartStatus.Shipped),
                    AssembledPartsCount = p.Parts.Count(part => part.Status == PartStatus.Assembled),
                    ShippedPartsCount = p.Parts.Count(part => part.Status == PartStatus.Shipped),
                    TotalPartsCount = p.Parts.Count
                }).ToList(),
                Hardware = workOrder.Hardware,
                DetachedProducts = workOrder.DetachedProducts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping dashboard data for work order {WorkOrderId}", workOrderId);
            return new ShippingDashboardData();
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
                    part.StatusUpdatedDate = DateTime.UtcNow;
                }
            }

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
            var workOrder = await _context.WorkOrders
                .Include(w => w.Products)
                    .ThenInclude(p => p.Parts)
                .FirstOrDefaultAsync(w => w.Id == workOrderId);

            if (workOrder == null)
            {
                return false;
            }

            // Work order is ready for shipping if all products have all their parts assembled
            return workOrder.Products.All(product => 
                product.Parts.Any() && product.Parts.All(part => part.Status == PartStatus.Assembled));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if work order {WorkOrderId} is ready for shipping", workOrderId);
            return false;
        }
    }
}

// Data models for shipping dashboard
public class ShippingDashboardData
{
    public WorkOrder WorkOrder { get; set; } = null!;
    public List<string> ReadyProductIds { get; set; } = new();
    public List<ProductShippingStatus> Products { get; set; } = new();
    public List<Hardware> Hardware { get; set; } = new();
    public List<DetachedProduct> DetachedProducts { get; set; } = new();
}

public class ProductShippingStatus
{
    public Product Product { get; set; } = null!;
    public bool IsReadyForShipping { get; set; }
    public bool IsShipped { get; set; }
    public int AssembledPartsCount { get; set; }
    public int ShippedPartsCount { get; set; }
    public int TotalPartsCount { get; set; }
}