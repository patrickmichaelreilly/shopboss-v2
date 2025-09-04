using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Smartsheet.Api.Models;

namespace ShopBoss.Web.Services;

public class SmartSheetSyncService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SmartSheetSyncService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    
    private static readonly JsonSerializerOptions SmartSheetJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly SmartSheetService _smartSheetService;
    
    // Column mapping cache: sheetId -> columnTitle -> columnId
    private readonly Dictionary<long, Dictionary<string, long>> _columnCache = new();

    public SmartSheetSyncService(
        ShopBossDbContext context,
        ILogger<SmartSheetSyncService> logger,
        HttpClient httpClient,
        IConfiguration configuration,
        SmartSheetService smartSheetService)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
        _smartSheetService = smartSheetService;
    }


    public async Task<SmartSheetSyncResult> SyncProjectEventsAsync(string projectId, string accessToken)
    {
        try
        {
            // Attempt token refresh before sync operations
            var refreshedToken = await EnsureValidTokenAsync(accessToken);
            if (refreshedToken == null)
                return SmartSheetSyncResult.CreateError("Authentication failed. Please re-authenticate with Smartsheet.");
            
            accessToken = refreshedToken;
            var project = await _context.Projects
                .Include(p => p.Events)
                .Include(p => p.TaskBlocks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return SmartSheetSyncResult.CreateError("Project not found");

            _logger.LogInformation("Syncing project {ProjectName}: {EventCount} events, {BlockCount} task blocks", 
                project.ProjectName, project.Events.Count, project.TaskBlocks.Count);

            // Get or create sheet for this project
            var sheetId = await GetOrCreateProjectSheetAsync(project, accessToken);
            if (sheetId == null)
                return SmartSheetSyncResult.CreateError("Failed to create/access SmartSheet");

            // Clear all existing rows from the sheet first
            await ClearAllSheetRowsAsync(sheetId.Value, accessToken);
            _logger.LogInformation("Cleared all existing rows from Smartsheet");

            // Build unified timeline matching exactly how Timeline displays items
            var timelineItems = await BuildUnifiedTimelineAsync(projectId);
            
            _logger.LogInformation("Built unified timeline: {ItemCount} items total", timelineItems.Count);
            
            // Log the hierarchy structure for debugging
            foreach (var item in timelineItems.Take(10)) // Log first 10 items
            {
                var indent = new string(' ', item.HierarchyLevel * 2);
                _logger.LogInformation("Timeline: {Indent}{Type} '{Id}' (Level {Level}, Parent: {ParentId})", 
                    indent, item.Type, item.Id, item.HierarchyLevel, item.ParentSmartsheetRowId?.ToString() ?? "none");
            }

            // Clear all SmartsheetRowId references but preserve logical parent relationships
            foreach (var item in timelineItems)
            {
                item.SmartsheetRowId = null;
                // Don't clear ParentSmartsheetRowId yet - we'll set it dynamically during batch processing
                
                if (item.Type == "TaskBlock" && item.Item is TaskBlock taskBlock)
                {
                    taskBlock.SmartsheetRowId = null;
                }
                else if (item.Type == "Event" && item.Item is ProjectEvent evt)
                {
                    evt.SmartsheetRowId = null;
                }
            }


            int created = 0;

            // Create all items in proper Timeline order using per-level batching
            if (timelineItems.Any())
            {
                var createdIds = await CreateTimelineRowsWithLevelBatching(sheetId.Value, timelineItems, accessToken);
                created = createdIds.Count(id => id != null);
                _logger.LogInformation("Created {CreatedCount} new Smartsheet rows using per-level batching with proper hierarchy", created);
            }

            // Update sheet summary with project data
            try
            {
                _logger.LogInformation("Updating sheet summary fields for project {ProjectId}", projectId);
                var summarySuccess = await UpdateSheetSummaryFromProjectAsync(projectId, sheetId.Value, accessToken);
                if (!summarySuccess)
                {
                    _logger.LogWarning("Summary field update failed for project {ProjectId}, but continuing with sync", projectId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Summary field update failed for project {ProjectId}, but continuing with sync", projectId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("SmartSheet unified sync completed for project {ProjectId}: {Created} created with proper hierarchy", 
                projectId, created);

            return SmartSheetSyncResult.CreateSuccess(created, 0, sheetId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing project {ProjectId} to SmartSheet", projectId);
            return SmartSheetSyncResult.CreateError($"Sync failed: {ex.Message}");
        }
    }

    public async Task<SmartSheetSyncResult> SyncFromSmartsheetAsync(string projectId, string accessToken)
    {
        try
        {
            // Attempt token refresh before sync operations
            var refreshedToken = await EnsureValidTokenAsync(accessToken);
            if (refreshedToken == null)
                return SmartSheetSyncResult.CreateError("Authentication failed. Please re-authenticate with Smartsheet.");
            
            accessToken = refreshedToken;
            var project = await _context.Projects
                .Include(p => p.Events)
                .Include(p => p.TaskBlocks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return SmartSheetSyncResult.CreateError("Project not found");

            if (!project.SmartsheetSheetId.HasValue)
                return SmartSheetSyncResult.CreateError("Project has no linked Smartsheet. Use 'To Smartsheet' first.");

            // Read rows from Smartsheet and update row numbers
            var updated = await UpdateRowNumbersFromSheetAsync(project, accessToken);
            
            if (updated >= 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Smartsheet inbound sync completed for project {ProjectId}: {Updated} updated", 
                    projectId, updated);
                return SmartSheetSyncResult.CreateSuccess(0, updated, project.SmartsheetSheetId.Value);
            }
            else
            {
                return SmartSheetSyncResult.CreateError("Failed to read from Smartsheet");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing from Smartsheet for project {ProjectId}", projectId);
            return SmartSheetSyncResult.CreateError($"Inbound sync failed: {ex.Message}");
        }
    }

    private async Task<long?> GetOrCreateProjectSheetAsync(Project project, string accessToken)
    {
        try
        {
            // Check if project already has a linked sheet
            if (project.SmartsheetSheetId.HasValue)
            {
                // Verify sheet still exists using SmartSheetService
                var sheetInfo = await _smartSheetService.GetSheetInfoAsync(project.SmartsheetSheetId.Value);
                if (sheetInfo != null)
                {
                    _logger.LogInformation("Using existing SmartSheet {SheetId} for project {ProjectId}", 
                        project.SmartsheetSheetId, project.Id);
                    return project.SmartsheetSheetId.Value;
                }
                else
                {
                    _logger.LogWarning("Linked sheet {SheetId} no longer accessible, creating new sheet", project.SmartsheetSheetId.Value);
                    project.SmartsheetSheetId = null; // Clear invalid link
                }
            }

            // Create new sheet and store ID
            var sheetId = await CreateProjectSheetAsync(project, accessToken);
            if (sheetId.HasValue)
            {
                project.SmartsheetSheetId = sheetId.Value;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created and linked new SmartSheet {SheetId} for project {ProjectId}", 
                    sheetId.Value, project.Id);
            }
            
            return sheetId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting/creating sheet for project {ProjectId}", project.Id);
            return null;
        }
    }

    private async Task<long?> CreateProjectSheetAsync(Project project, string accessToken)
    {
        try
        {
            var templateSheetId = long.Parse(_configuration["SmartSheet:TemplateSheetId"] 
                ?? throw new InvalidOperationException("SmartSheet:TemplateSheetId configuration is required"));
            var workspaceId = long.Parse(_configuration["SmartSheet:WorkspaceId"] 
                ?? throw new InvalidOperationException("SmartSheet:WorkspaceId configuration is required"));

            var includes = new List<SheetCopyInclusion> { SheetCopyInclusion.ATTACHMENTS, SheetCopyInclusion.DISCUSSIONS, SheetCopyInclusion.CELL_LINKS };
            var newId = await _smartSheetService.CopySheetToWorkspaceAsync(templateSheetId, workspaceId, project.ProjectName, includes);
            if (newId.HasValue)
            {
                _logger.LogInformation("Created SmartSheet for project {ProjectName} with ID {SheetId}", project.ProjectName, newId.Value);
            }
            return newId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SmartSheet for project {ProjectId}", project.Id);
            return null;
        }
    }

    private async Task<long?> CreateTaskBlockRowAsync(long sheetId, TaskBlock taskBlock, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
            if (row == null) return null;
            
            var ids = await _smartSheetService.AddRowsAsync(sheetId, new List<Row> { row });
            return ids.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SmartSheet TaskBlock row for block {BlockId}", taskBlock.Id);
            return null;
        }
    }

    private async Task<bool> UpdateTaskBlockRowAsync(long sheetId, TaskBlock taskBlock, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
            if (row == null) return false;
            
            row.Id = taskBlock.SmartsheetRowId!.Value; // Set the row ID for update

            var updateRowsRequest = new[] { new { id = row.Id, cells = row.Cells } };

            var json = JsonSerializer.Serialize(updateRowsRequest, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update SmartSheet TaskBlock row: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SmartSheet TaskBlock row for block {BlockId}", taskBlock.Id);
            return false;
        }
    }

    private async Task<long?> CreateRowAsync(long sheetId, ProjectEvent eventItem, long? parentRowId, string accessToken)
    {
        try
        {
            var row = await TransformEventToRowAsync(eventItem, sheetId, accessToken);
            if (row == null) return null;
            if (parentRowId.HasValue) row.ParentId = parentRowId.Value;
            var ids = await _smartSheetService.AddRowsAsync(sheetId, new List<Row> { row });
            return ids.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SmartSheet row for event {EventId}", eventItem.Id);
            return null;
        }
    }

    private async Task<bool> UpdateRowAsync(long sheetId, ProjectEvent eventItem, string accessToken)
    {
        try
        {
            var row = await TransformEventToRowAsync(eventItem, sheetId, accessToken);
            if (row == null) return false;
            row.Id = eventItem.SmartsheetRowId!.Value;
            var count = await _smartSheetService.UpdateRowsAsync(sheetId, new List<Row> { row });
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SmartSheet row for event {EventId}", eventItem.Id);
            return false;
        }
    }

    private async Task<Row?> TransformEventToRowAsync(ProjectEvent eventItem, long sheetId, string accessToken)
    {
        var columnMapping = await GetColumnMappingAsync(sheetId, accessToken);
        if (columnMapping == null) return null;

        var cells = new List<Cell>();

        // Map columns by title
        if (columnMapping.TryGetValue("Task Name", out var taskNameCol))
            cells.Add(new Cell { ColumnId = taskNameCol, Value = GetTaskName(eventItem) });

        if (columnMapping.TryGetValue("Start Date", out var startDateCol))
            cells.Add(new Cell { ColumnId = startDateCol, Value = eventItem.EventDate.ToString("yyyy-MM-dd") });

        if (columnMapping.TryGetValue("Assigned To", out var assignedToCol))
            cells.Add(new Cell { ColumnId = assignedToCol, Value = eventItem.CreatedBy ?? "System" });

        if (columnMapping.TryGetValue("Notes", out var notesCol) || columnMapping.TryGetValue("Notes ", out notesCol))
            cells.Add(new Cell { ColumnId = notesCol, Value = GetEventNotes(eventItem) });

        if (columnMapping.TryGetValue("ShopBoss Type", out var typeCol))
            cells.Add(new Cell { ColumnId = typeCol, Value = eventItem.EventType });
        return new Row { Cells = cells, ToBottom = true };
    }

    private async Task<Row?> TransformTaskBlockToRowAsync(TaskBlock taskBlock, long sheetId, string accessToken)
    {
        var columnMapping = await GetColumnMappingAsync(sheetId, accessToken);
        if (columnMapping == null) return null;

        var cells = new List<Cell>();

        if (columnMapping.TryGetValue("Task Name", out var taskNameCol))
            cells.Add(new Cell { ColumnId = taskNameCol, Value = taskBlock.Name });

        if (columnMapping.TryGetValue("ShopBoss Type", out var typeCol))
            cells.Add(new Cell { ColumnId = typeCol, Value = "TaskBlock" });

        // Mark TaskBlocks (parent rows) to be omitted from reports
        if (columnMapping.TryGetValue("Omit From Reports", out var omitCol))
            cells.Add(new Cell { ColumnId = omitCol, Value = true });
            
        return new Row { Cells = cells, ToBottom = true };
    }


    private string GetTaskName(ProjectEvent eventItem)
    {
        return eventItem.EventType switch
        {
            "comment" => $"Comment: {eventItem.Description}",
            "attachment" => eventItem.Attachment?.FileName ?? $"File Upload: {eventItem.Description}",
            "purchase_order" => $"Purchase Order: {eventItem.Description}",
            "custom_work_order" => $"Custom Work Order: {eventItem.Description}",
            _ => eventItem.Description
        };
    }

    private string GetEventNotes(ProjectEvent eventItem)
    {
        var notes = new List<string>();
        
        if (!string.IsNullOrEmpty(eventItem.Description))
            notes.Add($"Description: {eventItem.Description}");
            
        if (eventItem.AttachmentId != null)
            notes.Add($"Attachment ID: {eventItem.AttachmentId}");
            
        if (eventItem.PurchaseOrderId != null)
            notes.Add($"Purchase Order ID: {eventItem.PurchaseOrderId}");
            
        if (eventItem.CustomWorkOrderId != null)
            notes.Add($"Custom Work Order ID: {eventItem.CustomWorkOrderId}");

        return string.Join(" | ", notes);
    }

    private async Task<bool> ClearAllSheetRowsAsync(long sheetId, string accessToken)
    {
        try
        {
            var rowIds = await _smartSheetService.GetAllRowIdsAsync(sheetId);

            if (!rowIds.Any())
            {
                _logger.LogInformation("No rows to clear from sheet {SheetId}", sheetId);
                return true;
            }

            _logger.LogInformation("Found {RowCount} rows to clear from sheet {SheetId}", rowIds.Count, sheetId);

            // Delete rows in batches (Smartsheet allows up to 450 row deletes per request)
            const int BATCH_SIZE = 400;
            for (int i = 0; i < rowIds.Count; i += BATCH_SIZE)
            {
                var batch = rowIds.Skip(i).Take(BATCH_SIZE);
                var ok = await _smartSheetService.DeleteRowsAsync(sheetId, batch.ToList());
                if (!ok) return false;
            }

            _logger.LogInformation("Successfully cleared {RowCount} rows from sheet {SheetId}", rowIds.Count, sheetId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing sheet rows");
            return false;
        }
    }

    private async Task<List<TimelineItem>> BuildUnifiedTimelineAsync(string projectId)
    {
        var timelineItems = new List<TimelineItem>();
        
        // Get the timeline data using the same logic as TimelineService
        var project = await _context.Projects
            .Include(p => p.Events)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) return timelineItems;

        // Get root-level task blocks with full hierarchy
        var rootBlocks = await _context.TaskBlocks
            .Where(tb => tb.ProjectId == projectId && tb.ParentTaskBlockId == null)
            .Include(tb => tb.Events)
                .ThenInclude(e => e.Attachment)
            .Include(tb => tb.Events)
                .ThenInclude(e => e.WorkOrder!)
                    .ThenInclude(wo => wo.NestSheets)
            .Include(tb => tb.ChildTaskBlocks)
                .ThenInclude(child => child.Events)
                    .ThenInclude(e => e.Attachment)
            .OrderBy(tb => tb.GlobalDisplayOrder ?? tb.DisplayOrder)
            .AsSplitQuery()
            .ToListAsync();

        // Load all children recursively
        await LoadAllChildrenRecursivelyForSync(rootBlocks);

        // Get all events for this project
        var allEvents = await _context.ProjectEvents
            .Where(pe => pe.ProjectId == projectId)
            .Include(pe => pe.Attachment)
            .Include(pe => pe.WorkOrder!)
                .ThenInclude(wo => wo.NestSheets)
            .OrderBy(pe => pe.GlobalDisplayOrder ?? int.MaxValue)
            .ThenBy(pe => pe.EventDate)
            .AsSplitQuery()
            .ToListAsync();

        // Build the timeline items recursively, preserving the exact Timeline display order
        int orderCounter = 0;
        orderCounter = await BuildTimelineItemsRecursively(rootBlocks, allEvents, timelineItems, 0, null, orderCounter);

        // Add unblocked events at root level
        var blockedEventIds = GetBlockedEventIds(rootBlocks);
        var unblockedEvents = allEvents.Where(e => e.Id != null && !blockedEventIds.Contains(e.Id))
            .OrderBy(e => e.GlobalDisplayOrder ?? int.MaxValue)
            .ThenBy(e => e.EventDate);

        foreach (var evt in unblockedEvents)
        {
            timelineItems.Add(new TimelineItem
            {
                Id = evt.Id,
                Type = "Event",
                Item = evt,
                DisplayOrder = orderCounter++,
                HierarchyLevel = 0,
                ParentItemIndex = null, // Root level events have no parent
                ParentSmartsheetRowId = null,
                SmartsheetRowId = null // Clear old IDs
            });
        }

        return timelineItems.OrderBy(ti => ti.DisplayOrder).ToList();
    }

    private async Task LoadAllChildrenRecursivelyForSync(List<TaskBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block.ChildTaskBlocks.Any())
            {
                // Load children of children
                var childIds = block.ChildTaskBlocks.Select(c => c.Id).ToList();
                var grandChildren = await _context.TaskBlocks
                    .Where(tb => tb.ParentTaskBlockId != null && childIds.Contains(tb.ParentTaskBlockId))
                    .Include(tb => tb.Events)
                        .ThenInclude(e => e.Attachment)
                    .Include(tb => tb.Events)
                        .ThenInclude(e => e.WorkOrder!)
                            .ThenInclude(wo => wo.NestSheets)
                    .Include(tb => tb.ChildTaskBlocks)
                    .ToListAsync();

                foreach (var child in block.ChildTaskBlocks)
                {
                    child.ChildTaskBlocks = grandChildren.Where(gc => gc.ParentTaskBlockId == child.Id).ToList();
                }

                await LoadAllChildrenRecursivelyForSync(block.ChildTaskBlocks.ToList());
            }
        }
    }

    private async Task<int> BuildTimelineItemsRecursively(
        List<TaskBlock> taskBlocks, 
        List<ProjectEvent> allEvents, 
        List<TimelineItem> timelineItems, 
        int hierarchyLevel, 
        int? parentItemIndex,
        int orderCounter)
    {
        // Create a mixed list of TaskBlocks and Events with their display orders
        var mixedItems = new List<(object item, int order, string type)>();

        // Add TaskBlocks
        foreach (var block in taskBlocks)
        {
            mixedItems.Add((block, block.GlobalDisplayOrder ?? block.DisplayOrder, "TaskBlock"));
        }

        // Add Events that belong to the current level (unblocked at this level)
        var currentLevelEvents = allEvents.Where(e => 
            string.IsNullOrEmpty(e.TaskBlockId) && hierarchyLevel == 0 || // Root level events
            taskBlocks.Any(tb => tb.Id == e.TaskBlockId) // Events in current TaskBlocks
        );

        foreach (var evt in currentLevelEvents)
        {
            if (!string.IsNullOrEmpty(evt.TaskBlockId))
            {
                // This event belongs to a TaskBlock - it will be handled when we process that TaskBlock
                continue;
            }
            mixedItems.Add((evt, evt.GlobalDisplayOrder ?? int.MaxValue, "Event"));
        }

        // Sort mixed items by display order
        var sortedMixedItems = mixedItems.OrderBy(item => item.order).ToList();

        foreach (var (item, order, type) in sortedMixedItems)
        {
            if (type == "TaskBlock")
            {
                var taskBlock = (TaskBlock)item;
                
                // Add the TaskBlock and remember its index
                var taskBlockIndex = timelineItems.Count; // This will be the index of this TaskBlock
                timelineItems.Add(new TimelineItem
                {
                    Id = taskBlock.Id,
                    Type = "TaskBlock",
                    Item = taskBlock,
                    DisplayOrder = orderCounter++,
                    HierarchyLevel = hierarchyLevel,
                    ParentItemIndex = parentItemIndex,
                    ParentSmartsheetRowId = null, // Will be resolved during batch processing
                    SmartsheetRowId = null // Clear old IDs
                });

                // Add events within this TaskBlock
                var blockEvents = allEvents.Where(e => e.TaskBlockId == taskBlock.Id)
                    .OrderBy(e => e.BlockDisplayOrder ?? 0)
                    .ThenBy(e => e.EventDate)
                    .ToList();

                foreach (var evt in blockEvents)
                {
                    timelineItems.Add(new TimelineItem
                    {
                        Id = evt.Id,
                        Type = "Event",
                        Item = evt,
                        DisplayOrder = orderCounter++,
                        HierarchyLevel = hierarchyLevel + 1,
                        ParentItemIndex = taskBlockIndex, // Parent is the TaskBlock we just added
                        ParentSmartsheetRowId = null, // Will be resolved during batch processing
                        SmartsheetRowId = null // Clear old IDs
                    });
                }

                // Recursively add child TaskBlocks
                if (taskBlock.ChildTaskBlocks.Any())
                {
                    orderCounter = await BuildTimelineItemsRecursively(
                        taskBlock.ChildTaskBlocks.OrderBy(cb => cb.DisplayOrder).ToList(),
                        allEvents,
                        timelineItems,
                        hierarchyLevel + 1,
                        taskBlockIndex, // Pass the parent TaskBlock's index
                        orderCounter
                    );
                }
            }
            else if (type == "Event" && hierarchyLevel == 0) // Only add root-level events here
            {
                var evt = (ProjectEvent)item;
                timelineItems.Add(new TimelineItem
                {
                    Id = evt.Id,
                    Type = "Event",
                    Item = evt,
                    DisplayOrder = orderCounter++,
                    HierarchyLevel = hierarchyLevel,
                    ParentItemIndex = null, // Root level events have no parent
                    ParentSmartsheetRowId = null,
                    SmartsheetRowId = null // Clear old IDs
                });
            }
        }

        return orderCounter;
    }

    private HashSet<string> GetBlockedEventIds(List<TaskBlock> taskBlocks)
    {
        var blockedIds = new HashSet<string>();
        
        foreach (var block in taskBlocks)
        {
            foreach (var evt in block.Events)
            {
                blockedIds.Add(evt.Id);
            }
            
            if (block.ChildTaskBlocks.Any())
            {
                var childBlockedIds = GetBlockedEventIds(block.ChildTaskBlocks.ToList());
                blockedIds.UnionWith(childBlockedIds);
            }
        }
        
        return blockedIds;
    }

    private async Task<List<long?>> CreateTimelineRowsSequentially(long sheetId, List<TimelineItem> timelineItems, string accessToken)
    {
        var results = new List<long?>();
        
        _logger.LogInformation("Creating {ItemCount} timeline rows sequentially", timelineItems.Count);

        foreach (var item in timelineItems)
        {
            try
            {
                // Transform the item to a Smartsheet row
                Row? row = null;
                
                if (item.Type == "TaskBlock" && item.Item is TaskBlock taskBlock)
                {
                    row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
                }
                else if (item.Type == "Event" && item.Item is ProjectEvent evt)
                {
                    row = await TransformEventToRowAsync(evt, sheetId, accessToken);
                }

                if (row == null)
                {
                    _logger.LogWarning("Failed to transform {ItemType} '{ItemId}' to Smartsheet row", item.Type, item.Id);
                    results.Add(null);
                    continue;
                }

                // Add parentId if this item has a parent
                if (item.ParentItemIndex.HasValue)
                {
                    var parentItem = timelineItems[item.ParentItemIndex.Value];
                    if (parentItem.SmartsheetRowId.HasValue)
                    {
                        row.ParentId = parentItem.SmartsheetRowId.Value;
                        _logger.LogInformation("Creating {ItemType} '{ItemId}' with parentId {ParentId}", 
                            item.Type, item.Id, parentItem.SmartsheetRowId.Value);
                    }
                    else
                    {
                        _logger.LogError("Parent at index {ParentIndex} doesn't have SmartsheetRowId - this should not happen!", 
                            item.ParentItemIndex.Value);
                    }
                }

                var ids = await _smartSheetService.AddRowsAsync(sheetId, new List<Row> { row });
                if (ids.Count > 0)
                {
                    var rowId = ids[0];
                    item.SmartsheetRowId = rowId;
                    if (item.Type == "TaskBlock" && item.Item is TaskBlock tb) tb.SmartsheetRowId = rowId;
                    else if (item.Type == "Event" && item.Item is ProjectEvent pe) pe.SmartsheetRowId = rowId;
                    results.Add(rowId);
                    _logger.LogDebug("Created {ItemType} '{ItemId}' with SmartsheetRowId {RowId}", item.Type, item.Id, rowId);
                }
                else
                {
                    results.Add(null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Smartsheet row for {ItemType} '{ItemId}'", item.Type, item.Id);
                results.Add(null);
            }
        }

        _logger.LogInformation("Sequential creation completed: {SuccessCount} of {TotalCount} rows created", 
            results.Count(r => r != null), results.Count);

        return results;
    }












    private async Task<List<long?>> CreateTimelineRowsWithLevelBatching(long sheetId, List<TimelineItem> timelineItems, string accessToken)
    {
        var results = new List<long?>(new long?[timelineItems.Count]);
        
        _logger.LogInformation("Creating {ItemCount} timeline rows using per-level batching", timelineItems.Count);

        // Group items by hierarchy level
        var itemsByLevel = timelineItems
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.item.HierarchyLevel)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var levelGroup in itemsByLevel)
        {
            var level = levelGroup.Key;
            var itemsAtLevel = levelGroup.ToList();
            
            _logger.LogInformation("Processing level {Level}: {ItemCount} items", level, itemsAtLevel.Count);

            // Group items at this level by their parent (items with same parent can be batched together)
            var batchesByParent = itemsAtLevel
                .GroupBy(x => x.item.ParentItemIndex ?? -1) // -1 for root items (no parent)
                .ToList();

            foreach (var parentBatch in batchesByParent)
            {
                var parentIndex = parentBatch.Key == -1 ? (int?)null : parentBatch.Key;
                var itemsInBatch = parentBatch.Select(x => (x.item, x.index)).ToList();
                
                _logger.LogInformation("Processing batch at level {Level} with parent index {ParentIndex}: {BatchSize} items", 
                    level, parentIndex?.ToString() ?? "none", itemsInBatch.Count);

                // Get the parent's SmartsheetRowId if this batch has a parent
                long? parentRowId = null;
                if (parentIndex.HasValue && parentIndex.Value < timelineItems.Count)
                {
                    var parentItem = timelineItems[parentIndex.Value];
                    parentRowId = parentItem.SmartsheetRowId;
                    
                    if (!parentRowId.HasValue)
                    {
                        _logger.LogError("Parent at index {ParentIndex} doesn't have SmartsheetRowId - this should not happen in level-based processing!", parentIndex.Value);
                        // Fall back to individual creation for this batch
                        await CreateBatchItemsIndividually(sheetId, itemsInBatch, timelineItems, results, accessToken);
                        continue;
                    }
                }

                // Create batch request for all items with the same parent
                await CreateBatchWithSameParent(sheetId, itemsInBatch, parentRowId, timelineItems, results, accessToken);
            }
        }

        _logger.LogInformation("Per-level batch creation completed: {SuccessCount} of {TotalCount} rows created", 
            results.Count(r => r != null), results.Count);

        return results.ToList();
    }

    private async Task CreateBatchWithSameParent(
        long sheetId, 
        List<(TimelineItem item, int index)> itemsInBatch, 
        long? parentRowId, 
        List<TimelineItem> allTimelineItems, 
        List<long?> results, 
        string accessToken)
    {
        try
        {
            var sdkRows = new List<Row>();
            foreach (var (item, index) in itemsInBatch)
            {
                Row? row = null;
                if (item.Type == "TaskBlock" && item.Item is TaskBlock taskBlock)
                    row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
                else if (item.Type == "Event" && item.Item is ProjectEvent evt)
                    row = await TransformEventToRowAsync(evt, sheetId, accessToken);

                if (row != null)
                {
                    if (parentRowId.HasValue) row.ParentId = parentRowId.Value;
                    sdkRows.Add(row);
                }
                else
                {
                    _logger.LogWarning("Failed to transform timeline item {ItemType} '{ItemId}' to Smartsheet row", item.Type, item.Id);
                }
            }

            if (!sdkRows.Any())
            {
                _logger.LogWarning("No valid rows to create in batch");
                return;
            }

            var createdIds = await _smartSheetService.AddRowsAsync(sheetId, sdkRows);
            if (createdIds.Count == itemsInBatch.Count)
            {
                for (int i = 0; i < itemsInBatch.Count; i++)
                {
                    var (item, index) = itemsInBatch[i];
                    var rowId = createdIds[i];
                    item.SmartsheetRowId = rowId;
                    results[index] = rowId;
                    if (item.Type == "TaskBlock" && item.Item is TaskBlock tb) tb.SmartsheetRowId = rowId;
                    else if (item.Type == "Event" && item.Item is ProjectEvent pe) pe.SmartsheetRowId = rowId;
                    _logger.LogDebug("Batch created {ItemType} '{ItemId}' with SmartsheetRowId {RowId}", item.Type, item.Id, rowId);
                }
                _logger.LogInformation("Successfully created batch of {ItemCount} rows", itemsInBatch.Count);
            }
            else
            {
                _logger.LogError("Batch response returned {ReturnedCount} row IDs but expected {ExpectedCount}", createdIds.Count, itemsInBatch.Count);
                foreach (var (item, index) in itemsInBatch) { results[index] = null; }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating batch with same parent");
            foreach (var (item, index) in itemsInBatch) { results[index] = null; }
        }
    }

    private async Task CreateBatchItemsIndividually(
        long sheetId,
        List<(TimelineItem item, int index)> itemsInBatch,
        List<TimelineItem> allTimelineItems,
        List<long?> results,
        string accessToken)
    {
        _logger.LogWarning("Falling back to individual creation for {ItemCount} items", itemsInBatch.Count);
        
        foreach (var (item, index) in itemsInBatch)
        {
            try
            {
                Row? row = null;
                if (item.Type == "TaskBlock" && item.Item is TaskBlock taskBlock)
                    row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
                else if (item.Type == "Event" && item.Item is ProjectEvent evt)
                    row = await TransformEventToRowAsync(evt, sheetId, accessToken);

                if (row == null) { results[index] = null; continue; }

                if (item.ParentItemIndex.HasValue)
                {
                    var parentItem = allTimelineItems[item.ParentItemIndex.Value];
                    if (parentItem.SmartsheetRowId.HasValue)
                        row.ParentId = parentItem.SmartsheetRowId.Value;
                }

                var ids = await _smartSheetService.AddRowsAsync(sheetId, new List<Row> { row });
                if (ids.Count > 0)
                {
                    var rowId = ids[0];
                    item.SmartsheetRowId = rowId;
                    results[index] = rowId;
                    if (item.Type == "TaskBlock" && item.Item is TaskBlock tb) tb.SmartsheetRowId = rowId;
                    else if (item.Type == "Event" && item.Item is ProjectEvent pe) pe.SmartsheetRowId = rowId;
                }
                else
                {
                    results[index] = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating individual Smartsheet row for {ItemType} '{ItemId}'", item.Type, item.Id);
                results[index] = null;
            }
        }
    }

    private async Task<Dictionary<string, long>?> GetColumnMappingAsync(long sheetId, string accessToken)
    {
        try
        {
            // Check cache first
            if (_columnCache.TryGetValue(sheetId, out var cachedMapping))
            {
                return cachedMapping;
            }

            var mapping = await _smartSheetService.GetColumnMapAsync(sheetId);
            if (mapping.Count == 0)
            {
                _logger.LogWarning("No columns returned from sheet {SheetId}", sheetId);
                return null;
            }
            _columnCache[sheetId] = mapping;
            _logger.LogInformation("Cached column mapping for sheet {SheetId}: {Count} columns", sheetId, mapping.Count);
            return mapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting column mapping for sheet {SheetId}", sheetId);
            return null;
        }
    }

    private void RefreshColumnCache(long sheetId)
    {
        _columnCache.Remove(sheetId);
        _logger.LogInformation("Refreshed column cache for sheet {SheetId}", sheetId);
    }

    private async Task<int> UpdateRowNumbersFromSheetAsync(Project project, string accessToken)
    {
        try
        {
            if (!project.SmartsheetSheetId.HasValue) return 0;
            var rowIdToRowNumber = await _smartSheetService.GetRowIdToRowNumberMapAsync(project.SmartsheetSheetId.Value);

            // Update RowNumber for all events and TaskBlocks with SmartsheetRowId
            int updated = 0;
            
            foreach (var eventItem in project.Events.Where(e => e.SmartsheetRowId.HasValue))
            {
                if (eventItem.SmartsheetRowId.HasValue && 
                    rowIdToRowNumber.TryGetValue(eventItem.SmartsheetRowId.Value, out var rowNumber))
                {
                    if (eventItem.RowNumber != rowNumber)
                    {
                        eventItem.RowNumber = rowNumber;
                        updated++;
                    }
                }
            }

            foreach (var taskBlock in project.TaskBlocks.Where(b => b.SmartsheetRowId.HasValue))
            {
                // TaskBlocks don't have RowNumber field, but we could add one if needed
                // For now, just log that we found it
                if (taskBlock.SmartsheetRowId.HasValue &&
                    rowIdToRowNumber.TryGetValue(taskBlock.SmartsheetRowId.Value, out var rowNumber))
                {
                    _logger.LogDebug("TaskBlock {BlockName} is at row {RowNumber}", taskBlock.Name, rowNumber);
                }
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading row numbers from Smartsheet");
            return -1;
        }
    }

    private Task<string?> EnsureValidTokenAsync(string accessToken)
    {
        try
        {
            // Check if we have a SmartSheet session with valid token
            if (!_smartSheetService.HasSmartSheetSession())
            {
                _logger.LogWarning("No SmartSheet session available for sync");
                return Task.FromResult<string?>(null);
            }

            // SmartSheetService handles token refresh automatically in its async client method
            // For now, we'll use the existing token and let individual API calls handle refresh
            _logger.LogInformation("Token refresh check completed - using SmartSheetService session");
            return Task.FromResult<string?>(accessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring valid token for sync");
            return Task.FromResult<string?>(null);
        }
    }

    private async Task<bool> UpdateSheetSummaryFromProjectAsync(string projectId, long sheetId, string accessToken)
    {
        try
        {
            // Get the project with all necessary data
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                _logger.LogWarning("Project {ProjectId} not found for summary sync", projectId);
                return false;
            }

            // Map project fields to summary field updates (only fields that exist in Smartsheet)
            var fieldUpdates = new Dictionary<string, object?>
            {
                ["Project ID"] = project.ProjectId,
                ["Project Name"] = project.ProjectName,
                ["Job Address"] = project.ProjectAddress,
                ["GC"] = project.GeneralContractor,
                ["Job Contact"] = project.ProjectContact,
                ["Job Contact Phone"] = project.ProjectContactPhone,
                ["Job Contact Email"] = project.ProjectContactEmail,
                ["Project Manager"] = project.ProjectManager,
                ["Installer"] = project.Installer
            };

            _logger.LogInformation("Updating {FieldCount} summary fields for project {ProjectId} on sheet {SheetId}", 
                fieldUpdates.Count, projectId, sheetId);

            // Use SmartSheetService to update the summary fields
            var success = await _smartSheetService.UpdateSheetSummaryFieldsAsync(sheetId, fieldUpdates);
            
            if (success)
            {
                _logger.LogInformation("Successfully updated sheet summary for project {ProjectId}", projectId);
            }
            else
            {
                _logger.LogWarning("Failed to update sheet summary for project {ProjectId}", projectId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sheet summary for project {ProjectId}", projectId);
            return false;
        }
    }

}

// Response models for reading sheet
public class SmartSheetReadResponse
{
    public List<SmartSheetReadRow>? Rows { get; set; }
}

public class SmartSheetReadRow
{
    public long Id { get; set; }
    public int RowNumber { get; set; }
}

// Response models for column mapping
public class SmartSheetColumnResponse
{
    public List<SmartSheetColumn>? Columns { get; set; }
}

public class SmartSheetColumn
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Type { get; set; }
    public string[]? Options { get; set; }
    public bool Locked { get; set; }
}

// Response models for SmartSheet API
public class SmartSheetSyncResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public long? SheetId { get; set; }

    public static SmartSheetSyncResult CreateSuccess(int created, int updated, long sheetId) =>
        new() { Success = true, Created = created, Updated = updated, SheetId = sheetId };

    public static SmartSheetSyncResult CreateError(string message) =>
        new() { Success = false, Message = message };
}

public class TimelineItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "TaskBlock" or "Event"
    public object Item { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public int HierarchyLevel { get; set; }
    public long? ParentSmartsheetRowId { get; set; }
    public long? SmartsheetRowId { get; set; }
    public int? ParentItemIndex { get; set; } // Index to parent item in the timelineItems list
    public bool IsNew => SmartsheetRowId == null;
}
