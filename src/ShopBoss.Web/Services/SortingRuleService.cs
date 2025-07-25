using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class SortingRuleService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SortingRuleService> _logger;
    public SortingRuleService(ShopBossDbContext context, ILogger<SortingRuleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(string? RackId, int? Row, int? Column, string Message)> FindOptimalBinForPartAsync(string partId, string workOrderId, string? preferredRackId = null)
    {
        try
        {
            var part = await _context.Parts
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == partId);

            if (part == null)
            {
                return (null, null, null, "Part not found");
            }

            // Use the part's stored category to determine if it needs specialized routing
            var partCategory = part.Category;
            var preferredRackType = (RackType)partCategory;
            
            List<StorageRack> suitableRacks;
            
            // Check if this part requires specialized routing (doors, drawer fronts, adjustable shelves)
            if (part.Category != PartCategory.Standard)
            {
                // Filtered parts ALWAYS go to specialized racks, ignoring any preferred rack selection
                _logger.LogInformation("Part '{PartName}' requires specialized routing to {RackType} - ignoring preferred rack selection", 
                    part.Name, preferredRackType);
                
                suitableRacks = await _context.StorageRacks
                    .Include(r => r.Bins)
                    .Where(r => r.IsActive && r.Type == preferredRackType)
                    .ToListAsync();
                    
                if (!suitableRacks.Any())
                {
                    return (null, null, null, $"No {preferredRackType} racks available for {partCategory} parts. Please configure appropriate specialized racks.");
                }
            }
            else if (!string.IsNullOrEmpty(preferredRackId))
            {
                // Standard parts can use preferred rack if specified
                var specificRack = await _context.StorageRacks
                    .Include(r => r.Bins)
                    .FirstOrDefaultAsync(r => r.Id == preferredRackId && r.IsActive);
                    
                if (specificRack == null)
                {
                    _logger.LogWarning("Preferred rack '{PreferredRackId}' not found or inactive, falling back to standard sorting logic", preferredRackId);
                    // Fall back to standard logic instead of failing
                    suitableRacks = await _context.StorageRacks
                        .Include(r => r.Bins)
                        .Where(r => r.IsActive && 
                                   (r.Type == preferredRackType || 
                                    (preferredRackType == RackType.Standard && r.Type != RackType.DoorsAndDrawerFronts)))
                        .ToListAsync();

                    // Sort in memory since OccupancyPercentage is a computed property
                    suitableRacks = suitableRacks
                        .OrderBy(r => r.Type == preferredRackType ? 0 : 1) // Prefer exact match
                        .ThenBy(r => r.OccupancyPercentage) // Prefer less occupied racks
                        .ToList();

                    if (!suitableRacks.Any())
                    {
                        return (null, null, null, $"Preferred rack '{preferredRackId}' not found and no alternative suitable racks available for {partCategory} parts");
                    }
                }
                else
                {
                    suitableRacks = new List<StorageRack> { specificRack };
                    
                    // Check if rack is completely full
                    if (specificRack.AvailableBins == 0)
                    {
                        return (null, null, null, $"Rack '{specificRack.Name}' is full - no available bins. Please select a different rack.");
                    }
                }
            }
            else
            {
                // No preferred rack specified for standard parts - use standard logic

                suitableRacks = await _context.StorageRacks
                    .Include(r => r.Bins)
                    .Where(r => r.IsActive && 
                               (r.Type == preferredRackType || 
                                (preferredRackType == RackType.Standard && r.Type != RackType.DoorsAndDrawerFronts)))
                    .ToListAsync();

                // Sort in memory since OccupancyPercentage is a computed property
                suitableRacks = suitableRacks
                    .OrderBy(r => r.Type == preferredRackType ? 0 : 1) // Prefer exact match
                    .ThenBy(r => r.OccupancyPercentage) // Prefer less occupied racks
                    .ToList();

                if (!suitableRacks.Any())
                {
                    return (null, null, null, $"No suitable racks available for {partCategory} parts");
                }
            }

            // Try to group parts by product - look for existing bins with same product
            if (part.ProductId != null)
            {
                var productGroupBin = await FindProductGroupBinAsync(part.ProductId, suitableRacks, part);
                if (productGroupBin != null)
                {
                    return (productGroupBin.StorageRackId, productGroupBin.Row, productGroupBin.Column, 
                           $"Grouped with product '{part.Product?.Name}' in bin {productGroupBin.BinLabel}");
                }
            }

            // Find first available bin in suitable racks
            foreach (var rack in suitableRacks)
            {
                var availableBin = rack.Bins
                    .Where(b => b.IsAvailable)
                    .OrderBy(b => b.Row)
                    .ThenBy(b => b.Column)
                    .FirstOrDefault();

                if (availableBin != null)
                {
                    return (rack.Id, availableBin.Row, availableBin.Column, 
                           $"Assigned to {rack.Type} rack '{rack.Name}' bin {availableBin.BinLabel}");
                }
            }

            // No available bins found
            string rackNames = string.Join(", ", suitableRacks.Select(r => r.Name));
            return (null, null, null, $"No available bins found in racks: {rackNames}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding optimal bin for part {PartId}", partId);
            return (null, null, null, "Error occurred while finding bin placement");
        }
    }

    private Task<Bin?> FindProductGroupBinAsync(string productId, List<StorageRack> racks, Part newPart)
    {
        // Look for existing bins with same product assignment
        foreach (var rack in racks)
        {
            var productBin = rack.Bins
                .Where(b => b.ProductId == productId && 
                           b.Status != BinStatus.Full && 
                           b.Status != BinStatus.Blocked)
                .OrderBy(b => b.PartsCount) // Prefer bins with fewer parts to balance load
                .FirstOrDefault();

            if (productBin != null)
            {
                _logger.LogInformation("Found existing product group bin {BinLabel} for product {ProductId} (current: {CurrentParts}, adding: {NewParts})", 
                    productBin.BinLabel, productId, productBin.PartsCount, newPart.Qty);
                return Task.FromResult<Bin?>(productBin);
            }
        }

        return Task.FromResult<Bin?>(null);
    }


    public async Task<bool> AssignPartToBinAsync(string partId, string rackId, int row, int column, string workOrderId)
    {
        try
        {
            var part = await _context.Parts
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == partId);

            if (part == null) return false;

            var bin = await _context.Bins
                .Include(b => b.StorageRack)
                .FirstOrDefaultAsync(b => b.StorageRackId == rackId && b.Row == row && b.Column == column);

            if (bin == null || bin.Status == BinStatus.Blocked) return false;


            // Update bin assignment - handle multiple parts in same bin
            if (bin.Status == BinStatus.Empty)
            {
                // First part in bin
                bin.PartId = partId;
                bin.ProductId = part.ProductId;
                bin.WorkOrderId = workOrderId;
                bin.Contents = $"{part.Product?.Name}: {part.Name} (Qty: {part.Qty})";
                bin.AssignedDate = DateTime.UtcNow;
            }
            else
            {
                // Additional part in existing bin - update contents to show multiple parts
                if (bin.ProductId == part.ProductId)
                {
                    // Same product - append part info
                    bin.Contents += $", {part.Name} (Qty: {part.Qty})";
                }
                else
                {
                    _logger.LogWarning("Attempting to mix products in bin {BinLabel} - existing: {ExistingProduct}, new: {NewProduct}", 
                        bin.BinLabel, bin.ProductId, part.ProductId);
                    // Should not happen with proper grouping, but handle gracefully
                    bin.Contents += $" + {part.Product?.Name}: {part.Name} (Qty: {part.Qty})";
                }
            }
            
            bin.PartsCount = bin.PartsCount + 1;
            bin.Status = BinStatus.Partial; // Always partial when parts are added (full status determined by product completion logic elsewhere)
            bin.LastUpdatedDate = DateTime.UtcNow;

            // Update part status to Sorted and set location
            part.Status = PartStatus.Sorted;
            part.StatusUpdatedDate = DateTime.UtcNow;
            part.Location = $"{bin.StorageRack.Name}:{bin.BinLabel}"; // e.g., "Standard Rack A:A01"

            _logger.LogInformation("Assigned part {PartId} ({PartName}) to bin {BinLabel} at location {Location} - new total: {TotalParts} parts", 
                partId, part.Name, bin.BinLabel, part.Location, bin.PartsCount);

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning part {PartId} to bin", partId);
            return false;
        }
    }

    public async Task<List<StorageRack>> GetActiveRacksAsync()
    {
        return await _context.StorageRacks
            .Include(r => r.Bins)
            .Where(r => r.IsActive)
            .OrderBy(r => r.Type)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<StorageRack?> GetRackWithBinsAsync(string rackId)
    {
        // Load rack with bins first to avoid cartesian products
        var rack = await _context.StorageRacks
            .Include(r => r.Bins)
            .FirstOrDefaultAsync(r => r.Id == rackId);

        if (rack == null)
        {
            return null;
        }

        // Load parts and products for bins in separate queries
        var binIds = rack.Bins.Select(b => b.Id).ToList();
        
        var binsWithParts = await _context.Bins
            .Where(b => binIds.Contains(b.Id) && b.PartId != null)
            .Include(b => b.Part)
            .ToListAsync();

        var binsWithProducts = await _context.Bins
            .Where(b => binIds.Contains(b.Id) && b.ProductId != null)
            .Include(b => b.Product)
            .ToListAsync();

        // Update the bins in the rack with the loaded relationships
        foreach (var bin in rack.Bins)
        {
            var binWithPart = binsWithParts.FirstOrDefault(b => b.Id == bin.Id);
            if (binWithPart != null)
            {
                bin.Part = binWithPart.Part;
            }

            var binWithProduct = binsWithProducts.FirstOrDefault(b => b.Id == bin.Id);
            if (binWithProduct != null)
            {
                bin.Product = binWithProduct.Product;
            }
        }

        return rack;
    }

    public async Task<List<string>> CheckAssemblyReadinessAsync(string workOrderId)
    {
        try
        {
            var readyProducts = new List<string>();

            // Get all products in the work order with their parts
            var products = await _context.Products
                .Include(p => p.Parts)
                .Where(p => p.WorkOrderId == workOrderId)
                .ToListAsync();

            foreach (var product in products)
            {
                // Only consider standard parts for assembly readiness - filtered parts (doors, drawer fronts, adjustable shelves) 
                // are processed in specialized streams and don't determine standard assembly readiness
                var standardParts = product.Parts.Where(p => p.Category == PartCategory.Standard).ToList();
                
                if (!standardParts.Any())
                {
                    // Product has no standard parts, skip assembly readiness check
                    continue;
                }

                // Check if all standard parts for this product are sorted
                var allStandardPartsSorted = standardParts.All(part => part.Status == PartStatus.Sorted);
                
                if (allStandardPartsSorted)
                {
                    readyProducts.Add(product.Id);
                    
                    var filteredParts = product.Parts.Where(p => p.Category != PartCategory.Standard).ToList();
                    _logger.LogInformation("Product {ProductId} ({ProductName}) is ready for assembly - all {StandardPartCount} standard parts are sorted. " +
                                         "Filtered parts: {FilteredPartCount} (doors/fronts/shelves processed separately)",
                        product.Id, product.Name, standardParts.Count, filteredParts.Count);
                }
            }

            return readyProducts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking assembly readiness for work order {WorkOrderId}", workOrderId);
            return new List<string>();
        }
    }

    public async Task<bool> MarkProductReadyForAssemblyAsync(string productId)
    {
        try
        {
            var product = await _context.Products
                .Include(p => p.Parts)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found for assembly readiness marking", productId);
                return false;
            }

            // Verify all standard parts are actually sorted (filtered parts processed separately)
            var standardParts = product.Parts.Where(p => p.Category == PartCategory.Standard).ToList();
            var allStandardPartsSorted = standardParts.All(part => part.Status == PartStatus.Sorted);
            
            if (!allStandardPartsSorted)
            {
                _logger.LogWarning("Cannot mark product {ProductId} as ready - not all standard parts are sorted", productId);
                return false;
            }

            // Product readiness tracking is handled via audit trail
            await _context.SaveChangesAsync();

            _logger.LogInformation("Product {ProductId} ({ProductName}) marked as ready for assembly", 
                product.Id, product.Name);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking product {ProductId} as ready for assembly", productId);
            return false;
        }
    }
}