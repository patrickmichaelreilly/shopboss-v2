using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;

namespace ShopBoss.Web.Controllers;

public class TimelineController : Controller
{
    private readonly TimelineService _timelineService;
    private readonly ILogger<TimelineController> _logger;

    public TimelineController(TimelineService timelineService, ILogger<TimelineController> logger)
    {
        _timelineService = timelineService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string projectId)
    {
        try
        {
            var timelineData = await _timelineService.GetTimelineDataAsync(projectId);
            return PartialView("_Timeline", timelineData);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Timeline requested for invalid project: {ProjectId}", projectId);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading timeline for project: {ProjectId}", projectId);
            return Json(new { success = false, message = "Error loading timeline. Please try again." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateBlock([FromBody] CreateBlockRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Json(new { success = false, message = "Block name is required." });
            }

            // Get Smartsheet user for attribution, fallback to "Local User"
            var smartSheetUser = HttpContext.Session.GetString("ss_user") ?? "Local User";

            var block = await _timelineService.CreateTaskBlockAsync(request.ProjectId, request.Name, request.Description);
            return Json(new { success = true, block = new { block.Id, block.Name, block.Description, block.DisplayOrder } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating TaskBlock for project: {ProjectId}", request.ProjectId);
            return Json(new { success = false, message = "Error creating block. Please try again." });
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateBlock([FromBody] UpdateBlockRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Json(new { success = false, message = "Block name is required." });
            }

            var block = await _timelineService.UpdateTaskBlockAsync(request.BlockId, request.Name, request.Description);
            if (block == null)
            {
                return Json(new { success = false, message = "Block not found." });
            }

            return Json(new { success = true, block = new { block.Id, block.Name, block.Description } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating TaskBlock: {BlockId}", request.BlockId);
            return Json(new { success = false, message = "Error updating block. Please try again." });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteBlock(string blockId)
    {
        try
        {
            var success = await _timelineService.DeleteTaskBlockAsync(blockId);
            if (!success)
            {
                return Json(new { success = false, message = "Block not found." });
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TaskBlock: {BlockId}", blockId);
            return Json(new { success = false, message = "Error deleting block. Please try again." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AssignEvents([FromBody] AssignEventsRequest request)
    {
        try
        {
            if (request.EventIds == null || !request.EventIds.Any())
            {
                return Json(new { success = false, message = "No events specified." });
            }

            var success = await _timelineService.AssignEventsToBlockAsync(request.BlockId, request.EventIds);
            return Json(new { success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning events to TaskBlock: {BlockId}", request.BlockId);
            return Json(new { success = false, message = "Error assigning events. Please try again." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UnassignEvents([FromBody] UnassignEventsRequest request)
    {
        try
        {
            if (request.EventIds == null || !request.EventIds.Any())
            {
                return Json(new { success = false, message = "No events specified." });
            }

            var success = await _timelineService.UnassignEventsFromBlockAsync(request.EventIds);
            return Json(new { success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning events from blocks");
            return Json(new { success = false, message = "Error unassigning events. Please try again." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReorderBlocks([FromBody] ReorderBlocksRequest request)
    {
        try
        {
            if (request.BlockIds == null || !request.BlockIds.Any())
            {
                return Json(new { success = false, message = "No blocks specified." });
            }

            var success = await _timelineService.ReorderTaskBlocksAsync(request.ProjectId, request.BlockIds);
            return Json(new { success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering TaskBlocks for project: {ProjectId}", request.ProjectId);
            return Json(new { success = false, message = "Error reordering blocks. Please try again." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReorderEventsInBlock([FromBody] ReorderEventsRequest request)
    {
        try
        {
            if (request.EventIds == null || !request.EventIds.Any())
            {
                return Json(new { success = false, message = "No events specified." });
            }

            var success = await _timelineService.ReorderEventsInBlockAsync(request.BlockId, request.EventIds);
            return Json(new { success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering events in TaskBlock: {BlockId}", request.BlockId);
            return Json(new { success = false, message = "Error reordering events. Please try again." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReorderMixedItems([FromBody] ReorderMixedItemsRequest request)
    {
        try
        {
            var items = request.Items.Select(i => (i.Type, i.Id, i.Order)).ToList();
            await _timelineService.ReorderMixedTimelineItemsAsync(request.ProjectId, items);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering mixed timeline items for project: {ProjectId}", request.ProjectId);
            return Json(new { success = false, message = "Error reordering timeline items. Please try again." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> NestTaskBlock([FromBody] NestTaskBlockRequest request)
    {
        try
        {
            var success = await _timelineService.NestTaskBlockAsync(request.ChildBlockId, request.ParentBlockId);
            if (success)
            {
                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = "Could not nest task block. Check for circular references." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error nesting TaskBlock {ChildId} under {ParentId}", request.ChildBlockId, request.ParentBlockId);
            return Json(new { success = false, message = "Error nesting task block. Please try again." });
        }
    }
}

// Request DTOs
public class CreateBlockRequest
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateBlockRequest
{
    public string BlockId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AssignEventsRequest
{
    public string BlockId { get; set; } = string.Empty;
    public List<string> EventIds { get; set; } = new();
}

public class UnassignEventsRequest
{
    public List<string> EventIds { get; set; } = new();
}

public class ReorderBlocksRequest
{
    public string ProjectId { get; set; } = string.Empty;
    public List<string> BlockIds { get; set; } = new();
}

public class ReorderEventsRequest
{
    public string BlockId { get; set; } = string.Empty;
    public List<string> EventIds { get; set; } = new();
}

public class ReorderMixedItemsRequest
{
    public string ProjectId { get; set; } = string.Empty;
    public List<MixedTimelineItemOrder> Items { get; set; } = new();
}

public class MixedTimelineItemOrder
{
    public string Type { get; set; } = string.Empty; // "TaskBlock" or "Event"
    public string Id { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class NestTaskBlockRequest
{
    public string ChildBlockId { get; set; } = string.Empty;
    public string? ParentBlockId { get; set; } // null to unnest (move to root)
}
