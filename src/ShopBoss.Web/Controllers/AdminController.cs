using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Controllers;

public class AdminController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ShopBossDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string search = "")
    {
        try
        {
            var query = _context.WorkOrders
                .Include(w => w.Products)
                .Include(w => w.Hardware)
                .Include(w => w.DetachedProducts)
                .AsQueryable();

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(w => w.Name.Contains(search) || w.Id.Contains(search));
            }

            var workOrders = await query
                .OrderByDescending(w => w.ImportedDate)
                .ToListAsync();

            // Get current active work order from session
            ViewBag.ActiveWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            ViewBag.SearchTerm = search;

            return View(workOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work orders");
            TempData["ErrorMessage"] = "An error occurred while loading the work orders.";
            return View(new List<WorkOrder>());
        }
    }

    public async Task<IActionResult> WorkOrder(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        try
        {
            var workOrder = await _context.WorkOrders
                .Include(w => w.Products)
                    .ThenInclude(p => p.Parts)
                .Include(w => w.Products)
                    .ThenInclude(p => p.Subassemblies)
                        .ThenInclude(s => s.Parts)
                .Include(w => w.Products)
                    .ThenInclude(p => p.Subassemblies)
                        .ThenInclude(s => s.ChildSubassemblies)
                            .ThenInclude(cs => cs.Parts)
                .Include(w => w.Hardware)
                .Include(w => w.DetachedProducts)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            return View(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order {WorkOrderId}", id);
            TempData["ErrorMessage"] = "An error occurred while loading the work order.";
            return RedirectToAction(nameof(Index));
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

    public async Task<IActionResult> Modify(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        try
        {
            var workOrder = await _context.WorkOrders
                .Include(w => w.Products)
                    .ThenInclude(p => p.Parts)
                .Include(w => w.Products)
                    .ThenInclude(p => p.Subassemblies)
                        .ThenInclude(s => s.Parts)
                .Include(w => w.Products)
                    .ThenInclude(p => p.Subassemblies)
                        .ThenInclude(s => s.ChildSubassemblies)
                            .ThenInclude(cs => cs.Parts)
                .Include(w => w.Hardware)
                .Include(w => w.DetachedProducts)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workOrder == null)
            {
                return NotFound();
            }

            ViewBag.Mode = "modify";
            // Create clean data structure for JavaScript serialization
            ViewBag.WorkOrderData = new
            {
                id = workOrder.Id,
                name = workOrder.Name,
                importedDate = workOrder.ImportedDate,
                products = workOrder.Products.Select(p => new
                {
                    id = p.Id,
                    productNumber = p.ProductNumber,
                    name = p.Name,
                    qty = p.Qty,
                    length = p.Length,
                    width = p.Width,
                    parts = p.Parts.Select(part => new
                    {
                        id = part.Id,
                        name = part.Name,
                        qty = part.Qty,
                        length = part.Length,
                        width = part.Width,
                        thickness = part.Thickness,
                        material = part.Material
                    }),
                    subassemblies = p.Subassemblies.Select(sub => new
                    {
                        id = sub.Id,
                        name = sub.Name,
                        qty = sub.Qty,
                        parts = sub.Parts.Select(part => new
                        {
                            id = part.Id,
                            name = part.Name,
                            qty = part.Qty,
                            length = part.Length,
                            width = part.Width,
                            thickness = part.Thickness,
                            material = part.Material
                        }),
                        childSubassemblies = sub.ChildSubassemblies.Select(child => new
                        {
                            id = child.Id,
                            name = child.Name,
                            qty = child.Qty,
                            parts = child.Parts.Select(part => new
                            {
                                id = part.Id,
                                name = part.Name,
                                qty = part.Qty,
                                length = part.Length,
                                width = part.Width,
                                thickness = part.Thickness,
                                material = part.Material
                            })
                        })
                    })
                }),
                hardware = workOrder.Hardware.Select(h => new
                {
                    id = h.Id,
                    name = h.Name,
                    qty = h.Qty
                }),
                detachedProducts = workOrder.DetachedProducts.Select(d => new
                {
                    id = d.Id,
                    productNumber = d.ProductNumber,
                    name = d.Name,
                    qty = d.Qty,
                    length = d.Length,
                    width = d.Width,
                    thickness = d.Thickness,
                    material = d.Material
                })
            };
            
            // Pre-serialize the data for JavaScript to avoid circular reference issues
            ViewBag.WorkOrderDataJson = System.Text.Json.JsonSerializer.Serialize(ViewBag.WorkOrderData);
            
            return View(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order for modification {WorkOrderId}", id);
            TempData["ErrorMessage"] = "An error occurred while loading the work order for modification.";
            return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> SaveModifications(string id, string workOrderName)
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

            // Update work order metadata
            if (!string.IsNullOrEmpty(workOrderName))
            {
                workOrder.Name = workOrderName.Trim();
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Work order updated successfully.";
            
            return RedirectToAction(nameof(WorkOrder), new { id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving work order modifications {WorkOrderId}", id);
            TempData["ErrorMessage"] = "An error occurred while saving the work order modifications.";
            return RedirectToAction(nameof(Modify), new { id = id });
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
}