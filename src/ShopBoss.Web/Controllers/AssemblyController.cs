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
    private readonly WorkOrderService _workOrderService;
    private readonly SortingRuleService _sortingRuleService;
    private readonly AuditTrailService _auditTrailService;
    private readonly ShippingService _shippingService;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly ILogger<AssemblyController> _logger;

    public AssemblyController(
        ShopBossDbContext context,
        WorkOrderService workOrderService,
        SortingRuleService sortingRuleService,
        AuditTrailService auditTrailService,
        ShippingService shippingService,
        IHubContext<StatusHub> hubContext,
        ILogger<AssemblyController> logger)
    {
        _context = context;
        _workOrderService = workOrderService;
        _sortingRuleService = sortingRuleService;
        _auditTrailService = auditTrailService;
        _shippingService = shippingService;
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

        // Use optimized data loading to eliminate cartesian products
        var assemblyStationData = await _workOrderService.GetAssemblyStationDataAsync(activeWorkOrderId);

        if (assemblyStationData.WorkOrder == null)
        {
            TempData["ErrorMessage"] = "Active work order not found. Please select a valid work order.";
            return View("NoActiveWorkOrder");
        }

        // Get assembly readiness information
        var readyProductIds = await _sortingRuleService.CheckAssemblyReadinessAsync(activeWorkOrderId);
        

        // Prepare assembly readiness data using optimized data
        var assemblyData = new AssemblyDashboardData
        {
            WorkOrder = assemblyStationData.WorkOrder,
            ReadyProductIds = readyProductIds,
            Products = GetProductsWithAssemblyStatusOptimized(assemblyStationData.Products, assemblyStationData.Parts, readyProductIds)
        };

        return View(assemblyData);
    }

    private List<ProductAssemblyStatus> GetProductsWithAssemblyStatus(
        List<Product> products, 
        List<string> readyProductIds)
    {
        var result = new List<ProductAssemblyStatus>();

        foreach (var product in products)
        {
            var standardParts = product.Parts.Where(p => p.Category == PartCategory.Standard).ToList();
            var filteredParts = product.Parts.Where(p => p.Category != PartCategory.Standard).ToList();
            
            var sortedStandardParts = standardParts.Count(p => p.Status == PartStatus.Sorted);
            var totalStandardParts = standardParts.Count;
            var sortedFilteredParts = filteredParts.Count(p => p.Status == PartStatus.Sorted);
            var totalFilteredParts = filteredParts.Count;
            
            var isReady = readyProductIds.Contains(product.Id);
            var isCompleted = product.Status == PartStatus.Assembled;

            // Get simplified rack locations - just standard bin and doors/fronts bin
            var standardLocation = standardParts
                .Where(p => p.Status == PartStatus.Sorted && !string.IsNullOrEmpty(p.Location))
                .Select(p => p.Location)
                .FirstOrDefault();
                
            var doorsLocation = filteredParts
                .Where(p => p.Status == PartStatus.Sorted && !string.IsNullOrEmpty(p.Location))
                .Select(p => p.Location)
                .FirstOrDefault();

            var partLocations = new List<PartLocation>();
            if (!string.IsNullOrEmpty(standardLocation))
            {
                partLocations.Add(new PartLocation
                {
                    PartName = "Standard Parts",
                    Location = standardLocation,
                    Quantity = sortedStandardParts
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
                StandardPartsCount = totalStandardParts,
                SortedStandardPartsCount = sortedStandardParts,
                FilteredPartsCount = totalFilteredParts,
                SortedFilteredPartsCount = sortedFilteredParts,
                IsReadyForAssembly = isReady,
                IsCompleted = isCompleted,
                CompletionPercentage = totalStandardParts > 0 ? (int)((double)sortedStandardParts / totalStandardParts * 100) : 0,
                PartLocations = partLocations
            });
        }

        return result.OrderByDescending(p => p.IsReadyForAssembly)
                    .ThenByDescending(p => p.CompletionPercentage)
                    .ToList();
    }

    private List<ProductAssemblyStatus> GetProductsWithAssemblyStatusOptimized(
        List<Product> products,
        List<PartSummary> allParts,
        List<string> readyProductIds)
    {
        var result = new List<ProductAssemblyStatus>();

        foreach (var product in products)
        {
            // Get parts for this product from the optimized part list
            var productParts = allParts.Where(p => p.ProductId == product.Id).ToList();
            
            // Convert PartSummary to Part objects for filtering service compatibility
            var convertedParts = productParts.Select(ps => new Part
            {
                Id = ps.Id,
                Name = ps.Name,
                Status = ps.Status,
                Qty = ps.Qty,
                Location = ps.Location,
                Length = ps.Length,
                Width = ps.Width,
                Thickness = ps.Thickness,
                Material = ps.Material,
                Category = ps.Category
            }).ToList();

            var standardParts = convertedParts.Where(p => p.Category == PartCategory.Standard).ToList();
            var filteredParts = convertedParts.Where(p => p.Category != PartCategory.Standard).ToList();
            
            var sortedStandardParts = standardParts.Count(p => p.Status == PartStatus.Sorted);
            var totalStandardParts = standardParts.Count;
            var sortedFilteredParts = filteredParts.Count(p => p.Status == PartStatus.Sorted);
            var totalFilteredParts = filteredParts.Count;
            
            var isReady = readyProductIds.Contains(product.Id);
            var isCompleted = product.Status == PartStatus.Assembled;

            // Get simplified rack locations - just standard bin and doors/fronts bin
            var standardLocation = standardParts
                .Where(p => p.Status == PartStatus.Sorted && !string.IsNullOrEmpty(p.Location))
                .Select(p => p.Location)
                .FirstOrDefault();
                
            var doorsLocation = filteredParts
                .Where(p => p.Status == PartStatus.Sorted && !string.IsNullOrEmpty(p.Location))
                .Select(p => p.Location)
                .FirstOrDefault();

            var partLocations = new List<PartLocation>();
            if (!string.IsNullOrEmpty(standardLocation))
            {
                partLocations.Add(new PartLocation
                {
                    PartName = "Standard Parts",
                    Location = standardLocation,
                    Quantity = sortedStandardParts
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
                StandardPartsCount = totalStandardParts,
                SortedStandardPartsCount = sortedStandardParts,
                FilteredPartsCount = totalFilteredParts,
                SortedFilteredPartsCount = sortedFilteredParts,
                IsReadyForAssembly = isReady,
                IsCompleted = isCompleted,
                CompletionPercentage = totalStandardParts > 0 ? (int)((double)sortedStandardParts / totalStandardParts * 100) : 0,
                PartLocations = partLocations
            });
        }

        return result.OrderByDescending(p => p.IsReadyForAssembly)
                    .ThenByDescending(p => p.CompletionPercentage)
                    .ToList();
    }

    private List<FilteredPartLocation> GetFilteredPartsLocations(List<Product> products)
    {
        var result = new List<FilteredPartLocation>();

        foreach (var product in products)
        {
            var filteredParts = product.Parts.Where(p => p.Category != PartCategory.Standard).ToList();
            
            foreach (var part in filteredParts)
            {
                var category = part.Category;
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

            // Log the scan attempt
            await _auditTrailService.LogScanAsync(
                barcode: barcode,
                station: "Assembly",
                isSuccessful: false, // Will be updated to true if successful
                workOrderId: activeWorkOrderId,
                sessionId: HttpContext.Session.Id,
                details: "Assembly station barcode scan attempt"
            );

            // Find the part by barcode in the active work order (same logic as Sorting station)
            var part = await _context.Parts
                .Include(p => p.Product)
                    .ThenInclude(pr => pr.Parts)
                .Include(p => p.NestSheet)
                .FirstOrDefaultAsync(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && 
                                    (EF.Functions.Collate(p.Id, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Collate(p.Name, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Like(EF.Functions.Collate(p.Id, "NOCASE"), EF.Functions.Collate(barcode + "_%", "NOCASE"))) &&
                                    p.Product.Status != PartStatus.Assembled);

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
            
            var standardParts = product.Parts.Where(p => p.Category == PartCategory.Standard).ToList();
            var allStandardPartsSorted = standardParts.All(p => p.Status == PartStatus.Sorted);
            

            if (!allStandardPartsSorted)
            {
                var sortedCount = standardParts.Count(p => p.Status == PartStatus.Sorted);
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' is not ready for assembly. Only {sortedCount}/{standardParts.Count} standard parts are sorted." 
                });
            }

            // Check if product is already assembled
            var alreadyAssembled = standardParts.All(p => p.Status == PartStatus.Assembled);
            if (alreadyAssembled)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' is already assembled" 
                });
            }

            // Mark all standard parts as assembled and empty their bins
            var assembledParts = 0;
            var binsToEmpty = new HashSet<string>();
            
            foreach (var standardPart in standardParts)
            {
                if (standardPart.Status == PartStatus.Sorted)
                {
                    // Track bin location for emptying
                    if (!string.IsNullOrEmpty(standardPart.Location))
                    {
                        binsToEmpty.Add(standardPart.Location);
                    }
                    
                    standardPart.Status = PartStatus.Assembled;
                    standardPart.StatusUpdatedDate = DateTime.UtcNow;
                    standardPart.Location = null; // Clear location since part is no longer in bin
                    assembledParts++;

                    // Log the status change
                    await _auditTrailService.LogAsync(
                        action: "PartScannedForAssembly",
                        entityType: "Part",
                        entityId: standardPart.Id,
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: $"Part status changed from Sorted to Assembled via barcode scan (scanned: {barcode})",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }

            // Get filtered parts locations for guidance BEFORE marking them as assembled
            var filteredPartsForGuidance = product.Parts.Where(p => p.Category != PartCategory.Standard).ToList();
            var filteredPartsGuidance = filteredPartsForGuidance
                .Where(fp => fp.Status == PartStatus.Sorted)
                .Select(fp => new
                {
                    Name = fp.Name,
                    Category = fp.Category.ToString(),
                    Location = fp.Location ?? "Unknown Location",
                    Quantity = fp.Qty
                })
                .ToList();

            // Mark filtered parts as assembled and track their bins for emptying
            var filteredParts = product.Parts.Where(p => p.Category != PartCategory.Standard).ToList();
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
                        action: "PartScannedForAssembly",
                        entityType: "Part",
                        entityId: filteredPart.Id,
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: $"Filtered part status changed from Sorted to Assembled via barcode scan (scanned: {barcode})",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }
            
            // Mark product as assembled (ready for shipping)
            product.Status = PartStatus.Assembled;
            product.StatusUpdatedDate = DateTime.UtcNow;
            
            // Log product status change
            await _auditTrailService.LogAsync(
                action: "ProductAssembled",
                entityType: "Product",
                entityId: product.Id,
                oldValue: "Pending",
                newValue: "Shipped", 
                station: "Assembly",
                workOrderId: activeWorkOrderId,
                details: $"Product {product.Name} completed assembly via barcode scan (scanned: {barcode})",
                sessionId: HttpContext.Session.Id
            );

            // Empty the bins that contained the assembled parts
            foreach (var binLocation in binsToEmpty)
            {
                // We know these bins should be empty now, so clear them unconditionally
                // (same approach as the Clear Bin button in Sorting Station)
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
                        action: "BinEmptied",
                        entityType: "StorageBin",
                        entityId: bin.Id,
                        oldValue: "Occupied",
                        newValue: "Empty",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: $"Bin emptied after assembly of product {product.Name}",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }

            await _context.SaveChangesAsync();

            // Check if work order is now ready for shipping
            var isWorkOrderReadyForShipping = await _shippingService.IsWorkOrderReadyForShippingAsync(activeWorkOrderId);
            var readyForShippingProducts = await _shippingService.GetProductsReadyForShippingAsync(activeWorkOrderId);

            // Send single consolidated SignalR notification to avoid duplicates
            var assemblyCompletionData = new
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ScannedPartName = part.Name,
                ScannedBarcode = barcode,
                AssembledPartsCount = assembledParts,
                FilteredPartsCount = filteredPartsGuidance.Count,
                WorkOrderId = activeWorkOrderId,
                Timestamp = DateTime.UtcNow,
                Status = "Assembled",
                IsReadyForShipping = false,
                ReadyForShippingProducts = readyForShippingProducts,
                IsWorkOrderReadyForShipping = isWorkOrderReadyForShipping,
                Station = "Assembly",
                Message = $"Product '{product.Name}' has been assembled"
            };

            // Send a single notification to all stations to avoid duplicates
            await _hubContext.Clients.Group("all-stations")
                .SendAsync("ProductAssembledByScan", assemblyCompletionData);

            // Log successful scan
            await _auditTrailService.LogScanAsync(
                barcode: barcode,
                station: "Assembly",
                isSuccessful: true,
                workOrderId: activeWorkOrderId,
                partsProcessed: assembledParts,
                sessionId: HttpContext.Session.Id,
                details: $"Assembly completed for product '{product.Name}' - {assembledParts} parts marked as Assembled"
            );

            _logger.LogInformation("Product {ProductId} ({ProductName}) assembled via barcode scan '{Barcode}' - {AssembledParts} standard parts marked as Assembled",
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
            var standardParts = product.Parts.Where(p => p.Category == PartCategory.Standard).ToList();
            var allStandardPartsSorted = standardParts.All(p => p.Status == PartStatus.Sorted);

            if (!allStandardPartsSorted)
            {
                return Json(new { 
                    success = false, 
                    message = "Not all standard parts are sorted. Cannot start assembly." 
                });
            }

            // Mark all standard parts as "Assembled" and empty their bins
            var updatedParts = 0;
            var binsToEmpty = new HashSet<string>();
            
            foreach (var part in standardParts)
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
                        action: "StatusUpdate",
                        entityType: "Part",
                        entityId: part.Id,
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: $"Part status changed from Sorted to Assembled for product {product.Name}",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }

            // Mark filtered parts as assembled and track their bins for emptying
            var filteredParts = product.Parts.Where(p => p.Category != PartCategory.Standard).ToList();
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
                        action: "StatusUpdate",
                        entityType: "Part",
                        entityId: filteredPart.Id,
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: $"Filtered part status changed from Sorted to Assembled for product {product.Name}",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }
            
            // Mark product as assembled (ready for shipping)
            product.Status = PartStatus.Assembled;
            product.StatusUpdatedDate = DateTime.UtcNow;
            
            // Log product status change
            await _auditTrailService.LogAsync(
                action: "ProductAssembled",
                entityType: "Product",
                entityId: product.Id,
                oldValue: "Pending",
                newValue: "Shipped",
                station: "Assembly", 
                workOrderId: activeWorkOrderId,
                details: $"Product {product.Name} completed assembly manually",
                sessionId: HttpContext.Session.Id
            );

            // Empty the bins that contained the assembled parts
            foreach (var binLocation in binsToEmpty)
            {
                // We know these bins should be empty now, so clear them unconditionally
                // (same approach as the Clear Bin button in Sorting Station)
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
                        action: "BinEmptied",
                        entityType: "StorageBin",
                        entityId: bin.Id,
                        oldValue: "Occupied",
                        newValue: "Empty",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: $"Bin emptied after manual assembly of product {product.Name}",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }

            await _context.SaveChangesAsync();

            // Check if work order is now ready for shipping
            var isWorkOrderReadyForShipping = await _shippingService.IsWorkOrderReadyForShippingAsync(activeWorkOrderId);
            var readyForShippingProducts = await _shippingService.GetProductsReadyForShippingAsync(activeWorkOrderId);

            // Send single consolidated SignalR notification to avoid duplicates
            var assemblyCompletionData = new
            {
                ProductId = productId,
                ProductName = product.Name,
                PartsAssembled = updatedParts,
                WorkOrderId = activeWorkOrderId,
                Timestamp = DateTime.UtcNow,
                Status = "Assembled",
                IsReadyForShipping = false,
                ReadyForShippingProducts = readyForShippingProducts,
                IsWorkOrderReadyForShipping = isWorkOrderReadyForShipping,
                Station = "Assembly",
                Message = $"Product '{product.Name}' has been assembled"
            };

            // Send a single notification to all stations to avoid duplicates
            await _hubContext.Clients.Group("all-stations")
                .SendAsync("ProductAssembledManually", assemblyCompletionData);

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

            var standardParts = product.Parts.Where(p => p.Category == PartCategory.Standard).ToList();
            var filteredParts = product.Parts.Where(p => p.Category != PartCategory.Standard).ToList();

            var details = new
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ItemNumber = product.ItemNumber,
                StandardParts = standardParts.Select(p => new
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
                    Category = p.Category.ToString(),
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
    public List<ProductAssemblyStatus> Products { get; set; } = new();
}

public class ProductAssemblyStatus
{
    public Product Product { get; set; } = null!;
    public int StandardPartsCount { get; set; }
    public int SortedStandardPartsCount { get; set; }
    public int FilteredPartsCount { get; set; }
    public int SortedFilteredPartsCount { get; set; }
    public bool IsReadyForAssembly { get; set; }
    public bool IsCompleted { get; set; } // Computed from Product.Status == "Assembled"
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