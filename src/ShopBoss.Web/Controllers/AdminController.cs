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
    public async Task<IActionResult> ImportWorkOrder(IFormFile sdfFile)
    {
        if (sdfFile == null || sdfFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select an SDF file to import.";
            return RedirectToAction(nameof(Import));
        }

        if (!sdfFile.FileName.EndsWith(".sdf", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Please select a valid SDF file.";
            return RedirectToAction(nameof(Import));
        }

        try
        {
            // Create a temporary directory for the import process
            var tempDir = Path.Combine(Path.GetTempPath(), "shopboss_import", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Save the uploaded file
            var sdfPath = Path.Combine(tempDir, sdfFile.FileName);
            using (var stream = new FileStream(sdfPath, FileMode.Create))
            {
                await sdfFile.CopyToAsync(stream);
            }

            // TODO: Call the importer tool to process the SDF file
            // For now, just show a success message
            TempData["InfoMessage"] = $"SDF file '{sdfFile.FileName}' uploaded successfully. Import process would start here.";
            
            // Clean up temporary files
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Failed to clean up temporary directory: {TempDir}", tempDir);
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SDF file upload");
            TempData["ErrorMessage"] = "An error occurred while processing the SDF file.";
            return RedirectToAction(nameof(Import));
        }
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
            TempData["SuccessMessage"] = $"'{workOrder.Name}' is now the active work order.";
            
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
    public async Task<IActionResult> UpdateStatus(string itemId, string itemType, PartStatus newStatus, bool cascadeToChildren = false, string workOrderId = "")
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                return Json(new { success = false, message = "Work order ID is required" });
            }

            bool success = false;
            string entityType = itemType;
            var oldValue = "";
            var newValue = newStatus.ToString();

            switch (itemType.ToLower())
            {
                case "part":
                    var part = await _context.Parts.FirstOrDefaultAsync(p => p.Id == itemId);
                    if (part != null)
                    {
                        oldValue = part.Status.ToString();
                        success = await _shippingService.UpdatePartStatusAsync(itemId, newStatus, "Manual");
                    }
                    break;
                    
                case "product":
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == itemId);
                    if (product != null)
                    {
                        // For products, the old value is the effective status of their parts
                        oldValue = "Mixed"; // Simplified for now
                        success = await _shippingService.UpdateProductStatusAsync(itemId, newStatus, cascadeToChildren);
                    }
                    break;
                    
                case "hardware":
                    var hardware = await _context.Hardware.FirstOrDefaultAsync(h => h.Id == itemId);
                    if (hardware != null)
                    {
                        oldValue = hardware.Status == PartStatus.Shipped ? "Shipped" : "Pending";
                        success = await _shippingService.UpdateHardwareStatusAsync(itemId, newStatus == PartStatus.Shipped);
                        newValue = newStatus == PartStatus.Shipped ? "Shipped" : "Pending";
                    }
                    break;
                    
                case "detachedproduct":
                    var detachedProduct = await _context.DetachedProducts.FirstOrDefaultAsync(d => d.Id == itemId);
                    if (detachedProduct != null)
                    {
                        oldValue = detachedProduct.Status == PartStatus.Shipped ? "Shipped" : "Pending";
                        success = await _shippingService.UpdateDetachedProductStatusAsync(itemId, newStatus == PartStatus.Shipped);
                        newValue = newStatus == PartStatus.Shipped ? "Shipped" : "Pending";
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
                        Timestamp = DateTime.UtcNow,
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
                        Timestamp = DateTime.UtcNow,
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
                .OrderBy(r => r.Name)
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
                rack.CreatedDate = DateTime.UtcNow;
                rack.LastModifiedDate = DateTime.UtcNow;

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
    public async Task<IActionResult> EditRack(StorageRack rack)
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
                var originalRows = existingRack.Rows;
                var originalColumns = existingRack.Columns;

                // Update rack properties
                existingRack.Name = rack.Name;
                existingRack.Type = rack.Type;
                existingRack.Description = rack.Description;
                existingRack.Rows = rack.Rows;
                existingRack.Columns = rack.Columns;
                existingRack.Length = rack.Length;
                existingRack.Width = rack.Width;
                existingRack.Height = rack.Height;
                existingRack.Location = rack.Location;
                existingRack.IsActive = rack.IsActive;
                existingRack.IsPortable = rack.IsPortable;
                existingRack.LastModifiedDate = DateTime.UtcNow;

                // If dimensions changed, recreate bins
                if (originalRows != rack.Rows || originalColumns != rack.Columns)
                {
                    // Remove existing bins
                    _context.Bins.RemoveRange(existingRack.Bins);
                    
                    // Create new bins
                    await CreateBinsForRack(existingRack);
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
            rack.LastModifiedDate = DateTime.UtcNow;

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

        for (int row = 1; row <= rack.Rows; row++)
        {
            for (int col = 1; col <= rack.Columns; col++)
            {
                var bin = new Bin
                {
                    Id = Guid.NewGuid().ToString(),
                    StorageRackId = rack.Id,
                    Row = row,
                    Column = col,
                    Status = BinStatus.Empty,
                    MaxCapacity = rack.Type == RackType.DoorsAndDrawerFronts ? 20 : 50,
                    PartsCount = 0,
                    AssignedDate = DateTime.UtcNow,
                    LastUpdatedDate = DateTime.UtcNow
                };

                bins.Add(bin);
            }
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
            // Get current health status
            var healthStatus = await _healthMonitor.GetOrCreateHealthStatusAsync();
            
            var response = new
            {
                overallStatus = healthStatus.OverallStatus.ToString(),
                databaseStatus = healthStatus.DatabaseStatus.ToString(),
                diskSpaceStatus = healthStatus.DiskSpaceStatus.ToString(),
                memoryStatus = healthStatus.MemoryStatus.ToString(),
                responseTimeStatus = healthStatus.ResponseTimeStatus.ToString(),
                availableDiskSpaceGB = healthStatus.AvailableDiskSpaceGB,
                totalDiskSpaceGB = healthStatus.TotalDiskSpaceGB,
                diskUsagePercentage = healthStatus.TotalDiskSpaceGB > 0 ? 
                    ((healthStatus.TotalDiskSpaceGB - healthStatus.AvailableDiskSpaceGB) / healthStatus.TotalDiskSpaceGB) * 100 : 0,
                memoryUsagePercentage = healthStatus.MemoryUsagePercentage,
                averageResponseTimeMs = healthStatus.AverageResponseTimeMs,
                databaseConnectionTimeMs = healthStatus.DatabaseConnectionTimeMs,
                activeWorkOrderCount = healthStatus.ActiveWorkOrderCount,
                totalPartsCount = healthStatus.TotalPartsCount,
                lastHealthCheck = healthStatus.LastHealthCheck,
                errorMessage = healthStatus.ErrorMessage
            };

            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health metrics");
            return Json(new { error = "Failed to retrieve health metrics" });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }


    #region API Endpoints for Status Management Panel

    [HttpPost("admin/api/updatestatus")]
    public async Task<IActionResult> UpdateEntityStatus([FromBody] UpdateStatusRequest request)
    {
        try
        {
            int updatedCount = 0;
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");

            foreach (var entityId in request.EntityIds)
            {
                switch (request.EntityType.ToLower())
                {
                    case "part":
                        var part = await _context.Parts.FindAsync(entityId);
                        if (part != null && Enum.TryParse<PartStatus>(request.NewStatus, out var partStatus))
                        {
                            var oldStatus = part.Status;
                            part.Status = partStatus;
                            part.StatusUpdatedDate = DateTime.UtcNow;
                            
                            await _auditTrailService.LogAsync("StatusChange", "Part", part.Id,
                                new { Status = oldStatus.ToString(), StatusUpdatedDate = part.StatusUpdatedDate },
                                new { Status = part.Status.ToString(), StatusUpdatedDate = part.StatusUpdatedDate },
                                station: "Admin", workOrderId: activeWorkOrderId,
                                details: $"Manual status change from {oldStatus} to {part.Status}");
                            
                            updatedCount++;
                        }
                        break;

                    case "product":
                        var product = await _context.Products.FindAsync(entityId);
                        if (product != null && Enum.TryParse<PartStatus>(request.NewStatus, out var productStatus))
                        {
                            var oldStatus = product.Status;
                            product.Status = productStatus;
                            
                            await _auditTrailService.LogAsync("StatusChange", "Product", product.Id,
                                new { Status = oldStatus.ToString() },
                                new { Status = product.Status.ToString() },
                                station: "Admin", workOrderId: activeWorkOrderId,
                                details: $"Manual status change from {oldStatus} to {product.Status}");
                            
                            updatedCount++;
                        }
                        break;

                    case "nestsheet":
                        var nestSheet = await _context.NestSheets.FindAsync(entityId);
                        if (nestSheet != null)
                        {
                            var oldStatus = nestSheet.StatusString;
                            nestSheet.StatusString = request.NewStatus;
                            nestSheet.StatusUpdatedDate = DateTime.UtcNow;
                            
                            await _auditTrailService.LogAsync("StatusChange", "NestSheet", nestSheet.Id,
                                new { Status = oldStatus, StatusUpdatedDate = nestSheet.StatusUpdatedDate },
                                new { Status = nestSheet.StatusString, StatusUpdatedDate = nestSheet.StatusUpdatedDate },
                                station: "Admin", workOrderId: activeWorkOrderId,
                                details: $"Manual status change to {request.NewStatus}");
                            
                            updatedCount++;
                        }
                        break;

                    case "detached_product":
                        var detachedProduct = await _context.DetachedProducts.FindAsync(entityId);
                        if (detachedProduct != null && Enum.TryParse<PartStatus>(request.NewStatus, out var detachedStatus))
                        {
                            var oldStatus = detachedProduct.Status;
                            
                            // Update status and timestamp
                            detachedProduct.Status = detachedStatus;
                            detachedProduct.StatusUpdatedDate = DateTime.UtcNow;
                            
                            await _auditTrailService.LogAsync("StatusChange", "DetachedProduct", detachedProduct.Id,
                                new { Status = oldStatus.ToString() },
                                new { Status = detachedProduct.Status.ToString() },
                                station: "Admin", workOrderId: activeWorkOrderId,
                                details: $"Manual status change from {oldStatus} to {detachedProduct.Status}");
                            
                            updatedCount++;
                        }
                        break;

                    case "hardware":
                        var hardware = await _context.Hardware.FindAsync(entityId);
                        if (hardware != null && Enum.TryParse<PartStatus>(request.NewStatus, out var hwStatus))
                        {
                            var oldStatus = hardware.Status;
                            
                            // Update status and timestamp
                            hardware.Status = hwStatus;
                            hardware.StatusUpdatedDate = DateTime.UtcNow;
                            
                            await _auditTrailService.LogAsync("StatusChange", "Hardware", hardware.Id,
                                new { Status = oldStatus.ToString() },
                                new { Status = hardware.Status.ToString() },
                                station: "Admin", workOrderId: activeWorkOrderId,
                                details: $"Manual status change from {oldStatus} to {hardware.Status}");
                            
                            updatedCount++;
                        }
                        break;
                }
            }

            await _context.SaveChangesAsync();

            // Send SignalR update
            await _hubContext.Clients.All.SendAsync("RefreshStatus", new { 
                workOrderId = activeWorkOrderId,
                message = $"Status updated for {updatedCount} items"
            });

            return Ok(new { updatedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating entity status");
            return StatusCode(500, "An error occurred while updating status");
        }
    }

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
                    r.Rows,
                    r.Columns
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
            var bins = await _context.Bins
                .Where(b => b.StorageRackId == rackId)
                .OrderBy(b => b.Row)
                .ThenBy(b => b.Column)
                .Select(b => new
                {
                    b.Id,
                    Label = $"R{b.Row}C{b.Column}",
                    b.PartId,
                    b.Status,
                    b.PartsCount
                })
                .ToListAsync();

            return Ok(bins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bins for rack {RackId}", rackId);
            return StatusCode(500, "An error occurred while retrieving bins");
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
                if (!string.IsNullOrEmpty(bin.PartId))
                {
                    await _auditTrailService.LogAsync("BinCleared", "Bin", bin.Id,
                        new { PartId = bin.PartId, PartsCount = bin.PartsCount },
                        new { PartId = (string)null, PartsCount = 0 },
                        station: "Admin", workOrderId: activeWorkOrderId,
                        details: $"Manual bin clear - removed part {bin.PartId}");

                    bin.PartId = null;
                    bin.PartsCount = 0;
                    bin.Status = BinStatus.Empty;
                    bin.LastUpdatedDate = DateTime.UtcNow;
                    
                    clearedCount++;
                }
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

    public class UpdateStatusRequest
    {
        public string EntityType { get; set; } = string.Empty;
        public List<string> EntityIds { get; set; } = new();
        public string NewStatus { get; set; } = string.Empty;
    }

    public class ClearBinsRequest
    {
        public List<string> BinIds { get; set; } = new();
    }

    #endregion
}