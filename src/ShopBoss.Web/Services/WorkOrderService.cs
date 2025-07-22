using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class WorkOrderService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<WorkOrderService> _logger;

    public WorkOrderService(ShopBossDbContext context, ILogger<WorkOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WorkOrderManagementData> GetWorkOrderManagementDataAsync(string workOrderId)
    {
        try
        {
            // Use split queries to avoid cartesian product explosion
            var workOrder = await _context.WorkOrders
                .FirstOrDefaultAsync(w => w.Id == workOrderId);

            if (workOrder == null)
            {
                return new WorkOrderManagementData();
            }

            // Load entities separately to avoid massive joins
            var products = await _context.Products
                .Where(p => p.WorkOrderId == workOrderId)
                .ToListAsync();

            var parts = await _context.Parts
                .Where(p => p.Product.WorkOrderId == workOrderId)
                .Include(p => p.Product)
                .ToListAsync();

            var subassemblies = await _context.Subassemblies
                .Where(s => s.Product.WorkOrderId == workOrderId)
                .ToListAsync();

            var subassemblyParts = await _context.Parts
                .Where(p => p.Subassembly.Product.WorkOrderId == workOrderId)
                .Include(p => p.Subassembly)
                .ToListAsync();

            var hardware = await _context.Hardware
                .Where(h => h.WorkOrderId == workOrderId)
                .ToListAsync();

            var detachedProducts = await _context.DetachedProducts
                .Where(d => d.WorkOrderId == workOrderId)
                .ToListAsync();

            var nestSheets = await _context.NestSheets
                .Where(n => n.WorkOrderId == workOrderId)
                .ToListAsync();

            var nestSheetParts = await _context.Parts
                .Where(p => p.NestSheet.WorkOrderId == workOrderId)
                .Include(p => p.NestSheet)
                .ToListAsync();

            // Build object graph in memory (small dataset)
            var productNodes = BuildProductNodes(products, parts, subassemblies, subassemblyParts, hardware);

            // Calculate NestSheet summary
            var nestSheetSummary = new NestSheetSummary
            {
                TotalNestSheets = nestSheets.Count,
                ProcessedNestSheets = nestSheets.Count(n => n.Status == PartStatus.Cut),
                PendingNestSheets = nestSheets.Count(n => n.Status != PartStatus.Cut),
                TotalPartsOnNestSheets = nestSheetParts.Count
            };

            return new WorkOrderManagementData
            {
                WorkOrder = workOrder,
                ProductNodes = productNodes,
                AvailableStatuses = Enum.GetValues<PartStatus>().ToList(),
                NestSheets = nestSheets,
                NestSheetSummary = nestSheetSummary,
                DetachedProducts = detachedProducts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order management data for work order {WorkOrderId}", workOrderId);
            return new WorkOrderManagementData();
        }
    }

    public async Task<WorkOrder?> GetWorkOrderWithNestSheetsAsync(string workOrderId)
    {
        try
        {
            // Load work order with nest sheets first to avoid cartesian products
            var workOrder = await _context.WorkOrders
                .Include(w => w.NestSheets)
                .FirstOrDefaultAsync(w => w.Id == workOrderId);

            if (workOrder == null)
            {
                return null;
            }

            // Load parts for nest sheets in separate query
            var nestSheetIds = workOrder.NestSheets.Select(n => n.Id).ToList();
            var nestSheetsWithParts = await _context.NestSheets
                .Where(n => nestSheetIds.Contains(n.Id))
                .Include(n => n.Parts)
                .ToListAsync();

            // Update nest sheets with loaded parts
            foreach (var nestSheet in workOrder.NestSheets)
            {
                var nestSheetWithParts = nestSheetsWithParts.FirstOrDefault(n => n.Id == nestSheet.Id);
                if (nestSheetWithParts != null)
                {
                    nestSheet.Parts = nestSheetWithParts.Parts;
                }
            }

            return workOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order with nest sheets for work order {WorkOrderId}", workOrderId);
            return null;
        }
    }

    public async Task<List<WorkOrderSummary>> GetWorkOrderSummariesAsync(string searchTerm = "", bool includeArchived = false)
    {
        try
        {
            var baseQuery = _context.WorkOrders.AsQueryable();

            // Apply archive filter
            if (!includeArchived)
            {
                baseQuery = baseQuery.Where(w => !w.IsArchived);
            }

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(searchTerm))
            {
                baseQuery = baseQuery.Where(w => w.Name.Contains(searchTerm) || w.Id.Contains(searchTerm));
            }

            // Use optimized query with aggregations to avoid loading all related entities
            var summaries = await baseQuery
                .Select(w => new WorkOrderSummary
                {
                    Id = w.Id,
                    Name = w.Name,
                    ImportedDate = w.ImportedDate,
                    IsArchived = w.IsArchived,
                    ArchivedDate = w.ArchivedDate,
                    ProductsCount = w.Products.Count(),
                    HardwareCount = w.Hardware.Count(),
                    DetachedProductsCount = w.DetachedProducts.Count()
                })
                .OrderByDescending(w => w.ImportedDate)
                .ToListAsync();

            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order summaries with search term {SearchTerm}", searchTerm);
            return new List<WorkOrderSummary>();
        }
    }

    public async Task<WorkOrder?> GetWorkOrderByIdAsync(string workOrderId)
    {
        try
        {
            // For basic work order info, don't load all relationships to avoid performance issues
            return await _context.WorkOrders
                .FirstOrDefaultAsync(w => w.Id == workOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order by ID {WorkOrderId}", workOrderId);
            return null;
        }
    }

    public async Task<AssemblyStationData> GetAssemblyStationDataAsync(string workOrderId)
    {
        try
        {
            // Load work order basic info first
            var workOrder = await _context.WorkOrders
                .FirstOrDefaultAsync(w => w.Id == workOrderId);

            if (workOrder == null)
            {
                return new AssemblyStationData();
            }

            // Load products separately to avoid cartesian products
            var products = await _context.Products
                .Where(p => p.WorkOrderId == workOrderId)
                .ToListAsync();

            // Load all parts in a single query for this work order
            var allParts = await _context.Parts
                .Where(p => p.Product.WorkOrderId == workOrderId)
                .Select(p => new PartSummary
                {
                    Id = p.Id,
                    Name = p.Name,
                    ProductId = p.ProductId,
                    Status = p.Status,
                    Qty = p.Qty,
                    Location = p.Location,
                    Length = p.Length,
                    Width = p.Width,
                    Thickness = p.Thickness,
                    Material = p.Material,
                    Category = p.Category
                })
                .ToListAsync();

            // Load hardware separately
            var hardware = await _context.Hardware
                .Where(h => h.WorkOrderId == workOrderId)
                .ToListAsync();

            // Load detached products separately
            var detachedProducts = await _context.DetachedProducts
                .Where(d => d.WorkOrderId == workOrderId)
                .ToListAsync();

            return new AssemblyStationData
            {
                WorkOrder = workOrder,
                Products = products,
                Parts = allParts,
                Hardware = hardware,
                DetachedProducts = detachedProducts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assembly station data for work order {WorkOrderId}", workOrderId);
            return new AssemblyStationData();
        }
    }

    public async Task<ShippingStationData> GetShippingStationDataAsync(string workOrderId)
    {
        try
        {
            // Load work order basic info first
            var workOrder = await _context.WorkOrders
                .FirstOrDefaultAsync(w => w.Id == workOrderId);

            if (workOrder == null)
            {
                return new ShippingStationData();
            }

            // Load products separately to avoid cartesian products
            var products = await _context.Products
                .Where(p => p.WorkOrderId == workOrderId)
                .ToListAsync();

            // Load all parts with status counts in a single optimized query
            var partStatusSummaries = await _context.Parts
                .Where(p => p.Product.WorkOrderId == workOrderId)
                .GroupBy(p => new { p.ProductId, p.Status })
                .Select(g => new PartStatusSummary
                {
                    ProductId = g.Key.ProductId,
                    Status = g.Key.Status,
                    Count = g.Count()
                })
                .ToListAsync();

            // Load hardware separately
            var hardware = await _context.Hardware
                .Where(h => h.WorkOrderId == workOrderId)
                .ToListAsync();

            // Load detached products separately
            var detachedProducts = await _context.DetachedProducts
                .Where(d => d.WorkOrderId == workOrderId)
                .ToListAsync();

            return new ShippingStationData
            {
                WorkOrder = workOrder,
                Products = products,
                PartStatusSummaries = partStatusSummaries,
                Hardware = hardware,
                DetachedProducts = detachedProducts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping station data for work order {WorkOrderId}", workOrderId);
            return new ShippingStationData();
        }
    }


    public async Task<bool> ArchiveWorkOrderAsync(string workOrderId)
    {
        try
        {
            var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
            if (workOrder == null)
            {
                _logger.LogWarning("Attempted to archive non-existent work order {WorkOrderId}", workOrderId);
                return false;
            }

            if (workOrder.IsArchived)
            {
                _logger.LogWarning("Attempted to archive already archived work order {WorkOrderId}", workOrderId);
                return false;
            }

            workOrder.IsArchived = true;
            workOrder.ArchivedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Work order {WorkOrderId} archived successfully", workOrderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving work order {WorkOrderId}", workOrderId);
            return false;
        }
    }

    public async Task<bool> UnarchiveWorkOrderAsync(string workOrderId)
    {
        try
        {
            var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
            if (workOrder == null)
            {
                _logger.LogWarning("Attempted to unarchive non-existent work order {WorkOrderId}", workOrderId);
                return false;
            }

            if (!workOrder.IsArchived)
            {
                _logger.LogWarning("Attempted to unarchive non-archived work order {WorkOrderId}", workOrderId);
                return false;
            }

            workOrder.IsArchived = false;
            workOrder.ArchivedDate = null;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Work order {WorkOrderId} unarchived successfully", workOrderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unarchiving work order {WorkOrderId}", workOrderId);
            return false;
        }
    }

    public async Task<bool> IsWorkOrderActiveAsync(string workOrderId)
    {
        try
        {
            // A work order is considered active if it has any parts that are not in their final state
            var hasActiveParts = await _context.Parts
                .Where(p => p.Product.WorkOrderId == workOrderId && p.Status != PartStatus.Shipped)
                .AnyAsync();

            var hasActiveDetachedProducts = await _context.DetachedProducts
                .Where(d => d.WorkOrderId == workOrderId && d.Status != PartStatus.Shipped)
                .AnyAsync();

            return hasActiveParts || hasActiveDetachedProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if work order {WorkOrderId} is active", workOrderId);
            return true; // Default to active if there's an error to prevent accidental archiving
        }
    }

    private List<ProductStatusNode> BuildProductNodes(List<Product> products, List<Part> parts, List<Subassembly> subassemblies, List<Part> subassemblyParts, List<Hardware> hardware)
    {
        var productNodes = new List<ProductStatusNode>();

        foreach (var product in products)
        {
            // Get parts for this product
            var productParts = parts.Where(p => p.ProductId == product.Id).ToList();
            
            // Get subassemblies for this product
            var productSubassemblies = subassemblies.Where(s => s.ProductId == product.Id).ToList();
            
            // Get hardware for this product
            var productHardware = hardware.Where(h => h.ProductId == product.Id).ToList();
            
            // Attach parts to subassemblies
            foreach (var subassembly in productSubassemblies)
            {
                subassembly.Parts = subassemblyParts.Where(p => p.SubassemblyId == subassembly.Id).ToList();
            }

            productNodes.Add(new ProductStatusNode
            {
                Product = product,
                Parts = productParts,
                Subassemblies = productSubassemblies,
                Hardware = productHardware
            });
        }

        return productNodes;
    }

}

public class WorkOrderManagementData
{
    public WorkOrder WorkOrder { get; set; } = null!;
    public List<ProductStatusNode> ProductNodes { get; set; } = new();
    public List<PartStatus> AvailableStatuses { get; set; } = new();
    public List<NestSheet> NestSheets { get; set; } = new();
    public NestSheetSummary NestSheetSummary { get; set; } = new();
    public List<DetachedProduct> DetachedProducts { get; set; } = new();
}

public class NestSheetSummary
{
    public int TotalNestSheets { get; set; }
    public int ProcessedNestSheets { get; set; }
    public int PendingNestSheets { get; set; }
    public int TotalPartsOnNestSheets { get; set; }
}

public class ProductStatusNode
{
    public Product Product { get; set; } = null!;
    public List<Part> Parts { get; set; } = new();
    public List<Subassembly> Subassemblies { get; set; } = new();
    public List<Hardware> Hardware { get; set; } = new();
}

public class WorkOrderSummary
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime ImportedDate { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedDate { get; set; }
    public int ProductsCount { get; set; }
    public int HardwareCount { get; set; }
    public int DetachedProductsCount { get; set; }
}

public class AssemblyStationData
{
    public WorkOrder WorkOrder { get; set; } = null!;
    public List<Product> Products { get; set; } = new();
    public List<PartSummary> Parts { get; set; } = new();
    public List<Hardware> Hardware { get; set; } = new();
    public List<DetachedProduct> DetachedProducts { get; set; } = new();
}

public class PartSummary
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? ProductId { get; set; }
    public PartStatus Status { get; set; }
    public int Qty { get; set; }
    public string? Location { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public string Material { get; set; } = string.Empty;
    public PartCategory Category { get; set; } = PartCategory.Standard;
}

public class ShippingStationData
{
    public WorkOrder WorkOrder { get; set; } = null!;
    public List<Product> Products { get; set; } = new();
    public List<PartStatusSummary> PartStatusSummaries { get; set; } = new();
    public List<Hardware> Hardware { get; set; } = new();
    public List<DetachedProduct> DetachedProducts { get; set; } = new();
}

public class PartStatusSummary
{
    public string? ProductId { get; set; }
    public PartStatus Status { get; set; }
    public int Count { get; set; }
}

