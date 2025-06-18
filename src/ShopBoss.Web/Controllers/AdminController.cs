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

    public async Task<IActionResult> Index()
    {
        try
        {
            var workOrders = await _context.WorkOrders
                .Include(w => w.Products)
                .Include(w => w.Hardware)
                .Include(w => w.DetachedProducts)
                .OrderByDescending(w => w.ImportedDate)
                .ToListAsync();

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