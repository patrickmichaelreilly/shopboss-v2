using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models;
using System.Text.Json;

namespace ShopBoss.Web.Controllers;

public class ProjectController : Controller
{
    private readonly ProjectService _projectService;
    private readonly ProjectAttachmentService _attachmentService;
    private readonly PurchaseOrderService _purchaseOrderService;
    private readonly CustomWorkOrderService _customWorkOrderService;
    private readonly SmartSheetImportService _smartSheetImportService;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(
        ProjectService projectService,
        ProjectAttachmentService attachmentService,
        PurchaseOrderService purchaseOrderService,
        CustomWorkOrderService customWorkOrderService,
        SmartSheetImportService smartSheetImportService,
        ILogger<ProjectController> logger)
    {
        _projectService = projectService;
        _attachmentService = attachmentService;
        _purchaseOrderService = purchaseOrderService;
        _customWorkOrderService = customWorkOrderService;
        _smartSheetImportService = smartSheetImportService;
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
    public async Task<IActionResult> UploadFile(string projectId, string category, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "No file selected" });
            }

            var attachment = await _attachmentService.UploadAttachmentAsync(projectId, file, category);
            return Json(new { success = true, attachment = attachment });
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
    public async Task<IActionResult> UpdateFileCategory([FromBody] UpdateFileCategoryRequest request)
    {
        try
        {
            var success = await _attachmentService.UpdateAttachmentCategoryAsync(request.Id, request.Category);
            if (success)
            {
                return Json(new { success = true });
            }
            
            return Json(new { success = false, message = "File not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating file category {FileId}", request.Id);
            return Json(new { success = false, message = "Error updating file category" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] PurchaseOrder purchaseOrder)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Model validation failed for purchase order: {Errors}", string.Join(", ", errors));
                return Json(new { success = false, message = $"Invalid purchase order data: {string.Join(", ", errors)}" });
            }

            var createdPurchaseOrder = await _purchaseOrderService.CreatePurchaseOrderAsync(purchaseOrder);
            return Json(new { success = true, purchaseOrder = createdPurchaseOrder });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order for project {ProjectId}", purchaseOrder.ProjectId);
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
    public async Task<IActionResult> CreateCustomWorkOrder([FromBody] CustomWorkOrder customWorkOrder)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Model validation failed for custom work order: {Errors}", string.Join(", ", errors));
                return Json(new { success = false, message = $"Invalid custom work order data: {string.Join(", ", errors)}" });
            }

            var createdCustomWorkOrder = await _customWorkOrderService.CreateCustomWorkOrderAsync(customWorkOrder);
            return Json(new { success = true, customWorkOrder = createdCustomWorkOrder });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating custom work order for project {ProjectId}", customWorkOrder.ProjectId);
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
    public async Task<IActionResult> ListSmartSheets()
    {
        try
        {
            var sheets = await _smartSheetImportService.ListAvailableSheetsAsync();
            return Json(new { success = true, sheets = sheets });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing SmartSheet sheets");
            return Json(new { success = false, message = "Error loading SmartSheet sheets" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ImportFromSmartSheet()
    {
        try
        {
            var result = await _smartSheetImportService.ImportProjectsFromMasterListAsync();
            
            if (result.Success)
            {
                return Json(new 
                { 
                    success = true, 
                    message = $"Import completed successfully. Created: {result.ProjectsCreated}, Skipped: {result.ProjectsSkipped}, Errors: {result.ProjectsWithErrors}",
                    result = result
                });
            }
            else
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SmartSheet import");
            return Json(new { success = false, message = "Error during import process" });
        }
    }

    public class AttachWorkOrdersRequest
    {
        public List<string> WorkOrderIds { get; set; } = new();
        public string ProjectId { get; set; } = string.Empty;
    }

    public class UpdateFileCategoryRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

}