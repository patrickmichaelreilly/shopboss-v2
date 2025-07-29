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
            
            var standardParts = productParts.Where(p => p.Category == PartCategory.Standard).ToList();
            var filteredParts = productParts.Where(p => p.Category != PartCategory.Standard).ToList();
            
            var sortedStandardParts = standardParts.Count(p => p.Status == PartStatus.Sorted);
            var totalStandardParts = standardParts.Count;
            var sortedFilteredParts = filteredParts.Count(p => p.Status == PartStatus.Sorted);
            var totalFilteredParts = filteredParts.Count;
            
            var isReady = readyProductIds.Contains(product.Id);
            var isCompleted = product.Status == PartStatus.Assembled;

            // Get simplified rack locations using new bin system - no more string splitting
            var standardBinInfo = standardParts
                .Where(ps => ps.Status == PartStatus.Sorted && !string.IsNullOrEmpty(ps.BinId))
                .Select(ps => new { ps.BinLabel, ps.RackName })
                .FirstOrDefault();
                
            var doorsBinInfo = filteredParts
                .Where(ps => ps.Status == PartStatus.Sorted && !string.IsNullOrEmpty(ps.BinId))
                .Select(ps => new { ps.BinLabel, ps.RackName })
                .FirstOrDefault();

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
                StandardBinLabel = standardBinInfo?.BinLabel ?? "-",
                FilteredBinLabel = doorsBinInfo?.BinLabel ?? (totalFilteredParts > 0 ? "-" : null)
            });
        }

        return result.OrderBy(p => p.Product.ItemNumber).ToList();
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


    private async Task<(bool success, string message, object? data)> ProcessProductAssemblyAsync(string productId, string? scannedBarcode = null, string? scannedPartName = null)
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return (false, "No active work order selected", null);
            }

            // Get the product with all its parts
            var product = await _context.Products
                .Include(p => p.Parts)
                .FirstOrDefaultAsync(p => p.Id == productId && p.WorkOrderId == activeWorkOrderId);
                
            if (product == null)
            {
                return (false, "Product not found in active work order", null);
            }

            var standardParts = product.Parts.Where(p => p.Category == PartCategory.Standard).ToList();
            var allStandardPartsSorted = standardParts.All(p => p.Status == PartStatus.Sorted);
            
            if (!allStandardPartsSorted)
            {
                var sortedCount = standardParts.Count(p => p.Status == PartStatus.Sorted);
                return (false, $"Product '{product.Name}' is not ready for assembly. Only {sortedCount}/{standardParts.Count} standard parts are sorted.", null);
            }

            // Check if product is already assembled
            var alreadyAssembled = standardParts.All(p => p.Status == PartStatus.Assembled);
            if (alreadyAssembled)
            {
                return (false, $"Product '{product.Name}' is already assembled", null);
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

            // Mark all standard parts as assembled and empty their bins
            var assembledParts = 0;
            var binsToEmpty = new HashSet<string>();
            
            foreach (var standardPart in standardParts)
            {
                if (standardPart.Status == PartStatus.Sorted)
                {
                    // Track bin ID for emptying - use direct BinId reference
                    if (!string.IsNullOrEmpty(standardPart.BinId))
                    {
                        binsToEmpty.Add(standardPart.BinId);
                    }
                    
                    standardPart.Status = PartStatus.Assembled;
                    standardPart.StatusUpdatedDate = DateTime.Now;
                    standardPart.BinId = null; // Clear bin reference since part is no longer in bin
                    standardPart.Location = null; // Clear location since part is no longer in bin
                    assembledParts++;

                    // Log the status change
                    var action = !string.IsNullOrEmpty(scannedBarcode) ? "PartScannedForAssembly" : "StatusUpdate";
                    var details = !string.IsNullOrEmpty(scannedBarcode) 
                        ? $"Part status changed from Sorted to Assembled via barcode scan (scanned: {scannedBarcode})"
                        : $"Part status changed from Sorted to Assembled for product {product.Name}";
                        
                    await _auditTrailService.LogAsync(
                        action: action,
                        entityType: "Part",
                        entityId: standardPart.Id,
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: details,
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
                    // Track bin ID for emptying - use direct BinId reference
                    if (!string.IsNullOrEmpty(filteredPart.BinId))
                    {
                        binsToEmpty.Add(filteredPart.BinId);
                    }
                    
                    filteredPart.Status = PartStatus.Assembled;
                    filteredPart.StatusUpdatedDate = DateTime.Now;
                    filteredPart.BinId = null; // Clear bin reference since part is no longer in bin
                    filteredPart.Location = null;
                    assembledParts++;

                    // Log the status change
                    var action = !string.IsNullOrEmpty(scannedBarcode) ? "PartScannedForAssembly" : "StatusUpdate";
                    var details = !string.IsNullOrEmpty(scannedBarcode)
                        ? $"Filtered part status changed from Sorted to Assembled via barcode scan (scanned: {scannedBarcode})"
                        : $"Filtered part status changed from Sorted to Assembled for product {product.Name}";
                        
                    await _auditTrailService.LogAsync(
                        action: action,
                        entityType: "Part",
                        entityId: filteredPart.Id,
                        oldValue: "Sorted",
                        newValue: "Assembled",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: details,
                        sessionId: HttpContext.Session.Id
                    );
                }
            }
            
            // Mark product as assembled (ready for shipping)
            product.Status = PartStatus.Assembled;
            product.StatusUpdatedDate = DateTime.Now;
            
            // Log product status change
            var productDetails = !string.IsNullOrEmpty(scannedBarcode)
                ? $"Product {product.Name} completed assembly via barcode scan (scanned: {scannedBarcode})"
                : $"Product {product.Name} completed assembly manually";
                
            await _auditTrailService.LogAsync(
                action: "ProductAssembled",
                entityType: "Product",
                entityId: product.Id,
                oldValue: "Pending",
                newValue: "Shipped", 
                station: "Assembly",
                workOrderId: activeWorkOrderId,
                details: productDetails,
                sessionId: HttpContext.Session.Id
            );

            // Empty the bins that contained the assembled parts
            foreach (var binId in binsToEmpty)
            {
                // Direct bin lookup using BinId - no more toxic string parsing!
                var bin = await _context.Bins
                    .Include(b => b.StorageRack)
                    .FirstOrDefaultAsync(b => b.Id == binId);
                
                if (bin != null)
                {
                    bin.Status = BinStatus.Empty;
                    bin.ProductId = null;
                    bin.PartId = null;
                    bin.WorkOrderId = null;
                    bin.PartsCount = 0;
                    bin.Contents = string.Empty;
                    bin.LastUpdatedDate = DateTime.Now;
                    
                    var binDetails = !string.IsNullOrEmpty(scannedBarcode)
                        ? $"Bin emptied after assembly of product {product.Name}"
                        : $"Bin emptied after manual assembly of product {product.Name}";
                        
                    await _auditTrailService.LogAsync(
                        action: "BinEmptied",
                        entityType: "StorageBin",
                        entityId: bin.Id,
                        oldValue: "Occupied",
                        newValue: "Empty",
                        station: "Assembly",
                        workOrderId: activeWorkOrderId,
                        details: binDetails,
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
                ScannedPartName = scannedPartName,
                ScannedBarcode = scannedBarcode,
                AssembledPartsCount = assembledParts,
                FilteredPartsCount = filteredPartsGuidance.Count,
                WorkOrderId = activeWorkOrderId,
                Timestamp = DateTime.Now,
                Status = "Assembled",
                IsReadyForShipping = false,
                ReadyForShippingProducts = readyForShippingProducts,
                IsWorkOrderReadyForShipping = isWorkOrderReadyForShipping,
                Station = "Assembly",
                Message = $"Product '{product.Name}' has been assembled"
            };

            // Send appropriate SignalR notification based on assembly method
            var signalRMethod = !string.IsNullOrEmpty(scannedBarcode) ? "ProductAssembledByScan" : "ProductAssembledManually";
            await _hubContext.Clients.Group("all-stations")
                .SendAsync(signalRMethod, assemblyCompletionData);

            // Log successful assembly
            if (!string.IsNullOrEmpty(scannedBarcode))
            {
                await _auditTrailService.LogScanAsync(
                    barcode: scannedBarcode,
                    station: "Assembly",
                    isSuccessful: true,
                    workOrderId: activeWorkOrderId,
                    partsProcessed: assembledParts,
                    sessionId: HttpContext.Session.Id,
                    details: $"Assembly completed for product '{product.Name}' - {assembledParts} parts marked as Assembled"
                );
            }

            var successMessage = !string.IsNullOrEmpty(scannedBarcode)
                ? $"✅ Product '{product.Name}' assembly completed!"
                : $"Product '{product.Name}' assembly completed! {assembledParts} parts marked as assembled.";

            _logger.LogInformation("Product {ProductId} ({ProductName}) assembled - {AssembledParts} parts marked as Assembled (Method: {Method})",
                product.Id, product.Name, assembledParts, !string.IsNullOrEmpty(scannedBarcode) ? "Scan" : "Manual");

            return (true, successMessage, new
            {
                productId = product.Id,
                productName = product.Name,
                assembledPartsCount = assembledParts,
                filteredPartsGuidance = filteredPartsGuidance,
                hasFilteredParts = filteredPartsGuidance.Count > 0,
                partsAssembled = assembledParts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing product assembly for {ProductId} (Scanned: {ScannedBarcode})", productId, scannedBarcode ?? "Manual");
            return (false, "An error occurred while processing the assembly", null);
        }
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

            // Use the common assembly processing method
            var result = await ProcessProductAssemblyAsync(part.Product.Id, barcode, part.Name);
            
            return Json(new { 
                success = result.success, 
                message = result.message,
                productId = result.data?.GetType().GetProperty("productId")?.GetValue(result.data),
                productName = result.data?.GetType().GetProperty("productName")?.GetValue(result.data),
                assembledPartsCount = result.data?.GetType().GetProperty("assembledPartsCount")?.GetValue(result.data),
                filteredPartsGuidance = result.data?.GetType().GetProperty("filteredPartsGuidance")?.GetValue(result.data),
                hasFilteredParts = result.data?.GetType().GetProperty("hasFilteredParts")?.GetValue(result.data)
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
            // Use the common assembly processing method
            var result = await ProcessProductAssemblyAsync(productId);
            
            return Json(new { 
                success = result.success, 
                message = result.message,
                partsAssembled = result.data?.GetType().GetProperty("partsAssembled")?.GetValue(result.data)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting assembly for product {ProductId}", productId);
            return Json(new { success = false, message = "An error occurred while starting assembly" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProductBinLocations(string productId)
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected" });
            }

            // Get live bin location data for the product
            var parts = await _context.Parts
                .Where(p => p.ProductId == productId && p.Product.WorkOrderId == activeWorkOrderId)
                .Include(p => p.Bin)
                    .ThenInclude(b => b.StorageRack)
                .ToListAsync();

            var standardParts = parts.Where(p => p.Category == PartCategory.Standard && p.Status == PartStatus.Sorted).ToList();
            var filteredParts = parts.Where(p => p.Category != PartCategory.Standard && p.Status == PartStatus.Sorted).ToList();

            var standardBinLabel = standardParts.FirstOrDefault(p => !string.IsNullOrEmpty(p.BinId))?.Bin?.BinLabel ?? "-";
            var filteredBinLabel = filteredParts.FirstOrDefault(p => !string.IsNullOrEmpty(p.BinId))?.Bin?.BinLabel ?? "-";

            return Json(new { 
                success = true, 
                standardBin = standardBinLabel,
                filteredBin = filteredBinLabel
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product bin locations for {ProductId}", productId);
            return Json(new { success = false, message = "Error loading bin locations" });
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
    public string StandardBinLabel { get; set; } = "-";
    public string? FilteredBinLabel { get; set; }
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