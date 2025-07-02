using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Services;
using ShopBoss.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace ShopBoss.Web.Controllers;

public class AssemblyController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly SortingRuleService _sortingRuleService;
    private readonly PartFilteringService _partFilteringService;
    private readonly AuditTrailService _auditTrailService;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly ILogger<AssemblyController> _logger;

    public AssemblyController(
        ShopBossDbContext context,
        SortingRuleService sortingRuleService,
        PartFilteringService partFilteringService,
        AuditTrailService auditTrailService,
        IHubContext<StatusHub> hubContext,
        ILogger<AssemblyController> logger)
    {
        _context = context;
        _sortingRuleService = sortingRuleService;
        _partFilteringService = partFilteringService;
        _auditTrailService = auditTrailService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
        
        if (string.IsNullOrEmpty(activeWorkOrderId))
        {
            TempData["ErrorMessage"] = "No active work order selected. Please select a work order from the Admin Station.";
            return View("NoActiveWorkOrder");
        }

        var workOrder = await _context.WorkOrders
            .Include(w => w.Products)
                .ThenInclude(p => p.Parts)
            .Include(w => w.Hardware)
            .Include(w => w.DetachedProducts)
            .FirstOrDefaultAsync(w => w.Id == activeWorkOrderId);

        if (workOrder == null)
        {
            TempData["ErrorMessage"] = "Active work order not found. Please select a valid work order.";
            return View("NoActiveWorkOrder");
        }

        // Get assembly readiness information
        var readyProductIds = await _sortingRuleService.CheckAssemblyReadinessAsync(activeWorkOrderId);
        
        // Get rack information to show sorting status
        var racks = await _sortingRuleService.GetActiveRacksAsync();

        // Prepare assembly readiness data
        var assemblyData = new AssemblyDashboardData
        {
            WorkOrder = workOrder,
            ReadyProductIds = readyProductIds,
            StorageRacks = racks,
            Products = await GetProductsWithAssemblyStatus(workOrder.Products, readyProductIds),
            FilteredParts = await GetFilteredPartsLocations(workOrder.Products)
        };

        return View(assemblyData);
    }

    private async Task<List<ProductAssemblyStatus>> GetProductsWithAssemblyStatus(
        List<Product> products, 
        List<string> readyProductIds)
    {
        var result = new List<ProductAssemblyStatus>();

        foreach (var product in products)
        {
            var carcassParts = _partFilteringService.GetCarcassPartsOnly(product.Parts);
            var filteredParts = _partFilteringService.GetFilteredParts(product.Parts);
            
            var sortedCarcassParts = carcassParts.Count(p => p.Status == PartStatus.Sorted);
            var totalCarcassParts = carcassParts.Count;
            var sortedFilteredParts = filteredParts.Count(p => p.Status == PartStatus.Sorted);
            var totalFilteredParts = filteredParts.Count;
            
            var isReady = readyProductIds.Contains(product.Id);
            var isCompleted = carcassParts.All(p => p.Status == PartStatus.Assembled);

            // Get simplified rack locations - just carcass bin and doors/fronts bin
            var carcassLocation = carcassParts
                .Where(p => p.Status == PartStatus.Sorted && !string.IsNullOrEmpty(p.Location))
                .Select(p => p.Location)
                .FirstOrDefault();
                
            var doorsLocation = filteredParts
                .Where(p => p.Status == PartStatus.Sorted && !string.IsNullOrEmpty(p.Location))
                .Select(p => p.Location)
                .FirstOrDefault();

            var partLocations = new List<PartLocation>();
            if (!string.IsNullOrEmpty(carcassLocation))
            {
                partLocations.Add(new PartLocation
                {
                    PartName = "Carcass Parts",
                    Location = carcassLocation,
                    Quantity = sortedCarcassParts
                });
            }
            if (!string.IsNullOrEmpty(doorsLocation))
            {
                partLocations.Add(new PartLocation
                {
                    PartName = "Doors & Fronts",
                    Location = doorsLocation,
                    Quantity = sortedFilteredParts
                });
            }
            else if (totalFilteredParts > 0)
            {
                partLocations.Add(new PartLocation
                {
                    PartName = "Doors & Fronts",
                    Location = "N/A",
                    Quantity = 0
                });
            }

            result.Add(new ProductAssemblyStatus
            {
                Product = product,
                CarcassPartsCount = totalCarcassParts,
                SortedCarcassPartsCount = sortedCarcassParts,
                FilteredPartsCount = totalFilteredParts,
                SortedFilteredPartsCount = sortedFilteredParts,
                IsReadyForAssembly = isReady,
                IsCompleted = isCompleted,
                CompletionPercentage = totalCarcassParts > 0 ? (int)((double)sortedCarcassParts / totalCarcassParts * 100) : 0,
                PartLocations = partLocations
            });
        }

        return result.OrderByDescending(p => p.IsReadyForAssembly)
                    .ThenByDescending(p => p.CompletionPercentage)
                    .ToList();
    }

    private async Task<List<FilteredPartLocation>> GetFilteredPartsLocations(List<Product> products)
    {
        var result = new List<FilteredPartLocation>();

        foreach (var product in products)
        {
            var filteredParts = _partFilteringService.GetFilteredParts(product.Parts);
            
            foreach (var part in filteredParts)
            {
                var category = _partFilteringService.ClassifyPart(part);
                var location = !string.IsNullOrEmpty(part.Location) ? part.Location : "Not Sorted";
                
                result.Add(new FilteredPartLocation
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    PartName = part.Name,
                    Category = category.ToString(),
                    Location = location,
                    Status = part.Status.ToString(),
                    Quantity = part.Qty
                });
            }
        }

        return result.OrderBy(f => f.ProductName)
                    .ThenBy(f => f.Category)
                    .ThenBy(f => f.PartName)
                    .ToList();
    }

    [HttpPost]
    public async Task<IActionResult> ScanPartForAssembly(string barcode)
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected" });
            }

            // Validate barcode input
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return Json(new { success = false, message = "Please enter a valid barcode" });
            }

            barcode = barcode.Trim();

            // Find the part by barcode in the active work order (same logic as Sorting station)
            var part = await _context.Parts
                .Include(p => p.Product)
                    .ThenInclude(pr => pr.Parts)
                .Include(p => p.NestSheet)
                .FirstOrDefaultAsync(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && 
                                    (p.Id == barcode || p.Name == barcode));

            if (part == null)
            {
                return Json(new { 
                    success = false, 
                    message = $"Part with barcode '{barcode}' not found in active work order" 
                });
            }

            // Check if the part belongs to a product that's ready for assembly
            // Reload the product with all its parts to ensure we have current data
            var product = await _context.Products
                .Include(p => p.Parts)
                .FirstOrDefaultAsync(p => p.Id == part.Product.Id);
                
            if (product == null)
            {
                return Json(new { success = false, message = "Product not found" });
            }
            
            var carcassParts = _partFilteringService.GetCarcassPartsOnly(product.Parts);
            var allCarcassPartsSorted = carcassParts.All(p => p.Status == PartStatus.Sorted);
            

            if (!allCarcassPartsSorted)
            {
                var sortedCount = carcassParts.Count(p => p.Status == PartStatus.Sorted);
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' is not ready for assembly. Only {sortedCount}/{carcassParts.Count} carcass parts are sorted." 
                });
            }

            // Check if product is already assembled
            var alreadyAssembled = carcassParts.All(p => p.Status == PartStatus.Assembled);
            if (alreadyAssembled)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' is already assembled" 
                });
            }

            // Mark all carcass parts as assembled and empty their bins
            var assembledParts = 0;
            var binsToEmpty = new HashSet<string>();
            
            foreach (var carcassPart in carcassParts)
            {
                if (carcassPart.Status == PartStatus.Sorted)
                {
                    // Track bin location for emptying
                    if (!string.IsNullOrEmpty(carcassPart.Location))
                    {
                        binsToEmpty.Add(carcassPart.Location);
                    }
                    
                    carcassPart.Status = PartStatus.Assembled;
                    carcassPart.StatusUpdatedDate = DateTime.UtcNow;
                    carcassPart.Location = null; // Clear location since part is no longer in bin
                    assembledParts++;

                    // Log the status change
                    await _auditTrailService.LogAsync(
                        entityType: "Part",
                        entityId: carcassPart.Id,
                        action: "PartScannedForAssembly",
                        details: $"Part status changed from Sorted to Assembled via barcode scan (scanned: {barcode})",
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }

            // Mark filtered parts as assembled and track their bins for emptying
            var filteredParts = _partFilteringService.GetFilteredParts(product.Parts);
            foreach (var filteredPart in filteredParts)
            {
                if (filteredPart.Status == PartStatus.Sorted)
                {
                    // Track bin location for emptying
                    if (!string.IsNullOrEmpty(filteredPart.Location))
                    {
                        binsToEmpty.Add(filteredPart.Location);
                    }
                    
                    filteredPart.Status = PartStatus.Assembled;
                    filteredPart.StatusUpdatedDate = DateTime.UtcNow;
                    filteredPart.Location = null;
                    assembledParts++;

                    // Log the status change
                    await _auditTrailService.LogAsync(
                        entityType: "Part",
                        entityId: filteredPart.Id,
                        action: "PartScannedForAssembly",
                        details: $"Filtered part status changed from Sorted to Assembled via barcode scan (scanned: {barcode})",
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }
            
            // Empty the bins that contained the assembled parts
            foreach (var binLocation in binsToEmpty)
            {
                var binParts = await _context.Parts
                    .Where(p => p.Location == binLocation && p.Status == PartStatus.Sorted)
                    .ToListAsync();
                
                // If bin has no more sorted parts, mark it as empty
                if (!binParts.Any())
                {
                    // Parse the location format "RackName:BinCode" (e.g., "Standard Rack A:A01")
                    var locationParts = binLocation.Split(':', 2);
                    Bin? bin = null;
                    if (locationParts.Length == 2)
                    {
                        var rackName = locationParts[0];
                        var binCode = locationParts[1]; // e.g., "A01"
                        
                        // Parse bin code: first char is row letter (A=1, B=2, etc), remaining digits are column
                        if (binCode.Length >= 2 && char.IsLetter(binCode[0]) && int.TryParse(binCode.Substring(1), out var column))
                        {
                            var row = char.ToUpper(binCode[0]) - 'A' + 1;
                            
                            bin = await _context.Bins
                                .Include(b => b.StorageRack)
                                .FirstOrDefaultAsync(b => b.StorageRack.Name == rackName && b.Row == row && b.Column == column);
                        }
                    }
                    
                    if (bin != null)
                    {
                        bin.Status = BinStatus.Empty;
                        bin.ProductId = null;
                        bin.PartId = null;
                        bin.WorkOrderId = null;
                        bin.PartsCount = 0;
                        bin.Contents = string.Empty;
                        bin.LastUpdatedDate = DateTime.UtcNow;
                        
                        await _auditTrailService.LogAsync(
                            entityType: "StorageBin",
                            entityId: bin.Id,
                            action: "BinEmptied",
                            details: $"Bin emptied after assembly of product {part.Product.Name}",
                            oldValue: "Occupied",
                            newValue: "Empty",
                            sessionId: HttpContext.Session.Id
                        );
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Get filtered parts locations for guidance
            var filteredParts = _partFilteringService.GetFilteredParts(product.Parts);
            var filteredPartsGuidance = filteredParts
                .Where(fp => fp.Status == PartStatus.Sorted)
                .Select(fp => new
                {
                    Name = fp.Name,
                    Category = _partFilteringService.ClassifyPart(fp).ToString(),
                    Location = fp.Location ?? "Unknown Location",
                    Quantity = fp.Qty
                })
                .ToList();

            // Send SignalR notification
            await _hubContext.Clients.Group($"WorkOrder_{activeWorkOrderId}")
                .SendAsync("ProductAssembledByScan", new
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ScannedPartName = part.Name,
                    ScannedBarcode = barcode,
                    AssembledPartsCount = assembledParts,
                    FilteredPartsCount = filteredPartsGuidance.Count,
                    Timestamp = DateTime.UtcNow
                });

            _logger.LogInformation("Product {ProductId} ({ProductName}) assembled via barcode scan '{Barcode}' - {AssembledParts} carcass parts marked as Assembled",
                product.Id, product.Name, barcode, assembledParts);

            return Json(new { 
                success = true, 
                message = $"✅ Product '{product.Name}' assembly completed!",
                productId = product.Id,
                productName = product.Name,
                assembledPartsCount = assembledParts,
                filteredPartsGuidance = filteredPartsGuidance,
                hasFilteredParts = filteredPartsGuidance.Count > 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing barcode scan for assembly: {Barcode}", barcode);
            return Json(new { success = false, message = "An error occurred while processing the scan" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> StartAssembly(string productId)
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected" });
            }

            var product = await _context.Products
                .Include(p => p.Parts)
                .FirstOrDefaultAsync(p => p.Id == productId && p.WorkOrderId == activeWorkOrderId);

            if (product == null)
            {
                return Json(new { success = false, message = "Product not found in active work order" });
            }

            // Verify product is ready for assembly
            var carcassParts = _partFilteringService.GetCarcassPartsOnly(product.Parts);
            var allCarcassPartsSorted = carcassParts.All(p => p.Status == PartStatus.Sorted);

            if (!allCarcassPartsSorted)
            {
                return Json(new { 
                    success = false, 
                    message = "Not all carcass parts are sorted. Cannot start assembly." 
                });
            }

            // Mark all carcass parts as "Assembled" and empty their bins
            var updatedParts = 0;
            var binsToEmpty = new HashSet<string>();
            
            foreach (var part in carcassParts)
            {
                if (part.Status == PartStatus.Sorted)
                {
                    // Track bin location for emptying
                    if (!string.IsNullOrEmpty(part.Location))
                    {
                        binsToEmpty.Add(part.Location);
                    }
                    
                    part.Status = PartStatus.Assembled;
                    part.StatusUpdatedDate = DateTime.UtcNow;
                    part.Location = null; // Clear location since part is no longer in bin
                    updatedParts++;

                    // Log the status change
                    await _auditTrailService.LogAsync(
                        entityType: "Part",
                        entityId: part.Id,
                        action: "StatusUpdate",
                        details: $"Part status changed from Sorted to Assembled for product {product.Name}",
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }

            // Mark filtered parts as assembled and track their bins for emptying
            var filteredParts = _partFilteringService.GetFilteredParts(product.Parts);
            foreach (var filteredPart in filteredParts)
            {
                if (filteredPart.Status == PartStatus.Sorted)
                {
                    // Track bin location for emptying
                    if (!string.IsNullOrEmpty(filteredPart.Location))
                    {
                        binsToEmpty.Add(filteredPart.Location);
                    }
                    
                    filteredPart.Status = PartStatus.Assembled;
                    filteredPart.StatusUpdatedDate = DateTime.UtcNow;
                    filteredPart.Location = null;
                    updatedParts++;

                    // Log the status change
                    await _auditTrailService.LogAsync(
                        entityType: "Part",
                        entityId: filteredPart.Id,
                        action: "StatusUpdate",
                        details: $"Filtered part status changed from Sorted to Assembled for product {product.Name}",
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }
            
            // Empty the bins that contained the assembled parts
            foreach (var binLocation in binsToEmpty)
            {
                var binParts = await _context.Parts
                    .Where(p => p.Location == binLocation && p.Status == PartStatus.Sorted)
                    .ToListAsync();
                
                // If bin has no more sorted parts, mark it as empty
                if (!binParts.Any())
                {
                    // Parse the location format "RackName:BinCode" (e.g., "Standard Rack A:A01")
                    var locationParts = binLocation.Split(':', 2);
                    Bin? bin = null;
                    if (locationParts.Length == 2)
                    {
                        var rackName = locationParts[0];
                        var binCode = locationParts[1]; // e.g., "A01"
                        
                        // Parse bin code: first char is row letter (A=1, B=2, etc), remaining digits are column
                        if (binCode.Length >= 2 && char.IsLetter(binCode[0]) && int.TryParse(binCode.Substring(1), out var column))
                        {
                            var row = char.ToUpper(binCode[0]) - 'A' + 1;
                            
                            bin = await _context.Bins
                                .Include(b => b.StorageRack)
                                .FirstOrDefaultAsync(b => b.StorageRack.Name == rackName && b.Row == row && b.Column == column);
                        }
                    }
                    
                    if (bin != null)
                    {
                        bin.Status = BinStatus.Empty;
                        bin.ProductId = null;
                        bin.PartId = null;
                        bin.WorkOrderId = null;
                        bin.PartsCount = 0;
                        bin.Contents = string.Empty;
                        bin.LastUpdatedDate = DateTime.UtcNow;
                        
                        await _auditTrailService.LogAsync(
                            entityType: "StorageBin",
                            entityId: bin.Id,
                            action: "BinEmptied",
                            details: $"Bin emptied after manual assembly of product {product.Name}",
                            oldValue: "Occupied",
                            newValue: "Empty",
                            sessionId: HttpContext.Session.Id
                        );
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Send SignalR notification
            await _hubContext.Clients.Group($"WorkOrder_{activeWorkOrderId}")
                .SendAsync("ProductAssembled", new
                {
                    ProductId = productId,
                    ProductName = product.Name,
                    PartsAssembled = updatedParts,
                    Timestamp = DateTime.UtcNow
                });

            _logger.LogInformation("Product {ProductId} ({ProductName}) assembled - {UpdatedParts} parts marked as Assembled",
                productId, product.Name, updatedParts);

            return Json(new { 
                success = true, 
                message = $"Product '{product.Name}' assembly completed! {updatedParts} parts marked as assembled.",
                partsAssembled = updatedParts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting assembly for product {ProductId}", productId);
            return Json(new { success = false, message = "An error occurred while starting assembly" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProductDetails(string productId)
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected" });
            }

            var product = await _context.Products
                .Include(p => p.Parts)
                .FirstOrDefaultAsync(p => p.Id == productId && p.WorkOrderId == activeWorkOrderId);

            if (product == null)
            {
                return Json(new { success = false, message = "Product not found" });
            }

            var carcassParts = _partFilteringService.GetCarcassPartsOnly(product.Parts);
            var filteredParts = _partFilteringService.GetFilteredParts(product.Parts);

            var details = new
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductNumber = product.ProductNumber,
                CarcassParts = carcassParts.Select(p => new
                {
                    Name = p.Name,
                    Status = p.Status.ToString(),
                    Location = p.Location ?? "Unknown",
                    Quantity = p.Qty,
                    Dimensions = $"{p.Length}mm x {p.Width}mm x {p.Thickness}mm"
                }).ToList(),
                FilteredParts = filteredParts.Select(p => new
                {
                    Name = p.Name,
                    Category = _partFilteringService.ClassifyPart(p).ToString(),
                    Status = p.Status.ToString(),
                    Location = p.Location ?? "Unknown",
                    Quantity = p.Qty
                }).ToList()
            };

            return Json(new { success = true, data = details });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product details for {ProductId}", productId);
            return Json(new { success = false, message = "Error loading product details" });
        }
    }
}

// Data models for the assembly dashboard
public class AssemblyDashboardData
{
    public WorkOrder WorkOrder { get; set; } = null!;
    public List<string> ReadyProductIds { get; set; } = new();
    public List<StorageRack> StorageRacks { get; set; } = new();
    public List<ProductAssemblyStatus> Products { get; set; } = new();
    public List<FilteredPartLocation> FilteredParts { get; set; } = new();
}

public class ProductAssemblyStatus
{
    public Product Product { get; set; } = null!;
    public int CarcassPartsCount { get; set; }
    public int SortedCarcassPartsCount { get; set; }
    public int FilteredPartsCount { get; set; }
    public int SortedFilteredPartsCount { get; set; }
    public bool IsReadyForAssembly { get; set; }
    public bool IsCompleted { get; set; }
    public int CompletionPercentage { get; set; }
    public List<PartLocation> PartLocations { get; set; } = new();
}

public class PartLocation
{
    public string PartName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class FilteredPartLocation
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Quantity { get; set; }
}