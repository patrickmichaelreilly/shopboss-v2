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
    private readonly WorkOrderService _workOrderService;
    private readonly ShippingService _shippingService;
    private readonly HardwareGroupingService _hardwareGroupingService;
    private readonly AuditTrailService _auditTrailService;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly ILogger<ShippingController> _logger;

    public ShippingController(
        ShopBossDbContext context,
        WorkOrderService workOrderService,
        ShippingService shippingService,
        HardwareGroupingService hardwareGroupingService,
        AuditTrailService auditTrailService,
        IHubContext<StatusHub> hubContext,
        ILogger<ShippingController> logger)
    {
        _context = context;
        _workOrderService = workOrderService;
        _shippingService = shippingService;
        _hardwareGroupingService = hardwareGroupingService;
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

        // Get the shipping station data directly from WorkOrderService
        var shippingStationData = await _workOrderService.GetShippingStationDataAsync(activeWorkOrderId);
        
        if (shippingStationData.WorkOrder == null)
        {
            TempData["ErrorMessage"] = "Active work order not found. Please select a valid work order.";
            return View("NoActiveWorkOrder");
        }

        // Build the simplified dashboard data
        var readyProductIds = await _shippingService.GetProductsReadyForShippingAsync(activeWorkOrderId);
        var shippedProductIds = await _shippingService.GetProductsShippedAsync(activeWorkOrderId);
        var groupedHardware = _hardwareGroupingService.GroupHardwareByName(shippingStationData.Hardware);

        var dashboardData = new ShippingDashboardData
        {
            WorkOrder = shippingStationData.WorkOrder,
            ReadyProductIds = readyProductIds,
            ShippedProductIds = shippedProductIds,
            Products = shippingStationData.Products,
            Hardware = shippingStationData.Hardware.Select(h => new HardwareShippingStatus
            {
                Hardware = h,
                IsShipped = h.Status == PartStatus.Shipped
            }).ToList(),
            GroupedHardware = groupedHardware,
            DetachedProducts = shippingStationData.DetachedProducts.Select(d => new DetachedProductShippingStatus
            {
                DetachedProduct = d,
                IsShipped = d.Status == PartStatus.Shipped
            }).ToList()
        };

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

            // Find the product by barcode (consistent with other scan methods)
            var product = await _context.Products
                .Include(p => p.Parts)
                .FirstOrDefaultAsync(p => p.WorkOrderId == activeWorkOrderId && 
                                    (EF.Functions.Collate(p.Id, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Collate(p.Name, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Collate(p.ItemNumber, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Like(EF.Functions.Collate(p.Id, "NOCASE"), EF.Functions.Collate(barcode + "_%", "NOCASE"))) &&
                                    p.Status != PartStatus.Shipped);

            if (product == null)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product with barcode '{barcode}' not found in active work order" 
                });
            }

            // Check if product is ready for shipping (all parts assembled)
            var assembledParts = product.Parts.Count(p => p.Status == PartStatus.Assembled);
            var totalParts = product.Parts.Count;
            
            if (assembledParts == 0)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' has no assembled parts. Cannot ship." 
                });
            }

            if (assembledParts < totalParts)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' is not fully assembled. Only {assembledParts}/{totalParts} parts are assembled." 
                });
            }

            // Check if product is already shipped (simple status check)
            if (product.Status == PartStatus.Shipped)
            {
                return Json(new { 
                    success = false, 
                    message = $"Product '{product.Name}' is already shipped" 
                });
            }

            // Load and update parts for shipping (separate query to avoid Include chains)
            var partsToShip = await _context.Parts
                .Where(p => p.ProductId == product.Id && p.Status == PartStatus.Assembled)
                .ToListAsync();

            var shippedParts = 0;
            var now = DateTime.Now;
            var auditItems = new List<BatchAuditItem>();
            
            foreach (var part in partsToShip)
            {
                part.Status = PartStatus.Shipped;
                part.StatusUpdatedDate = now;
                shippedParts++;

                // Prepare audit item for batch logging
                auditItems.Add(new BatchAuditItem
                {
                    Action = "PartScannedForShipping",
                    EntityType = "Part",
                    EntityId = part.Id,
                    OldValue = "Assembled",
                    NewValue = "Shipped",
                    Details = $"Part '{part.Name}' status changed from Assembled to Shipped via product barcode scan (scanned: {barcode})"
                });
            }

            // Mark the product itself as shipped
            product.Status = PartStatus.Shipped;
            product.StatusUpdatedDate = now;
            _context.Products.Update(product);

            await _context.SaveChangesAsync();

            // Create batch audit log for all parts shipped (performance optimization)
            if (auditItems.Any())
            {
                await _auditTrailService.LogBatchAsync(
                    items: auditItems,
                    station: "Shipping",
                    workOrderId: activeWorkOrderId,
                    sessionId: HttpContext.Session.Id
                );
            }

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
                Timestamp = DateTime.Now,
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
                    Timestamp = DateTime.Now
                });
            
            await _hubContext.Clients.Group("all-stations")
                .SendAsync("StatusUpdate", new
                {
                    type = "product-shipped",
                    productId = product.Id,
                    productName = product.Name,
                    workOrderId = activeWorkOrderId,
                    station = "Shipping",
                    timestamp = DateTime.Now,
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
    public async Task<IActionResult> ScanPart(string barcode)
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
                details: "Shipping station part barcode scan attempt"
            );

            // Find the part by barcode (ID or name)
            var part = await _context.Parts
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Product.WorkOrderId == activeWorkOrderId &&
                                        (EF.Functions.Collate(p.Id, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                         EF.Functions.Collate(p.Name, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                         EF.Functions.Like(EF.Functions.Collate(p.Id, "NOCASE"), EF.Functions.Collate(barcode + "_%", "NOCASE"))) &&
                                        p.Product.Status != PartStatus.Shipped);

            if (part == null)
            {
                return Json(new { success = false, message = $"Part with barcode '{barcode}' not found in active work order" });
            }

            // Check if part is assembled (prerequisite for shipping)
            if (part.Status != PartStatus.Assembled)
            {
                return Json(new { success = false, message = $"Part '{part.Name}' must be assembled before shipping. Current status: {part.Status}" });
            }

            // Business logic: Scanning any part from a product marks the entire product as shipped
            var product = part.Product;
            
            // Mark ALL parts in the product as shipped
            var allPartsInProduct = await _context.Parts
                .Where(p => p.ProductId == product.Id)
                .ToListAsync();

            var shippedPartsCount = 0;
            foreach (var productPart in allPartsInProduct)
            {
                if (productPart.Status != PartStatus.Shipped)
                {
                    productPart.Status = PartStatus.Shipped;
                    productPart.StatusUpdatedDate = DateTime.Now;
                    shippedPartsCount++;
                }
            }

            await _context.SaveChangesAsync();

            // Log successful scan
            await _auditTrailService.LogScanAsync(
                barcode: barcode,
                station: "Shipping",
                isSuccessful: true,
                workOrderId: activeWorkOrderId,
                partsProcessed: shippedPartsCount,
                sessionId: HttpContext.Session.Id,
                details: $"Product '{product.Name}' shipped via part scan '{part.Name}' - {shippedPartsCount} parts marked as Shipped"
            );

            _logger.LogInformation("Product {ProductId} ({ProductName}) shipped via part barcode scan '{Barcode}' - {ShippedParts} parts marked as Shipped",
                product.Id, product.Name, barcode, shippedPartsCount);

            return Json(new { 
                success = true, 
                message = $"✅ Product '{product.Name}' shipped successfully! ({shippedPartsCount} parts shipped)",
                partId = part.Id,
                partName = part.Name,
                productName = product.Name,
                productFullyShipped = true,
                shippedPartsCount = shippedPartsCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing part barcode scan for shipping: {Barcode}", barcode);
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
                                    (EF.Functions.Collate(h.Id, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Collate(h.Name, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Like(EF.Functions.Collate(h.Id, "NOCASE"), EF.Functions.Collate(barcode + "_%", "NOCASE"))) &&
                                    h.Status != PartStatus.Shipped);

            if (hardware == null)
            {
                return Json(new { 
                    success = false, 
                    message = $"Hardware item with barcode '{barcode}' not found in active work order" 
                });
            }

            // Mark hardware as shipped
            hardware.Status = PartStatus.Shipped;
            hardware.StatusUpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Check if work order is now fully shipped
            var workOrderFullyShipped = await IsWorkOrderFullyShippedAsync(activeWorkOrderId);

            // Send SignalR notifications
            await _hubContext.Clients.Group($"WorkOrder_{activeWorkOrderId}")
                .SendAsync("HardwareShippedByScan", new
                {
                    HardwareId = hardware.Id,
                    HardwareName = hardware.Name,
                    ScannedBarcode = barcode,
                    WorkOrderId = activeWorkOrderId,
                    Timestamp = DateTime.Now,
                    IsWorkOrderFullyShipped = workOrderFullyShipped
                });

            await _hubContext.Clients.Group("all-stations")
                .SendAsync("StatusUpdate", new
                {
                    type = "hardware-shipped",
                    hardwareId = hardware.Id,
                    hardwareName = hardware.Name,
                    workOrderId = activeWorkOrderId,
                    station = "Shipping",
                    timestamp = DateTime.Now,
                    isWorkOrderFullyShipped = workOrderFullyShipped,
                    message = $"Hardware '{hardware.Name}' has been shipped"
                });

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
    public async Task<IActionResult> ShipHardwareGroup(string hardwareIds)
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected" });
            }

            // Validate input
            if (string.IsNullOrWhiteSpace(hardwareIds))
            {
                return Json(new { success = false, message = "No hardware IDs provided" });
            }

            var ids = hardwareIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(id => id.Trim())
                                 .ToList();

            if (!ids.Any())
            {
                return Json(new { success = false, message = "No valid hardware IDs provided" });
            }

            // Use a transaction to prevent database locking issues
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Find all hardware items by IDs in a single query
                var hardwareItems = await _context.Hardware
                    .Where(h => h.WorkOrderId == activeWorkOrderId && ids.Contains(h.Id))
                    .ToListAsync();

                if (!hardwareItems.Any())
                {
                    await transaction.RollbackAsync();
                    return Json(new { 
                        success = false, 
                        message = "No matching hardware items found in active work order" 
                    });
                }

                var shippedItems = new List<object>();
                var groupName = hardwareItems.First().Name; // Group name from first item
                var now = DateTime.Now;
                var auditItems = new List<BatchAuditItem>();

                // Mark all hardware items as shipped in a single operation
                foreach (var hardware in hardwareItems)
                {
                    hardware.Status = PartStatus.Shipped;
                    hardware.StatusUpdatedDate = now;

                    shippedItems.Add(new
                    {
                        id = hardware.Id,
                        name = hardware.Name,
                        quantity = hardware.Qty
                    });

                    // Prepare audit item for batch logging
                    auditItems.Add(new BatchAuditItem
                    {
                        Action = "HardwareGroupShipped",
                        EntityType = "Hardware",
                        EntityId = hardware.Id,
                        OldValue = "Pending",
                        NewValue = "Shipped",
                        Details = $"Hardware '{hardware.Name}' shipped as part of group '{groupName}' (bulk operation)"
                    });
                }

                // Save all changes in a single transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Create batch audit log for all hardware items (performance optimization)
                if (auditItems.Any())
                {
                    await _auditTrailService.LogBatchAsync(
                        items: auditItems,
                        station: "Shipping",
                        workOrderId: activeWorkOrderId,
                        sessionId: HttpContext.Session.Id
                    );
                }

                // Check if work order is now fully shipped
                var workOrderFullyShipped = await IsWorkOrderFullyShippedAsync(activeWorkOrderId);

                // Send SignalR notifications for the group operation
                await _hubContext.Clients.Group($"WorkOrder_{activeWorkOrderId}")
                    .SendAsync("HardwareGroupShipped", new
                    {
                        GroupName = groupName,
                        HardwareItems = shippedItems,
                        WorkOrderId = activeWorkOrderId,
                        Timestamp = DateTime.Now,
                        IsWorkOrderFullyShipped = workOrderFullyShipped
                    });

                await _hubContext.Clients.Group("all-stations")
                    .SendAsync("StatusUpdate", new
                    {
                        type = "hardware-group-shipped",
                        groupName = groupName,
                        itemCount = shippedItems.Count,
                        workOrderId = activeWorkOrderId,
                        station = "Shipping",
                        timestamp = DateTime.Now,
                        isWorkOrderFullyShipped = workOrderFullyShipped,
                        message = $"Hardware group '{groupName}' shipped ({shippedItems.Count} items)"
                    });

                _logger.LogInformation("Hardware group '{GroupName}' shipped - {ItemCount} items processed", 
                    groupName, shippedItems.Count);

                return Json(new { 
                    success = true, 
                    message = $"✅ Hardware group '{groupName}' shipped successfully! ({shippedItems.Count} items)",
                    groupName = groupName,
                    shippedCount = shippedItems.Count,
                    shippedItems = shippedItems
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing hardware group shipping: {HardwareIds}", hardwareIds);
            return Json(new { success = false, message = "An error occurred while processing the group shipping" });
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
                                    (EF.Functions.Collate(d.Id, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Collate(d.Name, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Collate(d.ItemNumber, "NOCASE") == EF.Functions.Collate(barcode, "NOCASE") || 
                                     EF.Functions.Like(EF.Functions.Collate(d.Id, "NOCASE"), EF.Functions.Collate(barcode + "_%", "NOCASE"))) &&
                                    d.Status != PartStatus.Shipped);

            if (detachedProduct == null)
            {
                return Json(new { 
                    success = false, 
                    message = $"Detached product with barcode '{barcode}' not found in active work order" 
                });
            }

            // Mark detached product as shipped
            detachedProduct.Status = PartStatus.Shipped;
            detachedProduct.StatusUpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Check if work order is now fully shipped
            var workOrderFullyShipped = await IsWorkOrderFullyShippedAsync(activeWorkOrderId);

            // Send SignalR notifications
            await _hubContext.Clients.Group($"WorkOrder_{activeWorkOrderId}")
                .SendAsync("DetachedProductShippedByScan", new
                {
                    DetachedProductId = detachedProduct.Id,
                    DetachedProductName = detachedProduct.Name,
                    ScannedBarcode = barcode,
                    WorkOrderId = activeWorkOrderId,
                    Timestamp = DateTime.Now,
                    IsWorkOrderFullyShipped = workOrderFullyShipped
                });

            await _hubContext.Clients.Group("all-stations")
                .SendAsync("StatusUpdate", new
                {
                    type = "detached-product-shipped",
                    detachedProductId = detachedProduct.Id,
                    detachedProductName = detachedProduct.Name,
                    workOrderId = activeWorkOrderId,
                    station = "Shipping",
                    timestamp = DateTime.Now,
                    isWorkOrderFullyShipped = workOrderFullyShipped,
                    message = $"Detached product '{detachedProduct.Name}' has been shipped"
                });

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

            // Get the shipping station data directly
            var shippingStationData = await _workOrderService.GetShippingStationDataAsync(activeWorkOrderId);
            if (shippingStationData.WorkOrder == null)
            {
                return Json(new { success = false, message = "Work order not found" });
            }

            var shippedProducts = shippingStationData.Products.Count(p => p.Status == PartStatus.Shipped);
            var totalProducts = shippingStationData.Products.Count;
            
            // Count actually shipped hardware and detached products
            var shippedHardware = shippingStationData.Hardware.Count(h => h.Status == PartStatus.Shipped);
            var totalHardware = shippingStationData.Hardware.Count;
            
            var shippedDetachedProducts = shippingStationData.DetachedProducts.Count(d => d.Status == PartStatus.Shipped);
            var totalDetachedProducts = shippingStationData.DetachedProducts.Count;

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
            // Optimized approach: Use COUNT queries instead of loading all data into memory
            
            // Check parts (product parts)
            var productPartsQuery = _context.Parts.Where(p => p.Product.WorkOrderId == workOrderId);
            var totalParts = await productPartsQuery.CountAsync();
            if (totalParts > 0)
            {
                var shippedParts = await productPartsQuery.CountAsync(p => p.Status == PartStatus.Shipped);
                if (shippedParts != totalParts)
                {
                    return false;
                }
            }
            
            // Check hardware
            var hardwareQuery = _context.Hardware.Where(h => h.WorkOrderId == workOrderId);
            var totalHardware = await hardwareQuery.CountAsync();
            if (totalHardware > 0)
            {
                var shippedHardware = await hardwareQuery.CountAsync(h => h.Status == PartStatus.Shipped);
                if (shippedHardware != totalHardware)
                {
                    return false;
                }
            }
            
            // Check detached products
            var detachedProductsQuery = _context.DetachedProducts.Where(d => d.WorkOrderId == workOrderId);
            var totalDetachedProducts = await detachedProductsQuery.CountAsync();
            if (totalDetachedProducts > 0)
            {
                var shippedDetachedProducts = await detachedProductsQuery.CountAsync(d => d.Status == PartStatus.Shipped);
                if (shippedDetachedProducts != totalDetachedProducts)
                {
                    return false;
                }
            }

            return true;
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


    [HttpGet]
    public async Task<IActionResult> GetWorkOrderCompletionReport()
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected" });
            }

            var report = await GenerateCompletionReportAsync(activeWorkOrderId);
            return Json(new { success = true, data = report });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating work order completion report");
            return Json(new { success = false, message = "Error generating completion report" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CompleteWorkOrder()
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected" });
            }

            var isFullyShipped = await IsWorkOrderFullyShippedAsync(activeWorkOrderId);
            if (!isFullyShipped)
            {
                return Json(new { success = false, message = "Work order is not fully shipped and cannot be completed" });
            }

            // Mark work order as completed
            var workOrder = await _context.WorkOrders.FindAsync(activeWorkOrderId);
            if (workOrder != null)
            {
                // Add completion tracking if needed
                await _auditTrailService.LogAsync(
                    action: "WorkOrderCompleted",
                    entityType: "WorkOrder",
                    entityId: activeWorkOrderId,
                    oldValue: "Active",
                    newValue: "Completed",
                    station: "Shipping",
                    workOrderId: activeWorkOrderId,
                    details: "Work order marked as completed - all items shipped",
                    sessionId: HttpContext.Session.Id
                );

                // Notify all stations
                await _hubContext.Clients.Group("all-stations")
                    .SendAsync("WorkOrderCompleted", new
                    {
                        workOrderId = activeWorkOrderId,
                        workOrderName = workOrder.Name,
                        completedAt = DateTime.Now,
                        station = "Shipping",
                        message = $"Work order '{workOrder.Name}' has been completed"
                    });

                _logger.LogInformation("Work order {WorkOrderId} completed at shipping station", activeWorkOrderId);
            }

            return Json(new { success = true, message = "Work order completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing work order");
            return Json(new { success = false, message = "Error completing work order" });
        }
    }

    private async Task<object> GenerateCompletionReportAsync(string workOrderId)
    {
        var workOrder = await _context.WorkOrders
            .Include(w => w.Products)
                .ThenInclude(p => p.Parts)
            .Include(w => w.Hardware)
            .Include(w => w.DetachedProducts)
            .FirstOrDefaultAsync(w => w.Id == workOrderId);

        if (workOrder == null)
        {
            return new { error = "Work order not found" };
        }

        var productStats = workOrder.Products.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            totalParts = p.Parts.Count,
            shippedParts = p.Parts.Count(part => part.Status == PartStatus.Shipped),
            isFullyShipped = p.Parts.Any() && p.Parts.All(part => part.Status == PartStatus.Shipped)
        }).ToList();

        var hardwareStats = workOrder.Hardware.Select(h => new
        {
            id = h.Id,
            name = h.Name,
            quantity = h.Qty,
            isShipped = h.Status == PartStatus.Shipped,
            shippedDate = h.StatusUpdatedDate
        }).ToList();

        var detachedProductStats = workOrder.DetachedProducts.Select(d => new
        {
            id = d.Id,
            name = d.Name,
            quantity = d.Qty,
            isShipped = d.Status == PartStatus.Shipped,
            shippedDate = d.StatusUpdatedDate
        }).ToList();

        return new
        {
            workOrder = new
            {
                id = workOrder.Id,
                name = workOrder.Name,
                importedDate = workOrder.ImportedDate
            },
            summary = new
            {
                totalProducts = workOrder.Products.Count,
                shippedProducts = productStats.Count(p => p.isFullyShipped),
                totalHardware = workOrder.Hardware.Count,
                shippedHardware = hardwareStats.Count(h => h.isShipped),
                totalDetachedProducts = workOrder.DetachedProducts.Count,
                shippedDetachedProducts = detachedProductStats.Count(d => d.isShipped),
                isFullyShipped = await IsWorkOrderFullyShippedAsync(workOrderId)
            },
            details = new
            {
                products = productStats,
                hardware = hardwareStats,
                detachedProducts = detachedProductStats
            }
        };
    }
}