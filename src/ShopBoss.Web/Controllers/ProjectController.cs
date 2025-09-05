using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models;
using ShopBoss.Web.Data;
using System.Text.Json;

namespace ShopBoss.Web.Controllers;

public class ProjectController : Controller
{
    private readonly ProjectService _projectService;
    private readonly ProjectAttachmentService _attachmentService;
    private readonly PurchaseOrderService _purchaseOrderService;
    private readonly CustomWorkOrderService _customWorkOrderService;
    private readonly SmartSheetService _smartSheetService;
    private readonly ShopBossDbContext _context;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(
        ProjectService projectService,
        ProjectAttachmentService attachmentService,
        PurchaseOrderService purchaseOrderService,
        CustomWorkOrderService customWorkOrderService,
        SmartSheetService smartSheetService,
        ShopBossDbContext context,
        ILogger<ProjectController> logger)
    {
        _projectService = projectService;
        _attachmentService = attachmentService;
        _purchaseOrderService = purchaseOrderService;
        _customWorkOrderService = customWorkOrderService;
        _smartSheetService = smartSheetService;
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string search = "", bool includeArchived = false, ProjectCategory? projectCategory = null)
    {
        try
        {
            var projects = await _projectService.GetProjectSummariesAsync(search, includeArchived, projectCategory);
            var unassignedWorkOrders = await _projectService.GetUnassignedWorkOrdersAsync();

            ViewBag.Search = search;
            ViewBag.IncludeArchived = includeArchived;
            ViewBag.ProjectCategory = projectCategory;
            ViewBag.UnassignedWorkOrders = unassignedWorkOrders;

            return View(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading project index");
            TempData["Error"] = "Error loading projects. Please try again.";
            return View(new List<Project>());
        }
    }

    // Details view is deprecated; project details render via partials on index.

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Project project)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                return Json(new { success = false, message = $"Invalid project data: {string.Join(", ", errors)}" });
            }

            var createdProject = await _projectService.CreateProjectAsync(project);
            return Json(new { success = true, project = createdProject });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Business logic error creating project: {Message}", ex.Message);
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            return Json(new { success = false, message = "Error creating project" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Update([FromBody] Project project)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid project data" });
            }

            var updatedProject = await _projectService.UpdateProjectAsync(project);
            return Json(new { success = true, project = updatedProject });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project");
            return Json(new { success = false, message = "Error updating project" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Archive(string id)
    {
        try
        {
            var success = await _projectService.ArchiveProjectAsync(id);
            if (success)
            {
                TempData["Success"] = "Project archived successfully";
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "Project not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving project {ProjectId}", id);
            return Json(new { success = false, message = "Error archiving project" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Unarchive(string id)
    {
        try
        {
            var success = await _projectService.UnarchiveProjectAsync(id);
            if (success)
            {
                TempData["Success"] = "Project unarchived successfully";
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "Project not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unarchiving project {ProjectId}", id);
            return Json(new { success = false, message = "Error unarchiving project" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var success = await _projectService.DeleteProjectAsync(id);
            if (success)
            {
                // Clean up project files
                await _attachmentService.CleanupProjectFilesAsync(id);
                TempData["Success"] = "Project deleted successfully";
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "Project not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", id);
            return Json(new { success = false, message = "Error deleting project" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AttachWorkOrders([FromBody] AttachWorkOrdersRequest request)
    {
        try
        {
            var success = await _projectService.AttachWorkOrdersToProjectAsync(request.WorkOrderIds, request.ProjectId);
            if (success)
            {
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "Error attaching work orders" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching work orders to project {ProjectId}", request.ProjectId);
            return Json(new { success = false, message = "Error attaching work orders" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DetachWorkOrder(string workOrderId)
    {
        try
        {
            var success = await _projectService.DetachWorkOrderFromProjectAsync(workOrderId);
            if (success)
            {
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "Work order not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detaching work order {WorkOrderId}", workOrderId);
            return Json(new { success = false, message = "Error detaching work order" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(string projectId, string label, IFormFile file, string? comment = null, string? taskBlockId = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "No file selected" });
            }

            // Get SmartSheet user from session for attribution, fallback to "Local User"
            var smartSheetUser = HttpContext.Session.GetString("ss_user") ?? "Local User";

            var attachment = await _attachmentService.UploadAttachmentAsync(projectId, file, label, uploadedBy: smartSheetUser, comment: comment, taskBlockId: taskBlockId);
            // Return a trimmed payload to avoid JSON cycles
            return Json(new { success = true, attachment = new { attachment.Id, attachment.OriginalFileName, attachment.FileSize, attachment.Label } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file for project {ProjectId}", projectId);
            return Json(new { success = false, message = "Error uploading file" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DownloadFile(string id)
    {
        try
        {
            var (stream, contentType, fileName) = await _attachmentService.DownloadAttachmentAsync(id);
            
            if (stream == null)
            {
                return NotFound();
            }

            return File(stream, contentType ?? "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileId}", id);
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFile(string id)
    {
        try
        {
            // Expect an Attachment Id; delete both the file and any linked timeline events
            var success = await _attachmentService.DeleteAttachmentAsync(id);
            if (success)
            {
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "File not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileId}", id);
            return Json(new { success = false, message = "Error deleting file" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateAttachmentLabel([FromBody] UpdateAttachmentLabelRequest request)
    {
        try
        {
            var success = await _attachmentService.UpdateAttachmentLabelAsync(request.Id, request.Label);
            if (success)
            {
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "File not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attachment label {AttachmentId}", request.Id);
            return Json(new { success = false, message = "Error updating attachment label" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Model validation failed for purchase order: {Errors}", string.Join(", ", errors));
                return Json(new { success = false, message = $"Invalid purchase order data: {string.Join(", ", errors)}" });
            }

            // Check SmartSheet authentication for event attribution, fallback to "Local User"
            var smartSheetUser = HttpContext.Session.GetString("ss_user") ?? "Local User";

            var createdPurchaseOrder = await _purchaseOrderService.CreatePurchaseOrderAsync(request.PurchaseOrder, request.TaskBlockId, smartSheetUser);
            return Json(new { success = true, purchaseOrder = createdPurchaseOrder });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order for project {ProjectId}", request.PurchaseOrder.ProjectId);
            return Json(new { success = false, message = "Error creating purchase order" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePurchaseOrder([FromBody] PurchaseOrder purchaseOrder)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid purchase order data" });
            }

            var updatedPurchaseOrder = await _purchaseOrderService.UpdatePurchaseOrderAsync(purchaseOrder);
            if (updatedPurchaseOrder != null)
            {
                return Json(new { success = true, purchaseOrder = updatedPurchaseOrder });
            }
            
            return Json(new { success = false, message = "Purchase order not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating purchase order {PurchaseOrderId}", purchaseOrder.Id);
            return Json(new { success = false, message = "Error updating purchase order" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeletePurchaseOrder(string id)
    {
        try
        {
            var success = await _purchaseOrderService.DeletePurchaseOrderAsync(id);
            if (success)
            {
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "Purchase order not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting purchase order {PurchaseOrderId}", id);
            return Json(new { success = false, message = "Error deleting purchase order" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomWorkOrder([FromBody] CreateCustomWorkOrderRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Model validation failed for custom work order: {Errors}", string.Join(", ", errors));
                return Json(new { success = false, message = $"Invalid custom work order data: {string.Join(", ", errors)}" });
            }

            // Check SmartSheet authentication for event attribution, fallback to "Local User"
            var smartSheetUser = HttpContext.Session.GetString("ss_user") ?? "Local User";

            var createdCustomWorkOrder = await _customWorkOrderService.CreateCustomWorkOrderAsync(request.CustomWorkOrder, request.TaskBlockId, smartSheetUser);
            return Json(new { success = true, customWorkOrder = createdCustomWorkOrder });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating custom work order for project {ProjectId}", request.CustomWorkOrder.ProjectId);
            return Json(new { success = false, message = "Error creating custom work order" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCustomWorkOrder([FromBody] CustomWorkOrder customWorkOrder)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid custom work order data" });
            }

            var updatedCustomWorkOrder = await _customWorkOrderService.UpdateCustomWorkOrderAsync(customWorkOrder);
            if (updatedCustomWorkOrder != null)
            {
                return Json(new { success = true, customWorkOrder = updatedCustomWorkOrder });
            }
            
            return Json(new { success = false, message = "Custom work order not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating custom work order {CustomWorkOrderId}", customWorkOrder.Id);
            return Json(new { success = false, message = "Error updating custom work order" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCustomWorkOrder(string id)
    {
        try
        {
            var success = await _customWorkOrderService.DeleteCustomWorkOrderAsync(id);
            if (success)
            {
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "Custom work order not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting custom work order {CustomWorkOrderId}", id);
            return Json(new { success = false, message = "Error deleting custom work order" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPurchaseOrder(string id)
    {
        try
        {
            var purchaseOrder = await _purchaseOrderService.GetPurchaseOrderByIdAsync(id);
            if (purchaseOrder != null)
            {
                return Json(new { success = true, purchaseOrder = purchaseOrder });
            }
            
            return Json(new { success = false, message = "Purchase order not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting purchase order {PurchaseOrderId}", id);
            return Json(new { success = false, message = "Error loading purchase order" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomWorkOrder(string id)
    {
        try
        {
            var customWorkOrder = await _customWorkOrderService.GetCustomWorkOrderByIdAsync(id);
            if (customWorkOrder != null)
            {
                return Json(new { success = true, customWorkOrder = customWorkOrder });
            }
            
            return Json(new { success = false, message = "Custom work order not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting custom work order {CustomWorkOrderId}", id);
            return Json(new { success = false, message = "Error loading custom work order" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetUnassignedWorkOrders()
    {
        try
        {
            var unassignedWorkOrders = await _projectService.GetUnassignedWorkOrdersAsync();
            return Json(new { success = true, workOrders = unassignedWorkOrders });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unassigned work orders");
            return Json(new { success = false, message = "Error loading unassigned work orders" });
        }
    }

    // SmartSheet Integration Actions

    [HttpGet]
    public IActionResult GetSmartSheetSessionStatus()
    {
        try
        {
            var hasSession = _smartSheetService.HasSmartSheetSession();
            var userEmail = _smartSheetService.GetCurrentUserEmail();
            
            return Json(new { 
                success = true, 
                hasSession = hasSession, 
                userEmail = userEmail 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Smartsheet session status");
            return Json(new { success = false, message = "Error checking Smartsheet connection" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSmartSheetInfo(long sheetId)
    {
        try
        {
            var sheetInfo = await _smartSheetService.GetSheetInfoAsync(sheetId);
            if (sheetInfo != null)
            {
                return Json(new { success = true, sheet = sheetInfo });
            }
            
                return Json(new { success = false, message = "Smartsheet not found or not accessible" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Smartsheet info for sheet {SheetId}", sheetId);
            return Json(new { success = false, message = "Error loading Smartsheet information" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> SearchSmartSheets(string searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Json(new { success = true, sheets = new List<object>() });
            }

            var sheets = await _smartSheetService.SearchSheetsAsync(searchTerm);
            return Json(new { success = true, sheets = sheets });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Smartsheets with term '{SearchTerm}'", searchTerm);
            return Json(new { success = false, message = "Error searching Smartsheets" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> LinkProjectToSmartSheet([FromBody] LinkProjectToSmartSheetRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ProjectId) || request.SheetId <= 0)
            {
                return Json(new { success = false, message = "Invalid project or sheet ID" });
            }

            var success = await _smartSheetService.LinkProjectToSheetAsync(request.ProjectId, request.SheetId);
            if (success)
            {
                return Json(new { success = true, message = "Project successfully linked to Smartsheet" });
            }
            
            return Json(new { success = false, message = "Failed to link project to Smartsheet" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking project {ProjectId} to Smartsheet {SheetId}", request.ProjectId, request.SheetId);
            return Json(new { success = false, message = "Error linking to Smartsheet" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UnlinkProjectFromSmartSheet([FromBody] UnlinkProjectFromSmartSheetRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ProjectId))
            {
                return Json(new { success = false, message = "Invalid project ID" });
            }

            var success = await _smartSheetService.UnlinkProjectFromSheetAsync(request.ProjectId);
            if (success)
            {
                return Json(new { success = true, message = "Project successfully unlinked from Smartsheet" });
            }
            
            return Json(new { success = false, message = "Failed to unlink project from Smartsheet" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking project {ProjectId} from SmartSheet", request.ProjectId);
            return Json(new { success = false, message = "Error unlinking from Smartsheet" });
        }
    }

    /// <summary>
    /// Get specific SmartSheet workspace by ID or name
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWorkspace(long? workspaceId, string? workspaceName)
    {
        try
        {
            if (!_smartSheetService.HasSmartSheetSession())
            {
                return Json(new { success = false, message = "No Smartsheet session. Please authenticate first." });
            }

            object? workspace = null;
            
            if (workspaceId.HasValue)
            {
                workspace = await _smartSheetService.GetWorkspaceByIdAsync(workspaceId.Value);
            }
            else if (!string.IsNullOrEmpty(workspaceName))
            {
                workspace = await _smartSheetService.GetWorkspaceByNameAsync(workspaceName);
            }
            else
            {
                return Json(new { success = false, message = "Either workspaceId or workspaceName must be provided." });
            }

            if (workspace == null)
            {
                return Json(new { success = false, message = "Workspace not found or inaccessible." });
            }

            return Json(new { success = true, workspace = workspace });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workspace with ID {WorkspaceId} or name '{WorkspaceName}'", workspaceId, workspaceName);
            return Json(new { success = false, message = "Error retrieving workspace" });
        }
    }

    /// <summary>
    /// Get SmartSheet workspaces with sheets for browsing
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSmartSheetWorkspaces()
    {
        try
        {
            if (!_smartSheetService.HasSmartSheetSession())
            {
                return Json(new { success = false, message = "No Smartsheet session. Please authenticate first." });
            }

            var workspaces = await _smartSheetService.GetAccessibleWorkspacesAsync();
            return Json(new { success = true, workspaces = workspaces });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Smartsheet workspaces");
            return Json(new { success = false, message = "Error retrieving workspaces" });
        }
    }

    /// <summary>
    /// Get detailed SmartSheet data for a specific sheet
    /// </summary>



    [HttpPost]
    public async Task<IActionResult> CreateComment([FromBody] CreateCommentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ProjectId))
            {
                return Json(new { success = false, message = "Project ID is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return Json(new { success = false, message = "Comment description is required" });
            }

            // Get Smartsheet user from session for attribution, fallback to "Local User"
            var smartSheetUser = HttpContext.Session.GetString("ss_user") ?? "Local User";

            // Create new ProjectEvent for the comment
            var projectEvent = new ProjectEvent
            {
                ProjectId = request.ProjectId,
                EventDate = request.EventDate,
                EventType = "comment",
                Description = request.Description,
                CreatedBy = smartSheetUser, // Use SmartSheet user instead of client-provided value
                TaskBlockId = request.TaskBlockId
            };

            var success = await _projectService.AddProjectEventAsync(projectEvent);
            
            if (success)
            {
                return Json(new { success = true, message = "Comment added successfully" });
            }
            else
            {
                return Json(new { success = false, message = "Failed to add comment" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment for project {ProjectId}", request.ProjectId);
            return Json(new { success = false, message = "An error occurred while adding the comment" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEventDescription([FromBody] UpdateEventDescriptionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.EventId))
            {
                return Json(new { success = false, message = "Event ID is required" });
            }

            var projectEvent = await _context.ProjectEvents.FindAsync(request.EventId);
            if (projectEvent == null)
            {
                return Json(new { success = false, message = "Event not found" });
            }

            projectEvent.Description = request.Description ?? "";
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Comment updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating event description for event {EventId}", request.EventId);
            return Json(new { success = false, message = "An error occurred while updating the comment" });
        }
    }

    public class AttachWorkOrdersRequest
    {
        public List<string> WorkOrderIds { get; set; } = new();
        public string ProjectId { get; set; } = string.Empty;
    }

    public class UpdateAttachmentLabelRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class LinkProjectToSmartSheetRequest
    {
        public string ProjectId { get; set; } = string.Empty;
        public long SheetId { get; set; }
    }

    public class UnlinkProjectFromSmartSheetRequest
    {
        public string ProjectId { get; set; } = string.Empty;
    }


    public class CreateCommentRequest
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? TaskBlockId { get; set; }
    }

    public class UpdateEventDescriptionRequest
    {
        public string EventId { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteComment([FromBody] DeleteCommentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.EventId))
            {
                return Json(new { success = false, message = "Event ID is required" });
            }

            var projectEvent = await _context.ProjectEvents.FindAsync(request.EventId);
            if (projectEvent == null)
            {
                return Json(new { success = false, message = "Event not found" });
            }

            if (!string.Equals(projectEvent.EventType, "comment", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Unsupported event type for this endpoint" });
            }

            _context.ProjectEvents.Remove(projectEvent);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment event {EventId}", request.EventId);
            return Json(new { success = false, message = "An error occurred while deleting the comment" });
        }
    }

    public class DeleteCommentRequest
    {
        public string EventId { get; set; } = string.Empty;
    }

    public class CreateCustomWorkOrderRequest
    {
        public CustomWorkOrder CustomWorkOrder { get; set; } = new();
        public string? TaskBlockId { get; set; }
    }

    public class CreatePurchaseOrderRequest
    {
        public PurchaseOrder PurchaseOrder { get; set; } = new();
        public string? TaskBlockId { get; set; }
    }

    /// <summary>
    /// TEST ENDPOINT: Explore template sheet structure
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExploreTemplateSheet()
    {
        try
        {
            const long templateSheetId = 2455059368464260;
            
            // Get Smartsheet client directly to access full sheet data
            var smartsheetClient = _smartSheetService.GetType()
                .GetMethod("GetSmartSheetClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .Invoke(_smartSheetService, null) as Smartsheet.Api.SmartsheetClient;
                
            if (smartsheetClient == null)
            {
                return Json(new { success = false, message = "No Smartsheet session. Please authenticate first." });
            }
            
            // Get full sheet data including columns
            var sheet = await Task.Run(() => smartsheetClient.SheetResources.GetSheet(templateSheetId, null, null, null, null, null, null, null));
            
            // Extract column information
            var columns = sheet.Columns?.Select(col => new {
                Id = col.Id,
                Title = col.Title,
                Type = col.Type?.ToString(),
                Primary = col.Primary,
                Index = col.Index,
                Width = col.Width,
                Hidden = col.Hidden,
                Symbol = col.Symbol?.ToString(),
                SystemColumnType = col.SystemColumnType?.ToString(),
                AutoNumberFormat = col.AutoNumberFormat,
                Options = col.Options
            }).ToList();
            
            // Get basic sheet info
            var sheetInfo = await _smartSheetService.GetSheetInfoAsync(templateSheetId);
            
            // Get first few rows as examples
            var exampleRows = sheet.Rows?.Take(3).Select(row => new {
                Id = row.Id,
                RowNumber = row.RowNumber,
                ParentId = row.ParentId,
                Cells = row.Cells?.Select(cell => new {
                    ColumnId = cell.ColumnId,
                    Value = cell.Value?.ToString(),
                    DisplayValue = cell.DisplayValue
                }).ToList()
            }).ToList();
            
            return Json(new { 
                success = true, 
                templateSheetId = templateSheetId,
                sheetInfo = sheetInfo,
                sheetName = sheet.Name,
                totalRowCount = sheet.TotalRowCount,
                columns = columns,
                exampleRows = exampleRows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exploring template sheet");
            return Json(new { success = false, message = "Error exploring template sheet: " + ex.Message });
        }
    }

}
