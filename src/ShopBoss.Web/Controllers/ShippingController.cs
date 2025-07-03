using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Services;
using ShopBoss.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ShopBoss.Web.Controllers;

public class ShippingController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly ShippingService _shippingService;
    private readonly AuditTrailService _auditTrailService;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly ILogger<ShippingController> _logger;

    public ShippingController(
        ShopBossDbContext context,
        ShippingService shippingService,
        AuditTrailService auditTrailService,
        IHubContext<StatusHub> hubContext,
        ILogger<ShippingController> logger)
    {
        _context = context;
        _shippingService = shippingService;
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

        var dashboardData = await _shippingService.GetShippingDashboardDataAsync(activeWorkOrderId);
        
        if (dashboardData.WorkOrder == null)
        {
            TempData["ErrorMessage"] = "Active work order not found. Please select a valid work order.";
            return View("NoActiveWorkOrder");
        }

        return View(dashboardData);
    }

    [HttpPost]
    public async Task<IActionResult> ScanProduct(string barcode)
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
                station: "Shipping",
                isSuccessful: false, // Will be updated to true if successful
                workOrderId: activeWorkOrderId,
                sessionId: HttpContext.Session.Id,
                details: "Shipping station barcode scan attempt"
            );

            // Find the product by barcode (ID or name)
            var product = await _context.Products
                .Include(p => p.Parts)
                .FirstOrDefaultAsync(p => p.WorkOrderId == activeWorkOrderId && 
                                    (p.Id == barcode || p.Name == barcode || p.ProductNumber == barcode));

            if (product == null)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product with barcode '{barcode}' not found in active work order" 
                });
            }

            // Check if product is ready for shipping (all parts assembled)
            var assembledParts = product.Parts.Where(p => p.Status == PartStatus.Assembled).ToList();
            var totalParts = product.Parts.Count;
            
            if (assembledParts.Count == 0)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' has no assembled parts. Cannot ship." 
                });
            }

            if (assembledParts.Count < totalParts)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' is not fully assembled. Only {assembledParts.Count}/{totalParts} parts are assembled." 
                });
            }

            // Check if product is already shipped
            var alreadyShipped = product.Parts.All(p => p.Status == PartStatus.Shipped);
            if (alreadyShipped)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' is already shipped" 
                });
            }

            // Mark all parts as shipped
            var shippedParts = 0;
            foreach (var part in product.Parts)
            {
                if (part.Status == PartStatus.Assembled)
                {
                    part.Status = PartStatus.Shipped;
                    part.StatusUpdatedDate = DateTime.UtcNow;
                    shippedParts++;

                    // Log the status change
                    await _auditTrailService.LogAsync(
                        action: "PartScannedForShipping",
                        entityType: "Part",
                        entityId: part.Id,
                        oldValue: "Assembled",
                        newValue: "Shipped",
                        station: "Shipping",
                        workOrderId: activeWorkOrderId,
                        details: $"Part status changed from Assembled to Shipped via barcode scan (scanned: {barcode})",
                        sessionId: HttpContext.Session.Id
                    );
                }
            }

            await _context.SaveChangesAsync();

            // Check if work order is now fully shipped
            var workOrderFullyShipped = await IsWorkOrderFullyShippedAsync(activeWorkOrderId);
            var shippedProductCount = await GetShippedProductCountAsync(activeWorkOrderId);

            // Send SignalR notifications to all stations
            var shippingCompletionData = new
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ScannedBarcode = barcode,
                ShippedPartsCount = shippedParts,
                WorkOrderId = activeWorkOrderId,
                Timestamp = DateTime.UtcNow,
                Status = "Shipped",
                ShippedProductCount = shippedProductCount,
                IsWorkOrderFullyShipped = workOrderFullyShipped
            };

            // Notify all stations about the shipping completion
            await _hubContext.Clients.Group($"WorkOrder_{activeWorkOrderId}")
                .SendAsync("ProductShippedByScan", shippingCompletionData);

            // Send specific notifications to each station
            await _hubContext.Clients.Group("assembly-station")
                .SendAsync("ProductShippedFromShipping", shippingCompletionData);
            
            await _hubContext.Clients.Group("admin-station")
                .SendAsync("ProductShipped", new
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    WorkOrderId = activeWorkOrderId,
                    ShippedProductCount = shippedProductCount,
                    IsWorkOrderFullyShipped = workOrderFullyShipped,
                    Timestamp = DateTime.UtcNow
                });
            
            await _hubContext.Clients.Group("all-stations")
                .SendAsync("StatusUpdate", new
                {
                    type = "product-shipped",
                    productId = product.Id,
                    productName = product.Name,
                    workOrderId = activeWorkOrderId,
                    station = "Shipping",
                    timestamp = DateTime.UtcNow,
                    shippedProductCount = shippedProductCount,
                    isWorkOrderFullyShipped = workOrderFullyShipped,
                    message = $"Product '{product.Name}' has been shipped"
                });

            // Log successful scan
            await _auditTrailService.LogScanAsync(
                barcode: barcode,
                station: "Shipping",
                isSuccessful: true,
                workOrderId: activeWorkOrderId,
                partsProcessed: shippedParts,
                sessionId: HttpContext.Session.Id,
                details: $"Shipping completed for product '{product.Name}' - {shippedParts} parts marked as Shipped"
            );

            _logger.LogInformation("Product {ProductId} ({ProductName}) shipped via barcode scan '{Barcode}' - {ShippedParts} parts marked as Shipped",
                product.Id, product.Name, barcode, shippedParts);

            return Json(new { 
                success = true, 
                message = $"✅ Product '{product.Name}' shipped successfully!",
                productId = product.Id,
                productName = product.Name,
                shippedPartsCount = shippedParts,
                isWorkOrderFullyShipped = workOrderFullyShipped
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing barcode scan for shipping: {Barcode}", barcode);
            return Json(new { success = false, message = "An error occurred while processing the scan" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ScanHardware(string barcode)
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

            // Find the hardware item by barcode (ID or name)
            var hardware = await _context.Hardware
                .FirstOrDefaultAsync(h => h.WorkOrderId == activeWorkOrderId && 
                                    (h.Id == barcode || h.Name == barcode));

            if (hardware == null)
            {
                return Json(new { 
                    success = false, 
                    message = $"Hardware item with barcode '{barcode}' not found in active work order" 
                });
            }

            // Hardware items are considered always ready to ship
            // For this implementation, we'll track shipping in the audit log only

            await _context.SaveChangesAsync();

            // Log the status change
            await _auditTrailService.LogAsync(
                action: "HardwareScannedForShipping",
                entityType: "Hardware",
                entityId: hardware.Id,
                oldValue: "Pending",
                newValue: "Shipped",
                station: "Shipping",
                workOrderId: activeWorkOrderId,
                details: $"Hardware status changed to Shipped via barcode scan (scanned: {barcode})",
                sessionId: HttpContext.Session.Id
            );

            _logger.LogInformation("Hardware {HardwareId} ({HardwareName}) shipped via barcode scan '{Barcode}'",
                hardware.Id, hardware.Name, barcode);

            return Json(new { 
                success = true, 
                message = $"✅ Hardware '{hardware.Name}' shipped successfully!",
                hardwareId = hardware.Id,
                hardwareName = hardware.Name,
                quantity = hardware.Qty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing hardware barcode scan for shipping: {Barcode}", barcode);
            return Json(new { success = false, message = "An error occurred while processing the scan" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ScanDetachedProduct(string barcode)
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

            // Find the detached product by barcode (ID or name)
            var detachedProduct = await _context.DetachedProducts
                .FirstOrDefaultAsync(d => d.WorkOrderId == activeWorkOrderId && 
                                    (d.Id == barcode || d.Name == barcode || d.ProductNumber == barcode));

            if (detachedProduct == null)
            {
                return Json(new { 
                    success = false, 
                    message = $"Detached product with barcode '{barcode}' not found in active work order" 
                });
            }

            // Detached products are considered always ready to ship
            // For this implementation, we'll track shipping in the audit log only

            await _context.SaveChangesAsync();

            // Log the status change
            await _auditTrailService.LogAsync(
                action: "DetachedProductScannedForShipping",
                entityType: "DetachedProduct",
                entityId: detachedProduct.Id,
                oldValue: "Pending",
                newValue: "Shipped",
                station: "Shipping",
                workOrderId: activeWorkOrderId,
                details: $"Detached product status changed to Shipped via barcode scan (scanned: {barcode})",
                sessionId: HttpContext.Session.Id
            );

            _logger.LogInformation("Detached product {DetachedProductId} ({DetachedProductName}) shipped via barcode scan '{Barcode}'",
                detachedProduct.Id, detachedProduct.Name, barcode);

            return Json(new { 
                success = true, 
                message = $"✅ Detached product '{detachedProduct.Name}' shipped successfully!",
                detachedProductId = detachedProduct.Id,
                detachedProductName = detachedProduct.Name,
                quantity = detachedProduct.Qty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing detached product barcode scan for shipping: {Barcode}", barcode);
            return Json(new { success = false, message = "An error occurred while processing the scan" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetShippingProgress()
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected" });
            }

            var dashboardData = await _shippingService.GetShippingDashboardDataAsync(activeWorkOrderId);
            
            var shippedProducts = dashboardData.Products.Count(p => p.IsShipped);
            var totalProducts = dashboardData.Products.Count;
            
            // For this implementation, hardware and detached products are always considered shipped
            var shippedHardware = dashboardData.Hardware.Count;
            var totalHardware = dashboardData.Hardware.Count;
            
            var shippedDetachedProducts = dashboardData.DetachedProducts.Count;
            var totalDetachedProducts = dashboardData.DetachedProducts.Count;

            return Json(new
            {
                success = true,
                data = new
                {
                    Products = new { Shipped = shippedProducts, Total = totalProducts },
                    Hardware = new { Shipped = shippedHardware, Total = totalHardware },
                    DetachedProducts = new { Shipped = shippedDetachedProducts, Total = totalDetachedProducts },
                    IsWorkOrderFullyShipped = await IsWorkOrderFullyShippedAsync(activeWorkOrderId)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping progress");
            return Json(new { success = false, message = "Error loading shipping progress" });
        }
    }

    private async Task<bool> IsWorkOrderFullyShippedAsync(string workOrderId)
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
                return false;
            }

            // Check if all products are shipped
            var allProductsShipped = workOrder.Products.All(p => 
                p.Parts.Any() && p.Parts.All(part => part.Status == PartStatus.Shipped));

            // For this implementation, hardware and detached products are always considered ready/shipped
            // In a full implementation, you would add status tracking to these entities
            var allHardwareShipped = true;
            var allDetachedProductsShipped = true;

            return allProductsShipped && allHardwareShipped && allDetachedProductsShipped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if work order {WorkOrderId} is fully shipped", workOrderId);
            return false;
        }
    }

    private async Task<int> GetShippedProductCountAsync(string workOrderId)
    {
        try
        {
            var products = await _context.Products
                .Include(p => p.Parts)
                .Where(p => p.WorkOrderId == workOrderId)
                .ToListAsync();

            return products.Count(p => p.Parts.Any() && p.Parts.All(part => part.Status == PartStatus.Shipped));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipped product count for work order {WorkOrderId}", workOrderId);
            return 0;
        }
    }
}