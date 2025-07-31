using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Services;
using ShopBoss.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.Diagnostics;

namespace ShopBoss.Web.Controllers;

public class AdminController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly ShippingService _shippingService;
    private readonly WorkOrderService _workOrderService;
    private readonly AuditTrailService _auditTrailService;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly BackupService _backupService;
    private readonly SystemHealthMonitor _healthMonitor;

    public AdminController(
        ShopBossDbContext context, 
        ILogger<AdminController> logger,
        ShippingService shippingService,
        WorkOrderService workOrderService,
        AuditTrailService auditTrailService,
        IHubContext<StatusHub> hubContext,
        BackupService backupService,
        SystemHealthMonitor healthMonitor)
    {
        _context = context;
        _logger = logger;
        _shippingService = shippingService;
        _workOrderService = workOrderService;
        _auditTrailService = auditTrailService;
        _hubContext = hubContext;
        _backupService = backupService;
        _healthMonitor = healthMonitor;
    }

    public async Task<IActionResult> Index(string search = "", bool includeArchived = false)
    {
        try
        {
            var workOrderSummaries = await _workOrderService.GetWorkOrderSummariesAsync(search, includeArchived);

            // Get current active work order from session
            ViewBag.ActiveWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            ViewBag.SearchTerm = search;
            ViewBag.IncludeArchived = includeArchived;

            return View(workOrderSummaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work orders");
            TempData["ErrorMessage"] = "An error occurred while loading the work orders.";
            return View(new List<WorkOrderSummary>());
        }
    }


    [HttpPost]
    public async Task<IActionResult> DeleteWorkOrder(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        try
        {
            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
            {
                return NotFound();
            }

            _context.WorkOrders.Remove(workOrder);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Work order '{workOrder.Name}' has been deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting work order {WorkOrderId}", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the work order.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> ArchiveWorkOrder(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Json(new { success = false, message = "Invalid work order ID." });
        }

        try
        {
            // Check if work order is currently active
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (activeWorkOrderId == id)
            {
                return Json(new { success = false, message = "Cannot archive the currently active work order. Please set a different work order as active first." });
            }

            // Check if work order has active parts
            var isActive = await _workOrderService.IsWorkOrderActiveAsync(id);
            if (isActive)
            {
                return Json(new { success = false, message = "Cannot archive work order with active parts. All parts must be shipped before archiving." });
            }

            var success = await _workOrderService.ArchiveWorkOrderAsync(id);
            if (success)
            {
                // Log the archive action
                await _auditTrailService.LogAsync(
                    action: "ArchiveWorkOrder",
                    entityType: "WorkOrder",
                    entityId: id,
                    oldValue: "Active",
                    newValue: "Archived",
                    station: "Admin",
                    workOrderId: id,
                    details: "Work order archived via Admin interface",
                    sessionId: HttpContext.Session.Id
                );

                return Json(new { success = true, message = "Work order archived successfully." });
            }
            else
            {
                return Json(new { success = false, message = "Failed to archive work order." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving work order {WorkOrderId}", id);
            return Json(new { success = false, message = "An error occurred while archiving the work order." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UnarchiveWorkOrder(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Json(new { success = false, message = "Invalid work order ID." });
        }

        try
        {
            var success = await _workOrderService.UnarchiveWorkOrderAsync(id);
            if (success)
            {
                // Log the unarchive action
                await _auditTrailService.LogAsync(
                    action: "UnarchiveWorkOrder",
                    entityType: "WorkOrder",
                    entityId: id,
                    oldValue: "Archived",
                    newValue: "Active",
                    station: "Admin",
                    workOrderId: id,
                    details: "Work order unarchived via Admin interface",
                    sessionId: HttpContext.Session.Id
                );

                return Json(new { success = true, message = "Work order unarchived successfully." });
            }
            else
            {
                return Json(new { success = false, message = "Failed to unarchive work order." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unarchiving work order {WorkOrderId}", id);
            return Json(new { success = false, message = "An error occurred while unarchiving the work order." });
        }
    }

    public IActionResult Import()
    {
        return View();
    }



    [HttpPost]
    public async Task<IActionResult> SetActiveWorkOrder(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            TempData["ErrorMessage"] = "Invalid work order ID.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            // Verify the work order exists
            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
            {
                TempData["ErrorMessage"] = "Work order not found.";
                return RedirectToAction(nameof(Index));
            }

            // Set as active work order in session
            HttpContext.Session.SetString("ActiveWorkOrderId", id);
            
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active work order {WorkOrderId}", id);
            TempData["ErrorMessage"] = "An error occurred while setting the active work order.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetActiveWorkOrderJson(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Json(new { success = false, message = "Invalid work order ID." });
        }

        try
        {
            // Verify the work order exists
            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
            {
                return Json(new { success = false, message = "Work order not found." });
            }

            // Set as active work order in session
            HttpContext.Session.SetString("ActiveWorkOrderId", id);
            
            return Json(new { 
                success = true, 
                message = $"'{workOrder.Name}' is now the active work order.",
                workOrderId = id,
                workOrderName = workOrder.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active work order {WorkOrderId}", id);
            return Json(new { success = false, message = "An error occurred while setting the active work order." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> BulkDeleteWorkOrders(string[] selectedIds)
    {
        if (selectedIds == null || selectedIds.Length == 0)
        {
            TempData["ErrorMessage"] = "No work orders selected for deletion.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var workOrders = await _context.WorkOrders
                .Where(w => selectedIds.Contains(w.Id))
                .ToListAsync();

            if (workOrders.Count == 0)
            {
                TempData["ErrorMessage"] = "Selected work orders not found.";
                return RedirectToAction(nameof(Index));
            }

            // Check if any of the selected work orders is currently active
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (!string.IsNullOrEmpty(activeWorkOrderId) && selectedIds.Contains(activeWorkOrderId))
            {
                HttpContext.Session.Remove("ActiveWorkOrderId");
            }

            _context.WorkOrders.RemoveRange(workOrders);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Successfully deleted {workOrders.Count} work order(s).";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing bulk delete operation");
            TempData["ErrorMessage"] = "An error occurred while deleting the selected work orders.";
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> GetActiveWorkOrder()
    {
        var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
        if (string.IsNullOrEmpty(activeWorkOrderId))
        {
            return Json(new { success = false, message = "No active work order selected." });
        }

        try
        {
            var workOrder = await _context.WorkOrders.FindAsync(activeWorkOrderId);
            if (workOrder == null)
            {
                // Clear invalid active work order from session
                HttpContext.Session.Remove("ActiveWorkOrderId");
                return Json(new { success = false, message = "Active work order not found." });
            }

            return Json(new { 
                success = true, 
                activeWorkOrderId = activeWorkOrderId, 
                activeWorkOrderName = workOrder.Name 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active work order {WorkOrderId}", activeWorkOrderId);
            return Json(new { success = false, message = "Error retrieving active work order." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RestoreActiveWorkOrder(string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                return Json(new { success = false, message = "Work order ID is required." });
            }

            // Verify the work order exists and is not archived
            var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
            if (workOrder == null)
            {
                return Json(new { success = false, message = "Work order not found." });
            }

            if (workOrder.IsArchived)
            {
                return Json(new { success = false, message = "Cannot restore archived work order." });
            }

            // Restore the session
            HttpContext.Session.SetString("ActiveWorkOrderId", workOrderId);

            return Json(new { 
                success = true, 
                message = "Work order session restored.",
                activeWorkOrderId = workOrder.Id,
                activeWorkOrderName = workOrder.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring active work order session for {WorkOrderId}", workOrderId);
            return Json(new { success = false, message = "An error occurred while restoring the work order session." });
        }
    }

    public async Task<IActionResult> GetAllWorkOrders()
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            
            var workOrders = await _context.WorkOrders
                .OrderByDescending(w => w.ImportedDate)
                .Select(w => new 
                {
                    Id = w.Id,
                    Name = w.Name,
                    ImportedDate = w.ImportedDate,
                    IsActive = w.Id == activeWorkOrderId
                })
                .ToListAsync();

            return Json(new { 
                success = true, 
                workOrders = workOrders,
                activeWorkOrderId = activeWorkOrderId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all work orders for dropdown");
            return Json(new { success = false, message = "Error retrieving work orders." });
        }
    }


    public async Task<IActionResult> Statistics()
    {
        try
        {
            var stats = new
            {
                TotalWorkOrders = await _context.WorkOrders.CountAsync(),
                TotalProducts = await _context.Products.CountAsync(),
                TotalParts = await _context.Parts.CountAsync(),
                TotalHardware = await _context.Hardware.CountAsync(),
                TotalDetachedProducts = await _context.DetachedProducts.CountAsync(),
                TotalSubassemblies = await _context.Subassemblies.CountAsync()
            };

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics");
            TempData["ErrorMessage"] = "An error occurred while loading statistics.";
            return View();
        }
    }

    // Modify Work Order Methods
    public IActionResult ModifyWorkOrder(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            TempData["ErrorMessage"] = "Work order ID is required to modify work order.";
            return RedirectToAction(nameof(Index));
        }

        return View((object)id);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(string itemId, string itemType, PartStatus newStatus, bool cascadeToChildren = true, string workOrderId = "")
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                return Json(new { success = false, message = "Work order ID is required" });
            }

            bool success = false;
            string entityType = itemType;
            object? oldValue = null;
            object newValue = new { Status = newStatus.ToString() };

            switch (itemType.ToLower())
            {
                case "part":
                    var part = await _context.Parts.FirstOrDefaultAsync(p => p.Id == itemId);
                    if (part != null)
                    {
                        oldValue = new { Status = part.Status.ToString() };
                        part.Status = newStatus;
                        part.StatusUpdatedDate = DateTime.Now;
                        await _context.SaveChangesAsync();
                        success = true;
                    }
                    break;
                    
                case "product":
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == itemId);
                    if (product != null)
                    {
                        oldValue = new { Status = product.Status.ToString() };
                        product.Status = newStatus;
                        product.StatusUpdatedDate = DateTime.Now;

                        if (cascadeToChildren)
                        {
                            // Update all direct parts for this product
                            var productParts = await _context.Parts
                                .Where(p => p.ProductId == itemId)
                                .ToListAsync();

                            foreach (var productPart in productParts)
                            {
                                productPart.Status = newStatus;
                                productPart.StatusUpdatedDate = DateTime.Now;
                            }

                            // Update all subassembly parts (including nested subassemblies)
                            var subassemblyParts = await _context.Parts
                                .Where(p => p.Subassembly != null && p.Subassembly.ProductId == itemId)
                                .ToListAsync();

                            foreach (var subPart in subassemblyParts)
                            {
                                subPart.Status = newStatus;
                                subPart.StatusUpdatedDate = DateTime.Now;
                            }

                            // Update all hardware for this product
                            var productHardware = await _context.Hardware
                                .Where(h => h.ProductId == itemId)
                                .ToListAsync();

                            foreach (var productHardwareItem in productHardware)
                            {
                                productHardwareItem.Status = newStatus;
                                productHardwareItem.StatusUpdatedDate = DateTime.Now;
                            }
                        }

                        await _context.SaveChangesAsync();
                        success = true;
                    }
                    break;
                    
                case "hardware":
                    var hardware = await _context.Hardware.FirstOrDefaultAsync(h => h.Id == itemId);
                    if (hardware != null)
                    {
                        oldValue = new { Status = hardware.Status.ToString() };
                        hardware.Status = newStatus;
                        hardware.StatusUpdatedDate = DateTime.Now;
                        await _context.SaveChangesAsync();
                        success = true;
                    }
                    break;
                    
                case "detachedproduct":
                    var detachedProduct = await _context.DetachedProducts.FirstOrDefaultAsync(d => d.Id == itemId);
                    if (detachedProduct != null)
                    {
                        oldValue = new { Status = detachedProduct.Status.ToString() };
                        detachedProduct.Status = newStatus;
                        detachedProduct.StatusUpdatedDate = DateTime.Now;

                        if (cascadeToChildren)
                        {
                            // Update all Parts that belong to this DetachedProduct
                            var detachedProductParts = await _context.Parts
                                .Where(p => p.ProductId == itemId)
                                .ToListAsync();

                            foreach (var detachedPart in detachedProductParts)
                            {
                                detachedPart.Status = newStatus;
                                detachedPart.StatusUpdatedDate = DateTime.Now;
                            }
                        }

                        await _context.SaveChangesAsync();
                        success = true;
                    }
                    break;

                case "nestsheet":
                    var nestSheet = await _context.NestSheets.FirstOrDefaultAsync(n => n.Id == itemId);
                    if (nestSheet != null)
                    {
                        oldValue = new { Status = nestSheet.Status.ToString() };
                        nestSheet.Status = newStatus;
                        nestSheet.StatusUpdatedDate = DateTime.Now;

                        if (cascadeToChildren)
                        {
                            // Update all associated parts for this nest sheet
                            // This should find ALL parts linked to this nest sheet regardless of Product vs DetachedProduct
                            var nestSheetParts = await _context.Parts
                                .Where(p => p.NestSheetId == itemId)
                                .ToListAsync();

                            foreach (var nestPart in nestSheetParts)
                            {
                                nestPart.Status = newStatus;
                                nestPart.StatusUpdatedDate = DateTime.Now;
                            }
                            
                            // Additional safety net: Force refresh of entity tracking to ensure all changes are captured
                            _context.ChangeTracker.DetectChanges();
                        }

                        await _context.SaveChangesAsync();
                        success = true;
                    }
                    break;
            }

            if (success)
            {
                // Log the manual status change
                await _auditTrailService.LogAsync(
                    action: "ManualStatusChange",
                    entityType: entityType,
                    entityId: itemId,
                    oldValue: oldValue,
                    newValue: newValue,
                    station: "Manual",
                    workOrderId: workOrderId,
                    details: $"Manual status change via Admin interface. Cascade: {cascadeToChildren}",
                    sessionId: HttpContext.Session.Id
                );

                // Send SignalR notification to other stations
                await _hubContext.Clients.Group("all-stations")
                    .SendAsync("StatusManuallyChanged", new
                    {
                        ItemId = itemId,
                        ItemType = itemType,
                        NewStatus = newStatus.ToString(),
                        WorkOrderId = workOrderId,
                        Timestamp = DateTime.Now,
                        Station = "Manual",
                        ChangedBy = "Admin"
                    });

                return Json(new { success = true, message = $"{itemType} status updated successfully" });
            }
            else
            {
                return Json(new { success = false, message = $"Failed to update {itemType} status" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for {ItemType} {ItemId}", itemType, itemId);
            return Json(new { success = false, message = "An error occurred while updating the status" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateWorkOrderName(string workOrderId, string newName)
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                return Json(new { success = false, message = "Work order ID is required" });
            }

            if (string.IsNullOrEmpty(newName?.Trim()))
            {
                return Json(new { success = false, message = "Work order name cannot be empty" });
            }

            var workOrder = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Id == workOrderId);
            if (workOrder == null)
            {
                return Json(new { success = false, message = "Work order not found" });
            }

            var oldName = workOrder.Name;
            var trimmedNewName = newName.Trim();

            // Check if name is actually changing
            if (oldName == trimmedNewName)
            {
                return Json(new { success = true, message = "Work order name unchanged" });
            }

            // Update the work order name
            workOrder.Name = trimmedNewName;
            await _context.SaveChangesAsync();

            // Log the change to audit trail
            await _auditTrailService.LogAsync(
                action: "WorkOrderNameUpdate",
                entityType: "WorkOrder",
                entityId: workOrderId,
                oldValue: new { Name = oldName },
                newValue: new { Name = trimmedNewName },
                station: "Modify Interface",
                workOrderId: workOrderId,
                details: $"Work order name changed from '{oldName}' to '{trimmedNewName}'",
                sessionId: HttpContext.Session.Id
            );

            _logger.LogInformation("Work order {WorkOrderId} name updated from '{OldName}' to '{NewName}'", 
                workOrderId, oldName, trimmedNewName);

            return Json(new { success = true, message = "Work order name updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order name for {WorkOrderId}", workOrderId);
            return Json(new { success = false, message = "An error occurred while updating the work order name" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> BulkUpdateStatus(List<StatusUpdateRequest> updates, string workOrderId = "")
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                return Json(new { success = false, message = "Work order ID is required" });
            }

            if (updates == null || !updates.Any())
            {
                return Json(new { success = false, message = "No updates provided" });
            }

            var result = await _shippingService.UpdateMultipleStatusesAsync(updates);

            if (result.Success)
            {
                // Log bulk operation
                await _auditTrailService.LogAsync(
                    action: "BulkStatusChange",
                    entityType: "Multiple",
                    entityId: "Bulk",
                    oldValue: JsonSerializer.Serialize(updates.Select(u => new { u.ItemId, u.ItemType })),
                    newValue: JsonSerializer.Serialize(updates.Select(u => new { u.ItemId, u.NewStatus })),
                    station: "Manual",
                    workOrderId: workOrderId,
                    details: $"Bulk status change: {result.SuccessCount} successful, {result.FailureCount} failed",
                    sessionId: HttpContext.Session.Id
                );

                // Send SignalR notification
                await _hubContext.Clients.Group("all-stations")
                    .SendAsync("BulkStatusChanged", new
                    {
                        UpdateCount = result.SuccessCount,
                        WorkOrderId = workOrderId,
                        Timestamp = DateTime.Now,
                        Station = "Manual"
                    });

                return Json(new 
                { 
                    success = true, 
                    message = $"Bulk update completed: {result.SuccessCount} successful, {result.FailureCount} failed",
                    successCount = result.SuccessCount,
                    failureCount = result.FailureCount,
                    failedItems = result.FailedItems
                });
            }
            else
            {
                return Json(new { success = false, message = result.ErrorMessage ?? "Bulk update failed" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing bulk status update");
            return Json(new { success = false, message = "An error occurred during bulk update" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStatusData(string workOrderId = "", string search = "")
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                return Json(new { success = false, message = "Work order ID is required" });
            }

            var statusData = await _workOrderService.GetWorkOrderManagementDataAsync(workOrderId);

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                statusData.ProductNodes = statusData.ProductNodes
                    .Where(p => p.Product.Name.ToLower().Contains(search) ||
                               p.Parts.Any(part => part.Name.ToLower().Contains(search)) ||
                               p.Subassemblies.Any(sub => sub.Name.ToLower().Contains(search)))
                    .ToList();

                // Filter parts within products
                foreach (var productNode in statusData.ProductNodes)
                {
                    productNode.Parts = productNode.Parts
                        .Where(part => part.Name.ToLower().Contains(search))
                        .ToList();
                }
            }

            return Json(new { success = true, data = statusData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting filtered status data");
            return Json(new { success = false, message = "Error loading status data" });
        }
    }

    // Rack Configuration Methods
    public async Task<IActionResult> RackConfiguration()
    {
        try
        {
            var racks = await _context.StorageRacks
                .Include(r => r.Bins)
                .ToListAsync();

            return View(racks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rack configuration");
            TempData["ErrorMessage"] = "An error occurred while loading rack configuration.";
            return View(new List<StorageRack>());
        }
    }

    [HttpGet]
    public IActionResult CreateRack()
    {
        return View(new StorageRack());
    }

    [HttpPost]
    public async Task<IActionResult> CreateRack(StorageRack rack)
    {
        try
        {
            if (ModelState.IsValid)
            {
                rack.Id = Guid.NewGuid().ToString();
                rack.CreatedDate = DateTime.Now;
                rack.LastModifiedDate = DateTime.Now;

                _context.StorageRacks.Add(rack);
                await _context.SaveChangesAsync();

                // Create bins for the new rack
                await CreateBinsForRack(rack);

                TempData["SuccessMessage"] = $"Rack '{rack.Name}' created successfully with {rack.TotalBins} bins.";
                return RedirectToAction(nameof(RackConfiguration));
            }

            return View(rack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rack");
            TempData["ErrorMessage"] = "An error occurred while creating the rack.";
            return View(rack);
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditRack(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        try
        {
            var rack = await _context.StorageRacks
                .Include(r => r.Bins)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rack == null)
            {
                return NotFound();
            }

            return View(rack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rack for editing: {RackId}", id);
            TempData["ErrorMessage"] = "An error occurred while loading the rack.";
            return RedirectToAction(nameof(RackConfiguration));
        }
    }

    [HttpPost]
    public async Task<IActionResult> EditRack(StorageRack rack, string? BinData)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var existingRack = await _context.StorageRacks
                    .Include(r => r.Bins)
                    .FirstOrDefaultAsync(r => r.Id == rack.Id);

                if (existingRack == null)
                {
                    return NotFound();
                }

                // Store original dimensions
                // Update rack properties
                existingRack.Name = rack.Name;
                existingRack.Type = rack.Type;
                existingRack.Description = rack.Description;
                existingRack.Length = rack.Length;
                existingRack.Width = rack.Width;
                existingRack.Height = rack.Height;
                existingRack.Location = rack.Location;
                existingRack.IsActive = rack.IsActive;
                existingRack.IsPortable = rack.IsPortable;
                // Check if grid dimensions changed and adjust bins accordingly
                var originalBinCount = existingRack.Bins.Count;
                var newBinCount = rack.Rows * rack.Columns;
                
                existingRack.Rows = rack.Rows;
                existingRack.Columns = rack.Columns;
                existingRack.LastModifiedDate = DateTime.Now;

                // Adjust bins if grid size changed
                if (originalBinCount != newBinCount)
                {
                    _logger.LogInformation("Rack {RackId} grid size changed from {OriginalBins} to {NewBins} bins", 
                        rack.Id, originalBinCount, newBinCount);

                    if (newBinCount > originalBinCount)
                    {
                        // Add new bins - regenerate all bin labels to avoid conflicts
                        var existingBinLabels = existingRack.Bins.Select(b => b.BinLabel).ToHashSet();
                        var binsToAdd = newBinCount - originalBinCount;
                        
                        // Generate labels for new bins starting from the end
                        for (int i = originalBinCount; i < newBinCount; i++)
                        {
                            var row = i / rack.Columns + 1;
                            var col = (i % rack.Columns) + 1;
                            var binLabel = $"{(char)('A' + row - 1)}{col:D2}";
                            
                            // Ensure label is unique (shouldn't happen with proper grid logic, but safety check)
                            int suffix = 1;
                            var originalLabel = binLabel;
                            while (existingBinLabels.Contains(binLabel))
                            {
                                binLabel = $"{originalLabel}_{suffix}";
                                suffix++;
                            }
                            
                            var newBin = new Bin
                            {
                                StorageRackId = existingRack.Id,
                                Status = BinStatus.Empty,
                                BinLabel = binLabel,
                                LastUpdatedDate = DateTime.Now
                            };
                            
                            existingRack.Bins.Add(newBin);
                            existingBinLabels.Add(binLabel); // Track new label to avoid duplicates in this batch
                        }
                        
                        _logger.LogInformation("Added {BinsAdded} new bins to rack {RackId}", binsToAdd, rack.Id);
                    }
                    else
                    {
                        // Remove excess bins (only if they're empty)
                        var binsToRemove = existingRack.Bins
                            .Skip(newBinCount)
                            .Where(b => b.Status == BinStatus.Empty)
                            .ToList();
                            
                        var occupiedBinsToRemove = existingRack.Bins
                            .Skip(newBinCount)
                            .Where(b => b.Status != BinStatus.Empty)
                            .ToList();
                            
                        if (occupiedBinsToRemove.Any())
                        {
                            TempData["ErrorMessage"] = $"Cannot reduce grid size: {occupiedBinsToRemove.Count} bins beyond the new dimensions contain parts. Please move or remove parts first.";
                            return RedirectToAction("EditRack", new { id = rack.Id });
                        }
                        
                        foreach (var binToRemove in binsToRemove)
                        {
                            existingRack.Bins.Remove(binToRemove);
                        }
                        
                        _logger.LogInformation("Removed {BinsRemoved} empty bins from rack {RackId}", binsToRemove.Count, rack.Id);
                    }
                }

                // Update bin labels if provided - use two-phase approach to avoid constraint conflicts
                if (!string.IsNullOrEmpty(BinData))
                {
                    try
                    {
                        var binUpdates = System.Text.Json.JsonSerializer.Deserialize<List<BinUpdateModel>>(BinData);
                        if (binUpdates != null)
                        {
                            // Phase 1: Clear all labels that need to be changed to avoid conflicts
                            var binsToUpdate = new List<(Bin bin, string newLabel)>();
                            foreach (var binUpdate in binUpdates)
                            {
                                var existingBin = existingRack.Bins.FirstOrDefault(b => b.Id == binUpdate.Id);
                                if (existingBin != null && existingBin.BinLabel != binUpdate.Label)
                                {
                                    binsToUpdate.Add((existingBin, binUpdate.Label));
                                    existingBin.BinLabel = $"TEMP_{existingBin.Id}"; // Temporary unique label
                                }
                            }
                            
                            // Save temporary changes to avoid constraint violations
                            if (binsToUpdate.Any())
                            {
                                await _context.SaveChangesAsync();
                                
                                // Phase 2: Apply the actual labels
                                foreach (var (bin, newLabel) in binsToUpdate)
                                {
                                    bin.BinLabel = newLabel;
                                    bin.LastUpdatedDate = DateTime.Now;
                                }
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse bin data for rack {RackId}", rack.Id);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Rack '{rack.Name}' updated successfully.";
                return RedirectToAction(nameof(RackConfiguration));
            }

            return View(rack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rack: {RackId}", rack.Id);
            TempData["ErrorMessage"] = "An error occurred while updating the rack.";
            return View(rack);
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteRack(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        try
        {
            var rack = await _context.StorageRacks
                .Include(r => r.Bins)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rack == null)
            {
                return NotFound();
            }

            // Check if rack has any parts assigned
            var hasAssignedParts = rack.Bins.Any(b => !string.IsNullOrEmpty(b.PartId));

            if (hasAssignedParts)
            {
                TempData["ErrorMessage"] = $"Cannot delete rack '{rack.Name}' - it contains assigned parts. Please move all parts before deleting.";
                return RedirectToAction(nameof(RackConfiguration));
            }

            _context.StorageRacks.Remove(rack);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Rack '{rack.Name}' deleted successfully.";
            return RedirectToAction(nameof(RackConfiguration));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rack: {RackId}", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the rack.";
            return RedirectToAction(nameof(RackConfiguration));
        }
    }

    [HttpPost]
    public async Task<IActionResult> ToggleRackStatus(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Json(new { success = false, message = "Invalid rack ID" });
        }

        try
        {
            var rack = await _context.StorageRacks.FindAsync(id);
            if (rack == null)
            {
                return Json(new { success = false, message = "Rack not found" });
            }

            rack.IsActive = !rack.IsActive;
            rack.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = $"Rack '{rack.Name}' is now {(rack.IsActive ? "active" : "inactive")}",
                isActive = rack.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling rack status: {RackId}", id);
            return Json(new { success = false, message = "An error occurred while updating the rack status" });
        }
    }

    private async Task CreateBinsForRack(StorageRack rack)
    {
        var bins = new List<Bin>();

        // Create bins using the rack's specific row/column configuration
        var binLabels = new List<string>();
        for (int row = 1; row <= rack.Rows; row++)
        {
            for (int col = 1; col <= rack.Columns; col++)
            {
                binLabels.Add($"{(char)('A' + row - 1)}{col:D2}");
            }
        }

        foreach (var label in binLabels)
        {
            var bin = new Bin
            {
                Id = Guid.NewGuid().ToString(),
                StorageRackId = rack.Id,
                Status = BinStatus.Empty,
                BinLabel = label,
                PartsCount = 0,
                AssignedDate = DateTime.Now,
                LastUpdatedDate = DateTime.Now
            };

            bins.Add(bin);
        }

        _context.Bins.AddRange(bins);
        await _context.SaveChangesAsync();
    }

    public IActionResult TreeTestHarness()
    {
        return View();
    }


    // Backup Management Actions (Phase A2)
    [HttpGet]
    public async Task<IActionResult> BackupManagement()
    {
        try
        {
            var config = await _backupService.GetBackupConfigurationAsync();
            var recentBackups = await _backupService.GetRecentBackupsAsync(20);
            
            var viewModel = new BackupManagementViewModel
            {
                Configuration = config,
                RecentBackups = recentBackups
            };
            
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading backup management page");
            TempData["ErrorMessage"] = "An error occurred while loading the backup management page.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateBackupConfiguration(BackupConfiguration configuration)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var success = await _backupService.UpdateBackupConfigurationAsync(configuration);
                if (success)
                {
                    TempData["SuccessMessage"] = "Backup configuration updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update backup configuration.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid backup configuration settings.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating backup configuration");
            TempData["ErrorMessage"] = "An error occurred while updating the backup configuration.";
        }
        
        return RedirectToAction(nameof(BackupManagement));
    }

    [HttpPost]
    public async Task<IActionResult> CreateManualBackup()
    {
        try
        {
            var backupResult = await _backupService.CreateBackupAsync(BackupType.Manual);
            
            if (backupResult.IsSuccessful)
            {
                TempData["SuccessMessage"] = $"Manual backup created successfully. File: {Path.GetFileName(backupResult.FilePath)}";
            }
            else
            {
                TempData["ErrorMessage"] = $"Backup failed: {backupResult.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manual backup");
            TempData["ErrorMessage"] = "An error occurred while creating the backup.";
        }
        
        return RedirectToAction(nameof(BackupManagement));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteBackup(int id)
    {
        try
        {
            var success = await _backupService.DeleteBackupAsync(id);
            
            if (success)
            {
                TempData["SuccessMessage"] = "Backup deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete backup.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup {BackupId}", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the backup.";
        }
        
        return RedirectToAction(nameof(BackupManagement));
    }

    [HttpPost]
    public async Task<IActionResult> RestoreBackup(int id)
    {
        try
        {
            var success = await _backupService.RestoreBackupAsync(id);
            
            if (success)
            {
                TempData["SuccessMessage"] = "Database restored successfully. Please restart the application.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to restore backup.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup {BackupId}", id);
            TempData["ErrorMessage"] = "An error occurred while restoring the backup.";
        }
        
        return RedirectToAction(nameof(BackupManagement));
    }


    public async Task<IActionResult> HealthDashboard()
    {
        try
        {
            // Get current health status from database
            var healthStatus = await _healthMonitor.GetOrCreateHealthStatusAsync();
            
            // Get recent health metrics
            var currentMetrics = await _healthMonitor.CheckSystemHealthAsync();
            
            // Get recent audit logs for health-related activities
            var recentHealthLogs = await _context.AuditLogs
                .Where(log => log.EntityType == "SystemHealth" || log.EntityType == "System")
                .OrderByDescending(log => log.Timestamp)
                .Take(20)
                .ToListAsync();

            var viewModel = new HealthDashboardViewModel
            {
                CurrentHealthStatus = healthStatus,
                CurrentMetrics = currentMetrics,
                RecentHealthLogs = recentHealthLogs,
                PageTitle = "System Health Dashboard"
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading health dashboard");
            TempData["ErrorMessage"] = "An error occurred while loading the health dashboard.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> RunHealthCheck()
    {
        try
        {
            // Force an immediate health check
            var metrics = await _healthMonitor.CheckSystemHealthAsync();
            await _healthMonitor.UpdateHealthStatusAsync(metrics);
            
            await _auditTrailService.LogAsync(
                "SystemHealth",
                "ManualHealthCheck",
                "System",
                "1",
                "Admin",
                "Manual health check initiated from admin dashboard");

            return Json(new { success = true, message = "Health check completed successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running manual health check");
            return Json(new { success = false, message = "An error occurred while running the health check." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetHealthMetrics()
    {
        try
        {
            // Get fresh health metrics instead of relying solely on database
            var healthMetrics = await _healthMonitor.CheckSystemHealthAsync();
            
            // Also try to get stored status for additional data
            var healthStatus = await _healthMonitor.GetOrCreateHealthStatusAsync();
            
            var response = new
            {
                overallStatus = healthMetrics.OverallStatus.ToString(),
                databaseStatus = healthMetrics.DatabaseStatus.ToString(),
                diskSpaceStatus = healthMetrics.DiskSpaceStatus.ToString(),
                memoryStatus = healthMetrics.MemoryStatus.ToString(),
                responseTimeStatus = healthMetrics.ResponseTimeStatus.ToString(),
                availableDiskSpaceGB = healthMetrics.AvailableDiskSpaceGB,
                totalDiskSpaceGB = healthMetrics.TotalDiskSpaceGB,
                diskUsagePercentage = healthMetrics.TotalDiskSpaceGB > 0 ? 
                    ((healthMetrics.TotalDiskSpaceGB - healthMetrics.AvailableDiskSpaceGB) / healthMetrics.TotalDiskSpaceGB) * 100 : 0,
                memoryUsagePercentage = healthMetrics.MemoryUsagePercentage,
                averageResponseTimeMs = healthMetrics.AverageResponseTimeMs,
                databaseConnectionTimeMs = healthMetrics.DatabaseConnectionTimeMs,
                activeWorkOrderCount = healthMetrics.ActiveWorkOrderCount,
                totalPartsCount = healthMetrics.TotalPartsCount,
                lastHealthCheck = healthMetrics.LastHealthCheck,
                errorMessage = healthMetrics.ErrorMessage
            };

            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health metrics");
            return Json(new { 
                overallStatus = "Error",
                error = "Failed to retrieve health metrics",
                errorMessage = ex.Message,
                lastHealthCheck = DateTime.Now
            });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }


    #region API Endpoints for Status Management Panel


    [HttpGet("admin/api/audithistory/{entityType}/{entityId}")]
    public async Task<IActionResult> GetAuditHistory(string entityType, string entityId)
    {
        try
        {
            var auditEntries = await _auditTrailService.GetEntityAuditTrailAsync(entityType, entityId);
            
            return Ok(auditEntries.Select(e => new
            {
                e.Id,
                e.Timestamp,
                e.Action,
                e.EntityType,
                e.EntityId,
                e.OldValue,
                e.NewValue,
                e.Station,
                e.Details,
                e.UserId
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit history for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, "An error occurred while retrieving audit history");
        }
    }

    [HttpGet("admin/api/racks")]
    public async Task<IActionResult> GetRacks()
    {
        try
        {
            var racks = await _context.StorageRacks
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Type,
                    TotalBins = r.TotalBins,
                    AvailableBins = r.AvailableBins
                })
                .ToListAsync();

            return Ok(racks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving racks");
            return StatusCode(500, "An error occurred while retrieving racks");
        }
    }

    [HttpGet("admin/api/racks/{rackId}/bins")]
    public async Task<IActionResult> GetRackBins(string rackId)
    {
        try
        {
            var rawBins = await _context.Bins
                .Where(b => b.StorageRackId == rackId)
                .ToListAsync();

            _logger.LogInformation("Found {Count} bins for rack {RackId}", rawBins.Count, rackId);
            if (rawBins.Count > 0)
            {
                _logger.LogInformation("First bin: Id={Id}, BinLabel={BinLabel}, Status={Status}", 
                    rawBins[0].Id, rawBins[0].BinLabel, rawBins[0].Status);
            }

            var bins = rawBins.Select(b => new
            {
                b.Id,
                Label = b.BinLabel,
                b.PartId,
                Status = b.Status.ToString(),
                b.PartsCount
            }).ToList();

            return Ok(bins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bins for rack {RackId}", rackId);
            return StatusCode(500, "An error occurred while retrieving bins");
        }
    }

    [HttpPost("admin/api/racks/{rackId}/bins")]
    public async Task<IActionResult> SaveRackBins(string rackId, [FromBody] List<BinUpdateModel> binData)
    {
        try
        {
            // Verify rack exists
            var rack = await _context.StorageRacks.FindAsync(rackId);
            if (rack == null)
            {
                return NotFound("Rack not found");
            }

            // Get existing bins
            var existingBins = await _context.Bins
                .Where(b => b.StorageRackId == rackId)
                .ToListAsync();

            var session = HttpContext.Session.Id;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var updatedCount = 0;

            // Process bin label updates only
            foreach (var binUpdate in binData)
            {
                var existingBin = existingBins.FirstOrDefault(b => b.Id == binUpdate.Id);
                if (existingBin != null && existingBin.BinLabel != binUpdate.Label)
                {
                    var oldLabel = existingBin.BinLabel;
                    existingBin.BinLabel = binUpdate.Label;
                    existingBin.LastUpdatedDate = DateTime.Now;
                    updatedCount++;

                    await _auditTrailService.LogAsync("BinUpdated", "Bin", existingBin.Id,
                        oldValue: new { BinLabel = oldLabel },
                        newValue: new { BinLabel = binUpdate.Label },
                        station: "Admin", details: $"Bin label changed from '{oldLabel}' to '{binUpdate.Label}'",
                        sessionId: session, ipAddress: ipAddress);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Bin labels updated for rack {RackId}: {UpdatedCount} updated", rackId, updatedCount);

            return Ok(new { success = true, message = "Bin labels updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving bin labels for rack {RackId}", rackId);
            return StatusCode(500, "An error occurred while saving bin labels");
        }
    }

    [HttpPost("admin/api/clearbins")]
    public async Task<IActionResult> ClearBins([FromBody] ClearBinsRequest request)
    {
        try
        {
            var bins = await _context.Bins
                .Where(b => request.BinIds.Contains(b.Id))
                .ToListAsync();

            int clearedCount = 0;
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");

            foreach (var bin in bins)
            {
                // Nuclear bin clearing - always clear regardless of current state
                await _auditTrailService.LogAsync("BinCleared", "Bin", bin.Id,
                    new { PartId = bin.PartId, PartsCount = bin.PartsCount },
                    new { PartId = (string?)null, PartsCount = 0 },
                    station: "Admin", workOrderId: activeWorkOrderId,
                    details: $"Nuclear bin clear - was: PartId={bin.PartId}, PartsCount={bin.PartsCount}");

                bin.PartId = null;
                bin.PartsCount = 0;
                bin.Status = BinStatus.Empty;
                bin.LastUpdatedDate = DateTime.Now;
                
                clearedCount++;
            }

            await _context.SaveChangesAsync();

            return Ok(new { clearedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing bins");
            return StatusCode(500, "An error occurred while clearing bins");
        }
    }

    #endregion

    #region Request Models


    public class ClearBinsRequest
    {
        public List<string> BinIds { get; set; } = new();
    }

    public class BinUpdateModel
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    #endregion
}