using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models;
using ShopBoss.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ShopBoss.Web.Controllers;

public class SmartSheetMigrationController : Controller
{
    private readonly SmartSheetImportService _smartSheetService;
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SmartSheetMigrationController> _logger;

    public SmartSheetMigrationController(
        SmartSheetImportService smartSheetService,
        ShopBossDbContext context,
        ILogger<SmartSheetMigrationController> logger)
    {
        _smartSheetService = smartSheetService;
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var workspaces = await _smartSheetService.GetWorkspacesAsync();
            return View(workspaces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SmartSheet workspaces");
            ViewBag.Error = $"Failed to load workspaces: {ex.Message}";
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSheetDetails(long sheetId)
    {
        _logger.LogInformation("GetSheetDetails called with sheetId: {SheetId}", sheetId);
        
        try
        {
            _logger.LogInformation("Calling SmartSheetService.GetSheetDetailsAsync for sheet {SheetId}", sheetId);
            var details = await _smartSheetService.GetSheetDetailsAsync(sheetId);
            
            _logger.LogInformation("Successfully retrieved sheet details for {SheetId}. Summary count: {SummaryCount}, Attachments: {AttachmentCount}, Comments: {CommentCount}", 
                sheetId, 
                details.Summary?.Count ?? 0, 
                details.Attachments?.Count ?? 0, 
                details.Comments?.Count ?? 0);
            
            return Json(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sheet details for {SheetId}. Error: {ErrorMessage}", sheetId, ex.Message);
            return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ImportProject([FromBody] ImportProjectRequest request)
    {
        try
        {
            var result = await _smartSheetService.ImportProjectAsync(request);
            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import project from sheet {SheetId}", request.SheetId);
            return Json(new { success = false, message = ex.Message });
        }
    }
}