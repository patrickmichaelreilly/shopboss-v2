using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models;
using ShopBoss.Web.Data;
using Microsoft.EntityFrameworkCore;
using Smartsheet.Api;
using Smartsheet.Api.Models;

namespace ShopBoss.Web.Controllers;

public class SmartSheetMigrationController : Controller
{
    private readonly SmartSheetService _smartSheetService;
    private readonly ProjectAttachmentService _attachmentService;
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SmartSheetMigrationController> _logger;

    public SmartSheetMigrationController(
        SmartSheetService smartSheetService,
        ProjectAttachmentService attachmentService,
        ShopBossDbContext context,
        ILogger<SmartSheetMigrationController> logger)
    {
        _smartSheetService = smartSheetService;
        _attachmentService = attachmentService;
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Check if user is authenticated with SmartSheet
            if (!_smartSheetService.HasSmartSheetSession())
            {
                ViewBag.Error = "Please authenticate with SmartSheet first. Use the SmartSheet indicator in the header.";
                return View();
            }

            var workspaces = await _smartSheetService.GetAccessibleWorkspacesAsync();
            
            // Transform to the format expected by the view
            var result = new WorkspaceListResult();
            
            foreach (var workspace in workspaces)
            {
                var workspaceData = workspace as dynamic;
                var sheets = ((IEnumerable<object>)workspaceData.sheets).Select(s =>
                {
                    var sheetData = s as dynamic;
                    return new SheetInfo
                    {
                        Id = (long)sheetData.id,
                        Name = (string)sheetData.name,
                        ModifiedAt = sheetData.modifiedAt as DateTime?
                    };
                }).ToList();

                if ((string)workspaceData.name == "Active Jobs")
                {
                    result.ActiveJobs = sheets;
                }
                else if ((string)workspaceData.name == "_Archived Jobs")
                {
                    result.ArchivedJobs = sheets;
                }
            }
            
            return View(result);
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
            // Check if user is authenticated
            if (!_smartSheetService.HasSmartSheetSession())
            {
                return Json(new { error = "Not authenticated with SmartSheet. Please authenticate first." });
            }

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
            // Check if user is authenticated
            if (!_smartSheetService.HasSmartSheetSession())
            {
                return Json(new { success = false, message = "Not authenticated with SmartSheet. Please authenticate first." });
            }

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