using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models;
using System.Text.Json;

namespace ShopBoss.Web.Controllers;

public class ProjectController : Controller
{
    private readonly ProjectService _projectService;
    private readonly ProjectAttachmentService _attachmentService;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(
        ProjectService projectService,
        ProjectAttachmentService attachmentService,
        ILogger<ProjectController> logger)
    {
        _projectService = projectService;
        _attachmentService = attachmentService;
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

    public class AttachWorkOrdersRequest
    {
        public List<string> WorkOrderIds { get; set; } = new();
        public string ProjectId { get; set; } = string.Empty;
    }
}