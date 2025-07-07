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
                ProcessedNestSheets = nestSheets.Count(n => n.IsProcessed),
                PendingNestSheets = nestSheets.Count(n => !n.IsProcessed),
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
            return await _context.WorkOrders
                .Include(w => w.NestSheets)
                    .ThenInclude(n => n.Parts)
                .FirstOrDefaultAsync(w => w.Id == workOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order with nest sheets for work order {WorkOrderId}", workOrderId);
            return null;
        }
    }

    public async Task<List<WorkOrder>> GetWorkOrderSummariesAsync(string searchTerm = "")
    {
        try
        {
            var query = _context.WorkOrders
                .Include(w => w.Products)
                .Include(w => w.Hardware)
                .Include(w => w.DetachedProducts)
                .AsQueryable();

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(w => w.Name.Contains(searchTerm) || w.Id.Contains(searchTerm));
            }

            return await query
                .OrderByDescending(w => w.ImportedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order summaries with search term {SearchTerm}", searchTerm);
            return new List<WorkOrder>();
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

            // Calculate effective status
            var allProductParts = productParts.ToList();
            allProductParts.AddRange(productSubassemblies.SelectMany(s => s.Parts));
            var effectiveStatus = CalculateEffectiveStatus(allProductParts);

            productNodes.Add(new ProductStatusNode
            {
                Product = product,
                Parts = productParts,
                Subassemblies = productSubassemblies,
                Hardware = productHardware,
                EffectiveStatus = effectiveStatus
            });
        }

        return productNodes;
    }

    private PartStatus CalculateEffectiveStatus(List<Part> parts)
    {
        if (!parts.Any()) return PartStatus.Pending;

        // If all parts have the same status, return that status
        var distinctStatuses = parts.Select(p => p.Status).Distinct().ToList();
        if (distinctStatuses.Count == 1)
        {
            return distinctStatuses.First();
        }

        // Return the "lowest" status if mixed
        if (parts.Any(p => p.Status == PartStatus.Pending)) return PartStatus.Pending;
        if (parts.Any(p => p.Status == PartStatus.Cut)) return PartStatus.Cut;
        if (parts.Any(p => p.Status == PartStatus.Sorted)) return PartStatus.Sorted;
        if (parts.Any(p => p.Status == PartStatus.Assembled)) return PartStatus.Assembled;
        return PartStatus.Shipped;
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
    public PartStatus EffectiveStatus { get; set; }
}