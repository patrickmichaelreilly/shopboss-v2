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

            // If a preferred rack is specified, only use that rack
            List<StorageRack> suitableRacks;
            if (!string.IsNullOrEmpty(preferredRackId))
            {
                var specificRack = await _context.StorageRacks
                    .Include(r => r.Bins)
                    .FirstOrDefaultAsync(r => r.Id == preferredRackId && r.IsActive);
                    
                if (specificRack == null)
                {
                    return (null, null, null, $"Preferred rack '{preferredRackId}' not found or inactive");
                }
                
                suitableRacks = new List<StorageRack> { specificRack };
                
                // Check if rack is completely full
                if (specificRack.AvailableBins == 0)
                {
                    return (null, null, null, $"Rack '{specificRack.Name}' is full - no available bins. Please select a different rack.");
                }
            }
            else
            {
                // Original logic for finding suitable racks based on part type
                var partCategory = DeterminePartCategory(part);
                var preferredRackType = GetPreferredRackType(partCategory);

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
                    return (null, null, null, $"No suitable racks available for {DeterminePartCategory(part)} parts");
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
        // Look for existing bins with same product assignment that have capacity for this part
        foreach (var rack in racks)
        {
            var productBin = rack.Bins
                .Where(b => b.ProductId == productId && 
                           b.Status != BinStatus.Full && 
                           b.Status != BinStatus.Blocked &&
                           (b.PartsCount + newPart.Qty) <= b.MaxCapacity) // Ensure new part will fit
                .OrderBy(b => b.PartsCount) // Prefer bins with fewer parts to balance load
                .FirstOrDefault();

            if (productBin != null)
            {
                _logger.LogInformation("Found existing product group bin {BinLabel} for product {ProductId} (current: {CurrentParts}, adding: {NewParts}, max: {MaxCapacity})", 
                    productBin.BinLabel, productId, productBin.PartsCount, newPart.Qty, productBin.MaxCapacity);
                return Task.FromResult<Bin?>(productBin);
            }
        }

        return Task.FromResult<Bin?>(null);
    }

    private PartCategory DeterminePartCategory(Part part)
    {
        var partName = part.Name.ToLower();
        
        // Check for doors and drawer fronts
        if (partName.Contains("door") || 
            partName.Contains("drawer front") || 
            partName.Contains("panel") && (partName.Contains("door") || partName.Contains("front")))
        {
            return PartCategory.DoorsAndDrawerFronts;
        }

        // Check for adjustable shelves
        if (partName.Contains("adjustable") || 
            partName.Contains("shelf") && partName.Contains("adj"))
        {
            return PartCategory.AdjustableShelves;
        }

        // Check for hardware (though hardware typically doesn't go in part bins)
        if (partName.Contains("hinge") || 
            partName.Contains("handle") || 
            partName.Contains("knob") ||
            partName.Contains("screw") ||
            partName.Contains("bracket"))
        {
            return PartCategory.Hardware;
        }

        // Default to carcass parts (sides, backs, tops, bottoms, etc.)
        return PartCategory.Carcass;
    }

    private RackType GetPreferredRackType(PartCategory category)
    {
        return category switch
        {
            PartCategory.DoorsAndDrawerFronts => RackType.DoorsAndDrawerFronts,
            PartCategory.AdjustableShelves => RackType.AdjustableShelves,
            PartCategory.Hardware => RackType.Hardware,
            PartCategory.Carcass => RackType.Standard,
            _ => RackType.Standard
        };
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
                .FirstOrDefaultAsync(b => b.StorageRackId == rackId && b.Row == row && b.Column == column);

            if (bin == null || bin.Status == BinStatus.Blocked) return false;

            // Check if adding this part would exceed capacity
            var newTotalParts = bin.PartsCount + part.Qty;
            if (newTotalParts > bin.MaxCapacity)
            {
                _logger.LogWarning("Cannot assign part {PartId} to bin {BinLabel} - would exceed capacity ({Current} + {New} > {Max})", 
                    partId, bin.BinLabel, bin.PartsCount, part.Qty, bin.MaxCapacity);
                return false;
            }

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
            
            bin.PartsCount = newTotalParts;
            bin.Status = newTotalParts >= bin.MaxCapacity ? BinStatus.Full : BinStatus.Partial;
            bin.LastUpdatedDate = DateTime.UtcNow;

            // Update part status to Sorted and set location
            part.Status = PartStatus.Sorted;
            part.StatusUpdatedDate = DateTime.UtcNow;
            part.Location = $"{rackId}-{bin.BinLabel}"; // e.g., "RackId-A01"

            _logger.LogInformation("Assigned part {PartId} ({PartName}) to bin {BinLabel} at location {Location} - new total: {TotalParts}/{MaxCapacity}", 
                partId, part.Name, bin.BinLabel, part.Location, bin.PartsCount, bin.MaxCapacity);

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
        return await _context.StorageRacks
            .Include(r => r.Bins)
                .ThenInclude(b => b.Part)
            .Include(r => r.Bins)
                .ThenInclude(b => b.Product)
            .FirstOrDefaultAsync(r => r.Id == rackId);
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
                // Check if all parts for this product are sorted
                var allPartsSorted = product.Parts.All(part => part.Status == PartStatus.Sorted);
                
                if (allPartsSorted && product.Parts.Any())
                {
                    readyProducts.Add(product.Id);
                    _logger.LogInformation("Product {ProductId} ({ProductName}) is ready for assembly - all {PartCount} parts are sorted", 
                        product.Id, product.Name, product.Parts.Count);
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

            // Verify all parts are actually sorted
            var allPartsSorted = product.Parts.All(part => part.Status == PartStatus.Sorted);
            
            if (!allPartsSorted)
            {
                _logger.LogWarning("Cannot mark product {ProductId} as ready - not all parts are sorted", productId);
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

public enum PartCategory
{
    Carcass,
    DoorsAndDrawerFronts,
    AdjustableShelves,
    Hardware
}