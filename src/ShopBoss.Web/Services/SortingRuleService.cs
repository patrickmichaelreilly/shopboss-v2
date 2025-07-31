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

    public async Task<(string? BinId, string Message)> FindOptimalBinForPartAsync(string partId, string workOrderId, string? preferredRackId = null)
    {
        try
        {
            var part = await _context.Parts
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == partId);

            if (part == null)
            {
                return (null, "Part not found");
            }

            // Check for detached product (single-part products go to Cart)
            if (part.Product != null)
            {
                var productPartsCount = await _context.Parts
                    .CountAsync(p => p.ProductId == part.ProductId);
                    
                if (productPartsCount == 1)
                {
                    // This is a detached product - route to Cart
                    var cartRacks = await _context.StorageRacks
                        .Include(r => r.Bins)
                        .Where(r => r.IsActive && r.Type == RackType.Cart)
                        .ToListAsync();
                        
                    if (cartRacks.Any())
                    {
                        return FindBinInRacks(cartRacks, part, "Cart (detached product)");
                    }
                    
                    _logger.LogWarning("No Cart racks available for detached product {PartName}", part.Name);
                }
            }

            // Determine rack type using keyword-based sorting rules
            var preferredRackType = await DetermineRackTypeForPartAsync(part.Name);
            
            List<StorageRack> suitableRacks;
            
            // Check if this part requires specialized routing based on sorting rules
            if (preferredRackType != RackType.Standard)
            {
                // Specialized parts ALWAYS go to their designated racks, ignoring any preferred rack selection
                _logger.LogInformation("Part '{PartName}' requires specialized routing to {RackType} - ignoring preferred rack selection", 
                    part.Name, preferredRackType);
                
                suitableRacks = await _context.StorageRacks
                    .Include(r => r.Bins)
                    .Where(r => r.IsActive && r.Type == preferredRackType)
                    .ToListAsync();
                    
                if (!suitableRacks.Any())
                {
                    return (null, $"No {preferredRackType} racks available. Please configure appropriate specialized racks.");
                }
                
                return FindBinInRacks(suitableRacks, part, $"{preferredRackType} (keyword match)");
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
                        return (null, $"Preferred rack '{preferredRackId}' not found and no alternative suitable racks available");
                    }
                }
                else
                {
                    suitableRacks = new List<StorageRack> { specificRack };
                }
            }
            else
            {
                // No preferred rack specified for standard parts - use standard logic

                suitableRacks = await _context.StorageRacks
                    .Include(r => r.Bins)
                    .Where(r => r.IsActive && 
                               (r.Type == preferredRackType || 
                                (preferredRackType == RackType.Standard && r.Type == RackType.Standard)))
                    .ToListAsync();

                // Sort in memory since OccupancyPercentage is a computed property
                suitableRacks = suitableRacks
                    .OrderBy(r => r.Type == preferredRackType ? 0 : 1) // Prefer exact match
                    .ThenBy(r => r.OccupancyPercentage) // Prefer less occupied racks
                    .ToList();

                if (!suitableRacks.Any())
                {
                    return (null, "No suitable racks available for standard parts");
                }
            }

            // Product grouping logic is now handled in FindBinInRacks to properly handle full bins
            return FindBinInRacks(suitableRacks, part, "Standard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding optimal bin for part {PartId}", partId);
            return (null, "Error occurred while finding bin placement");
        }
    }


    public async Task<bool> AssignPartToBinAsync(string partId, string binId, string workOrderId)
    {
        try
        {
            var part = await _context.Parts
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == partId);

            if (part == null) return false;

            var bin = await _context.Bins
                .Include(b => b.StorageRack)
                .FirstOrDefaultAsync(b => b.Id == binId);

            if (bin == null || bin.Status == BinStatus.Blocked) return false;


            // Update bin assignment - handle multiple parts in same bin
            if (bin.Status == BinStatus.Empty)
            {
                // First part in bin
                bin.PartId = partId;
                bin.ProductId = part.ProductId;
                bin.WorkOrderId = workOrderId;
                bin.Contents = $"{part.Product?.Name}: {part.Name} (Qty: {part.Qty})";
                bin.AssignedDate = DateTime.Now;
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
            bin.LastUpdatedDate = DateTime.Now;

            // Update part status to Sorted and set bin relationship
            part.Status = PartStatus.Sorted;
            part.StatusUpdatedDate = DateTime.Now;
            part.BinId = bin.Id; // Direct foreign key reference
            part.Location = $"{bin.StorageRack.Name}:{bin.BinLabel}"; // Keep legacy location for compatibility

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
                var standardParts = new List<Part>();
                foreach (var part in product.Parts)
                {
                    var rackType = await DetermineRackTypeForPartAsync(part.Name);
                    if (rackType == RackType.Standard)
                    {
                        standardParts.Add(part);
                    }
                }
                
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
                    
                    var totalPartsCount = product.Parts.Count;
                    var filteredPartsCount = totalPartsCount - standardParts.Count;
                    _logger.LogDebug("Product {ProductId} ({ProductName}) is ready for assembly - all {StandardPartCount} standard parts are sorted. " +
                                         "Filtered parts: {FilteredPartCount} (doors/fronts/shelves processed separately)",
                        product.Id, product.Name, standardParts.Count, filteredPartsCount);
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

            // Check if product is already marked as ready (Sorted status)
            if (product.Status == PartStatus.Sorted)
            {
                _logger.LogDebug("Product {ProductId} ({ProductName}) is already marked as ready for assembly", 
                    product.Id, product.Name);
                return false; // Already ready, no state change
            }

            // Verify all standard parts are actually sorted (filtered parts processed separately)
            var standardParts = new List<Part>();
            foreach (var part in product.Parts)
            {
                var rackType = await DetermineRackTypeForPartAsync(part.Name);
                if (rackType == RackType.Standard)
                {
                    standardParts.Add(part);
                }
            }
            var allStandardPartsSorted = standardParts.All(part => part.Status == PartStatus.Sorted);
            
            if (!allStandardPartsSorted)
            {
                _logger.LogWarning("Cannot mark product {ProductId} as ready - not all standard parts are sorted", productId);
                return false;
            }

            // Mark product as ready by updating its status
            product.Status = PartStatus.Sorted;
            product.StatusUpdatedDate = DateTime.Now;
            
            await _context.SaveChangesAsync();

            _logger.LogDebug("Product {ProductId} ({ProductName}) newly marked as ready for assembly", 
                product.Id, product.Name);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking product {ProductId} as ready for assembly", productId);
            return false;
        }
    }

    /// <summary>
    /// Determines the appropriate rack type for a part based on keyword-based sorting rules
    /// </summary>
    private async Task<RackType> DetermineRackTypeForPartAsync(string partName)
    {
        try
        {
            // Get active sorting rules ordered by priority (lower number = higher priority)
            var rules = await _context.SortingRules
                .Where(r => r.IsActive)
                .OrderBy(r => r.Priority)
                .ToListAsync();

            // Check each rule in priority order
            foreach (var rule in rules)
            {
                if (rule.MatchesPartName(partName))
                {
                    _logger.LogDebug("Part '{PartName}' matched rule '{RuleName}' -> {RackType}", 
                        partName, rule.Name, rule.TargetRackType);
                    return rule.TargetRackType;
                }
            }

            // No rules matched - default to Standard
            _logger.LogDebug("Part '{PartName}' did not match any sorting rules -> Standard", partName);
            return RackType.Standard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining rack type for part '{PartName}', defaulting to Standard", partName);
            return RackType.Standard;
        }
    }

    /// <summary>
    /// Finds an available bin in the provided racks, with product grouping logic
    /// </summary>
    private (string? BinId, string Message) FindBinInRacks(
        List<StorageRack> suitableRacks, Part part, string rackTypeDescription)
    {
        // Try to group parts by product - look for existing bins with same product
        // This should check ALL bins, not just non-full ones
        if (part.ProductId != null)
        {
            // First check if any bin already has parts from this product
            foreach (var rack in suitableRacks)
            {
                var existingProductBin = rack.Bins
                    .Where(b => b.ProductId == part.ProductId && 
                               b.Status != BinStatus.Blocked)
                    .FirstOrDefault();
                    
                if (existingProductBin != null)
                {
                    // Found a bin with same product - use it regardless of "full" status
                    return (existingProductBin.Id, $"Added to existing product '{part.Product?.Name}' in bin {existingProductBin.BinLabel}");
                }
            }
        }

        // No existing product bin found - find first available empty bin
        foreach (var rack in suitableRacks)
        {
            var availableBin = rack.Bins
                .Where(b => b.IsAvailable)
                .FirstOrDefault();

            if (availableBin != null)
            {
                return (availableBin.Id, 
                       $"Assigned to {rack.Type} rack '{rack.Name}' bin {availableBin.BinLabel} ({rackTypeDescription})");
            }
        }

        // No available bins found
        string rackNames = string.Join(", ", suitableRacks.Select(r => r.Name));
        return (null, $"No available bins found in {rackTypeDescription} racks: {rackNames}");
    }

    /// <summary>
    /// Seeds default sorting rules that match the current PartCategory behavior
    /// </summary>
    public async Task SeedDefaultSortingRulesAsync()
    {
        try
        {
            // Check if rules already exist
            var existingRulesCount = await _context.SortingRules.CountAsync();
            if (existingRulesCount > 0)
            {
                _logger.LogInformation("Sorting rules already exist ({Count} rules), skipping seeding", existingRulesCount);
                return;
            }

            var defaultRules = new List<SortingRule>
            {
                new SortingRule
                {
                    Name = "Doors and Drawer Fronts",
                    Priority = 1,
                    Keywords = "DOOR,DRAWER FRONT",
                    TargetRackType = RackType.DoorsAndDrawerFronts,
                    IsActive = true
                },
                new SortingRule
                {
                    Name = "Adjustable Shelves",
                    Priority = 2,
                    Keywords = "ADJ SHELF,ADJUSTABLE",
                    TargetRackType = RackType.AdjustableShelves,
                    IsActive = true
                },
                new SortingRule
                {
                    Name = "Hardware",
                    Priority = 3,
                    Keywords = "HARDWARE,HINGE,SLIDE",
                    TargetRackType = RackType.Hardware,
                    IsActive = true
                }
                // Standard parts don't need a rule - they default to Standard rack type
            };

            _context.SortingRules.AddRange(defaultRules);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded {Count} default sorting rules", defaultRules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default sorting rules");
            throw;
        }
    }
}