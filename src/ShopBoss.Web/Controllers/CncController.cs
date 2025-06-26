using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Controllers;

public class CncController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<CncController> _logger;
    private readonly IHubContext<StatusHub> _hubContext;

    public CncController(ShopBossDbContext context, ILogger<CncController> logger, IHubContext<StatusHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Get active work order from session
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                ViewBag.ErrorMessage = "No active work order selected. Please set an active work order from the Admin station.";
                return View(new List<NestSheet>());
            }

            // Get nest sheets for active work order (excluding default nest sheet)
            var nestSheets = await _context.NestSheets
                .Include(n => n.Parts)
                .Where(n => n.WorkOrderId == activeWorkOrderId && n.Name != "Default Nest Sheet")
                .OrderBy(n => n.Name)
                .ToListAsync();

            // Get active work order details for display
            var activeWorkOrder = await _context.WorkOrders.FindAsync(activeWorkOrderId);
            ViewBag.ActiveWorkOrderId = activeWorkOrderId;
            ViewBag.ActiveWorkOrderName = activeWorkOrder?.Name ?? "Unknown";

            return View(nestSheets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nest sheets for CNC station");
            TempData["ErrorMessage"] = "An error occurred while loading the nest sheets.";
            return View(new List<NestSheet>());
        }
    }

    [HttpPost]
    public async Task<IActionResult> ProcessNestSheet(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Json(new { success = false, message = "Barcode is required." });
        }

        try
        {
            // Get active work order from session
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected." });
            }

            // Find nest sheet by barcode/name within the active work order
            var nestSheet = await _context.NestSheets
                .Include(n => n.Parts)
                .FirstOrDefaultAsync(n => n.WorkOrderId == activeWorkOrderId && 
                                         (n.Barcode == barcode.Trim() || n.Name == barcode.Trim()));

            if (nestSheet == null)
            {
                return Json(new { success = false, message = $"Nest sheet with barcode/name '{barcode}' not found in active work order." });
            }

            if (nestSheet.IsProcessed)
            {
                return Json(new { success = false, message = $"Nest sheet '{nestSheet.Name}' has already been processed." });
            }

            // Mark nest sheet as processed
            nestSheet.IsProcessed = true;
            nestSheet.ProcessedDate = DateTime.UtcNow;

            // Mark all parts on this nest sheet as Cut
            var partsToUpdate = nestSheet.Parts.Where(p => p.Status == PartStatus.Pending).ToList();
            foreach (var part in partsToUpdate)
            {
                part.Status = PartStatus.Cut;
                part.StatusUpdatedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Send real-time updates via SignalR
            await _hubContext.Clients.Groups($"workorder-{nestSheet.WorkOrderId}")
                .SendAsync("NestSheetProcessed", new
                {
                    nestSheetId = nestSheet.Id,
                    nestSheetName = nestSheet.Name,
                    partsProcessed = partsToUpdate.Count,
                    processedDate = nestSheet.ProcessedDate?.ToString("yyyy-MM-dd HH:mm")
                });

            await _hubContext.Clients.Group("cnc-station")
                .SendAsync("StatusUpdate", new
                {
                    type = "nest-sheet-processed",
                    nestSheetId = nestSheet.Id,
                    nestSheetName = nestSheet.Name,
                    partsProcessed = partsToUpdate.Count
                });

            _logger.LogInformation("Processed nest sheet {NestSheetId} - {PartsCount} parts marked as Cut", 
                nestSheet.Id, partsToUpdate.Count);

            return Json(new { 
                success = true, 
                message = $"Successfully processed nest sheet '{nestSheet.Name}'. {partsToUpdate.Count} parts marked as Cut.",
                nestSheetId = nestSheet.Id,
                partsProcessed = partsToUpdate.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing nest sheet with barcode {Barcode}", barcode);
            return Json(new { success = false, message = "An error occurred while processing the nest sheet." });
        }
    }

    // Manual nest sheet creation removed - nest sheets should only come from import process

    public async Task<IActionResult> GetNestSheetDetails(string id)
    {
        try
        {
            var nestSheet = await _context.NestSheets
                .Include(n => n.Parts)
                .ThenInclude(p => p.Product)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (nestSheet == null)
            {
                return Json(new { success = false, message = "Nest sheet not found." });
            }

            var details = new
            {
                success = true,
                nestSheet = new
                {
                    id = nestSheet.Id,
                    name = nestSheet.Name,
                    material = nestSheet.Material,
                    length = nestSheet.Length,
                    width = nestSheet.Width,
                    thickness = nestSheet.Thickness,
                    barcode = nestSheet.Barcode,
                    isProcessed = nestSheet.IsProcessed,
                    processedDate = nestSheet.ProcessedDate?.ToString("yyyy-MM-dd HH:mm"),
                    createdDate = nestSheet.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                    partCount = nestSheet.Parts.Count,
                    cutPartCount = nestSheet.Parts.Count(p => p.Status >= PartStatus.Cut),
                    parts = nestSheet.Parts.Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        qty = p.Qty,
                        status = p.Status.ToString(),
                        productName = p.Product?.Name ?? "Unknown",
                        material = p.Material,
                        length = p.Length,
                        width = p.Width,
                        thickness = p.Thickness
                    })
                }
            };

            return Json(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nest sheet details for {NestSheetId}", id);
            return Json(new { success = false, message = "An error occurred while retrieving nest sheet details." });
        }
    }
}