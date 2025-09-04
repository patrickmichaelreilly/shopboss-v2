using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

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

            // Build unified timeline matching exactly how Timeline displays items
            var timelineItems = await BuildUnifiedTimelineAsync(projectId);
            
            _logger.LogInformation("Built unified timeline: {ItemCount} items total", timelineItems.Count);
            
            // Separate new and existing items
            var newItems = timelineItems.Where(ti => ti.IsNew).ToList();
            var existingItems = timelineItems.Where(ti => !ti.IsNew).ToList();

            _logger.LogInformation("Timeline items to sync: {NewItems} new, {ExistingItems} existing", 
                newItems.Count, existingItems.Count);

            int created = 0, updated = 0;

            // Create new items in proper Timeline order
            if (newItems.Any())
            {
                var createdIds = await CreateUnifiedTimelineRowsBatchAsync(sheetId.Value, newItems, accessToken);
                created = createdIds.Count(id => id != null);
                _logger.LogInformation("Created {CreatedCount} new Smartsheet rows", created);
            }

            // Update existing items while maintaining hierarchy
            if (existingItems.Any())
            {
                var updatedCount = await UpdateUnifiedTimelineRowsBatchAsync(sheetId.Value, existingItems, accessToken);
                updated = updatedCount;
                _logger.LogInformation("Updated {UpdatedCount} existing Smartsheet rows", updated);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("SmartSheet unified sync completed for project {ProjectId}: {Created} created, {Updated} updated", 
                projectId, created, updated);

            return SmartSheetSyncResult.CreateSuccess(created, updated, sheetId.Value);
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
            var templateSheetId = _configuration["SmartSheet:TemplateSheetId"] 
                ?? throw new InvalidOperationException("SmartSheet:TemplateSheetId configuration is required");
            var workspaceId = _configuration["SmartSheet:WorkspaceId"] 
                ?? throw new InvalidOperationException("SmartSheet:WorkspaceId configuration is required");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // First, verify we can access the workspace
            var workspaceResponse = await _httpClient.GetAsync($"https://api.smartsheet.com/2.0/workspaces/{workspaceId}");
            if (!workspaceResponse.IsSuccessStatusCode)
            {
                var workspaceError = await workspaceResponse.Content.ReadAsStringAsync();
                _logger.LogError("Cannot access workspace {WorkspaceId}: {StatusCode} - {Content}", 
                    workspaceId, workspaceResponse.StatusCode, workspaceError);
                return null;
            }

            // Verify we can access the template sheet
            var templateResponse = await _httpClient.GetAsync($"https://api.smartsheet.com/2.0/sheets/{templateSheetId}");
            if (!templateResponse.IsSuccessStatusCode)
            {
                var templateError = await templateResponse.Content.ReadAsStringAsync();
                _logger.LogError("Cannot access template sheet {TemplateSheetId}: {StatusCode} - {Content}", 
                    templateSheetId, templateResponse.StatusCode, templateError);
                return null;
            }

            // Now try to create the sheet copy
            var createRequest = new
            {
                name = project.ProjectName,
                fromId = long.Parse(templateSheetId)
            };

            var json = JsonSerializer.Serialize(createRequest, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"https://api.smartsheet.com/2.0/workspaces/{workspaceId}/sheets", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<SmartSheetCreateResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                var sheetId = result?.Result?.Id;
                _logger.LogInformation("Created SmartSheet for project {ProjectName} with ID {SheetId}", 
                    project.ProjectName, sheetId);
                return sheetId;
            }
            else
            {
                _logger.LogError("Failed to create SmartSheet: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return null;
            }
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
            
            var addRowsRequest = new[] { new { toBottom = true, cells = row.Cells } };

            var json = JsonSerializer.Serialize(addRowsRequest, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<SmartSheetRowResponse>(responseContent);
                return result?.Result?.FirstOrDefault()?.Id;
            }
            else
            {
                _logger.LogError("Failed to create SmartSheet TaskBlock row: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return null;
            }
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
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var row = await TransformEventToRowAsync(eventItem, sheetId, accessToken);
            if (row == null) return null;
            
            if (parentRowId.HasValue)
            {
                row.ParentId = parentRowId.Value;
            }
            var addRowsRequest = new[] { new { toBottom = true, cells = row.Cells } };

            var json = JsonSerializer.Serialize(addRowsRequest, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<SmartSheetRowResponse>(responseContent);
                return result?.Result?.FirstOrDefault()?.Id;
            }
            else
            {
                _logger.LogError("Failed to create SmartSheet row: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return null;
            }
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
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var row = await TransformEventToRowAsync(eventItem, sheetId, accessToken);
            if (row == null) return false;
            
            row.Id = eventItem.SmartsheetRowId!.Value; // Set the row ID for update

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
                _logger.LogError("Failed to update SmartSheet row: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SmartSheet row for event {EventId}", eventItem.Id);
            return false;
        }
    }

    private async Task<SmartSheetRow?> TransformEventToRowAsync(ProjectEvent eventItem, long sheetId, string accessToken)
    {
        var columnMapping = await GetColumnMappingAsync(sheetId, accessToken);
        if (columnMapping == null) return null;

        var cells = new List<SmartSheetCell>();

        // Map columns by title
        if (columnMapping.TryGetValue("Task Name", out var taskNameCol))
            cells.Add(new SmartSheetCell { ColumnId = taskNameCol, Value = GetTaskName(eventItem) });

        if (columnMapping.TryGetValue("Status", out var statusCol))
            cells.Add(new SmartSheetCell { ColumnId = statusCol, Value = "Open" });

        if (columnMapping.TryGetValue("Start Date", out var startDateCol))
            cells.Add(new SmartSheetCell { ColumnId = startDateCol, Value = eventItem.EventDate.ToString("yyyy-MM-dd") });

        if (columnMapping.TryGetValue("Assigned To", out var assignedToCol))
            cells.Add(new SmartSheetCell { ColumnId = assignedToCol, Value = eventItem.CreatedBy ?? "System" });

        if (columnMapping.TryGetValue("Notes", out var notesCol) || columnMapping.TryGetValue("Notes ", out notesCol))
            cells.Add(new SmartSheetCell { ColumnId = notesCol, Value = GetEventNotes(eventItem) });

        if (columnMapping.TryGetValue("ShopBoss Type", out var typeCol))
            cells.Add(new SmartSheetCell { ColumnId = typeCol, Value = eventItem.EventType });

        return new SmartSheetRow { Cells = cells };
    }

    private async Task<SmartSheetRow?> TransformTaskBlockToRowAsync(TaskBlock taskBlock, long sheetId, string accessToken)
    {
        var columnMapping = await GetColumnMappingAsync(sheetId, accessToken);
        if (columnMapping == null) return null;

        var cells = new List<SmartSheetCell>();

        if (columnMapping.TryGetValue("Task Name", out var taskNameCol))
            cells.Add(new SmartSheetCell { ColumnId = taskNameCol, Value = taskBlock.Name });

        if (columnMapping.TryGetValue("Status", out var statusCol))
            cells.Add(new SmartSheetCell { ColumnId = statusCol, Value = "Open" });

        if (columnMapping.TryGetValue("ShopBoss Type", out var typeCol))
            cells.Add(new SmartSheetCell { ColumnId = typeCol, Value = "TaskBlock" });

        return new SmartSheetRow { Cells = cells };
    }

    private long? GetParentRowIdForEvent(ProjectEvent eventItem, ICollection<TaskBlock> taskBlocks)
    {
        if (string.IsNullOrEmpty(eventItem.TaskBlockId))
            return null;

        var parentBlock = taskBlocks.FirstOrDefault(b => b.Id == eventItem.TaskBlockId);
        return parentBlock?.SmartsheetRowId;
    }

    private string GetTaskName(ProjectEvent eventItem)
    {
        return eventItem.EventType switch
        {
            "comment" => $"Comment: {eventItem.Description}",
            "attachment" => $"File Upload: {eventItem.Description}",
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
                .ThenInclude(e => e.WorkOrder)
                    .ThenInclude(wo => wo.NestSheets)
            .Include(tb => tb.ChildTaskBlocks)
                .ThenInclude(child => child.Events)
                    .ThenInclude(e => e.Attachment)
            .OrderBy(tb => tb.GlobalDisplayOrder ?? tb.DisplayOrder)
            .ToListAsync();

        // Load all children recursively
        await LoadAllChildrenRecursivelyForSync(rootBlocks);

        // Get all events for this project
        var allEvents = await _context.ProjectEvents
            .Where(pe => pe.ProjectId == projectId)
            .Include(pe => pe.Attachment)
            .Include(pe => pe.WorkOrder)
                .ThenInclude(wo => wo.NestSheets)
            .OrderBy(pe => pe.GlobalDisplayOrder ?? int.MaxValue)
            .ThenBy(pe => pe.EventDate)
            .ToListAsync();

        // Build the timeline items recursively, preserving the exact Timeline display order
        int orderCounter = 0;
        orderCounter = await BuildTimelineItemsRecursively(rootBlocks, allEvents, timelineItems, 0, null, orderCounter);

        // Add unblocked events at root level
        var blockedEventIds = GetBlockedEventIds(rootBlocks);
        var unblockedEvents = allEvents.Where(e => !blockedEventIds.Contains(e.Id))
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
                ParentSmartsheetRowId = null,
                SmartsheetRowId = evt.SmartsheetRowId
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
                    .Where(tb => childIds.Contains(tb.ParentTaskBlockId))
                    .Include(tb => tb.Events)
                        .ThenInclude(e => e.Attachment)
                    .Include(tb => tb.Events)
                        .ThenInclude(e => e.WorkOrder)
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
        long? parentSmartsheetRowId,
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
                
                // Add the TaskBlock
                timelineItems.Add(new TimelineItem
                {
                    Id = taskBlock.Id,
                    Type = "TaskBlock",
                    Item = taskBlock,
                    DisplayOrder = orderCounter++,
                    HierarchyLevel = hierarchyLevel,
                    ParentSmartsheetRowId = parentSmartsheetRowId,
                    SmartsheetRowId = taskBlock.SmartsheetRowId
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
                        ParentSmartsheetRowId = taskBlock.SmartsheetRowId,
                        SmartsheetRowId = evt.SmartsheetRowId
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
                        taskBlock.SmartsheetRowId,
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
                    ParentSmartsheetRowId = parentSmartsheetRowId,
                    SmartsheetRowId = evt.SmartsheetRowId
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

    private async Task<List<long?>> CreateUnifiedTimelineRowsBatchAsync(long sheetId, List<TimelineItem> timelineItems, string accessToken)
    {
        const int BATCH_SIZE = 300;
        var allResults = new List<long?>();

        for (int i = 0; i < timelineItems.Count; i += BATCH_SIZE)
        {
            var batch = timelineItems.Skip(i).Take(BATCH_SIZE).ToList();
            var batchResults = await CreateUnifiedTimelineRowsBatch(sheetId, batch, accessToken);
            allResults.AddRange(batchResults);

            // Update the SmartsheetRowId for items in this batch so subsequent batches can reference them as parents
            for (int j = 0; j < batch.Count && j < batchResults.Count; j++)
            {
                if (batchResults[j] != null)
                {
                    batch[j].SmartsheetRowId = batchResults[j];
                    
                    // Update the original entity
                    if (batch[j].Type == "TaskBlock" && batch[j].Item is TaskBlock taskBlock)
                    {
                        taskBlock.SmartsheetRowId = batchResults[j];
                    }
                    else if (batch[j].Type == "Event" && batch[j].Item is ProjectEvent evt)
                    {
                        evt.SmartsheetRowId = batchResults[j];
                    }
                    
                    // Update ParentSmartsheetRowId for any child items in subsequent batches
                    UpdateChildParentReferences(timelineItems, batch[j]);
                }
            }
        }

        return allResults;
    }

    private async Task<List<long?>> CreateUnifiedTimelineRowsBatch(long sheetId, List<TimelineItem> timelineItems, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var rowRequests = new List<object>();
            foreach (var item in timelineItems)
            {
                SmartSheetRow? row = null;
                
                if (item.Type == "TaskBlock" && item.Item is TaskBlock taskBlock)
                {
                    row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
                }
                else if (item.Type == "Event" && item.Item is ProjectEvent evt)
                {
                    row = await TransformEventToRowAsync(evt, sheetId, accessToken);
                }

                if (row != null)
                {
                    var rowRequest = new Dictionary<string, object>
                    {
                        ["toBottom"] = true,
                        ["cells"] = row.Cells
                    };

                    // Add parentId if this is a child item
                    if (item.ParentSmartsheetRowId.HasValue)
                    {
                        rowRequest["parentId"] = item.ParentSmartsheetRowId.Value;
                    }

                    rowRequests.Add(rowRequest);
                }
                else
                {
                    _logger.LogWarning("Failed to transform timeline item {ItemType} '{ItemId}' to Smartsheet row", 
                        item.Type, item.Id);
                }
            }

            _logger.LogInformation("Transformed {RowCount} timeline items into Smartsheet rows", rowRequests.Count);

            if (!rowRequests.Any())
            {
                _logger.LogWarning("No rows created from {ItemCount} timeline items", timelineItems.Count);
                return new List<long?>();
            }

            var json = JsonSerializer.Serialize(rowRequests, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending Smartsheet unified row creation request: {Json}", json);

            var response = await _httpClient.PostAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Smartsheet unified row creation response: {Response}", responseContent);
                
                var result = JsonSerializer.Deserialize<SmartSheetRowResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                var rowIds = result?.Result?.Select(r => (long?)r.Id).ToList() ?? new List<long?>();
                _logger.LogInformation("Parsed {RowCount} row IDs from Smartsheet response", rowIds.Count);
                
                return rowIds;
            }
            else
            {
                _logger.LogError("Failed to create SmartSheet unified timeline rows batch: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return timelineItems.Select(e => (long?)null).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SmartSheet unified timeline rows batch");
            return timelineItems.Select(e => (long?)null).ToList();
        }
    }

    private void UpdateChildParentReferences(List<TimelineItem> allItems, TimelineItem parentItem)
    {
        if (!parentItem.SmartsheetRowId.HasValue) return;

        foreach (var item in allItems.Where(i => i.DisplayOrder > parentItem.DisplayOrder))
        {
            // Update items that should have this item as their parent
            if (item.HierarchyLevel == parentItem.HierarchyLevel + 1 && 
                item.ParentSmartsheetRowId == null && // Not already set
                ShouldBeChildOf(item, parentItem))
            {
                item.ParentSmartsheetRowId = parentItem.SmartsheetRowId;
            }
        }
    }

    private bool ShouldBeChildOf(TimelineItem child, TimelineItem potentialParent)
    {
        if (child.Type == "Event" && potentialParent.Type == "TaskBlock" && child.Item is ProjectEvent evt)
        {
            return evt.TaskBlockId == potentialParent.Id;
        }
        
        if (child.Type == "TaskBlock" && potentialParent.Type == "TaskBlock" && child.Item is TaskBlock childBlock)
        {
            return childBlock.ParentTaskBlockId == potentialParent.Id;
        }
        
        return false;
    }

    private async Task<int> UpdateUnifiedTimelineRowsBatchAsync(long sheetId, List<TimelineItem> timelineItems, string accessToken)
    {
        const int BATCH_SIZE = 300;
        int totalUpdated = 0;

        for (int i = 0; i < timelineItems.Count; i += BATCH_SIZE)
        {
            var batch = timelineItems.Skip(i).Take(BATCH_SIZE).ToList();
            var updated = await UpdateUnifiedTimelineRowsBatch(sheetId, batch, accessToken);
            totalUpdated += updated;
        }

        return totalUpdated;
    }

    private async Task<int> UpdateUnifiedTimelineRowsBatch(long sheetId, List<TimelineItem> timelineItems, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var rowRequests = new List<object>();
            
            foreach (var item in timelineItems.Where(ti => ti.SmartsheetRowId.HasValue))
            {
                SmartSheetRow? row = null;
                
                if (item.Type == "TaskBlock" && item.Item is TaskBlock taskBlock)
                {
                    row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
                }
                else if (item.Type == "Event" && item.Item is ProjectEvent evt)
                {
                    row = await TransformEventToRowAsync(evt, sheetId, accessToken);
                }

                if (row != null)
                {
                    var rowRequest = new Dictionary<string, object>
                    {
                        ["id"] = item.SmartsheetRowId!.Value,
                        ["cells"] = row.Cells
                    };

                    // Note: We don't update parentId on existing rows as this could disrupt structure
                    // Parent relationships should be established at creation time

                    rowRequests.Add(rowRequest);
                }
            }

            if (!rowRequests.Any()) return 0;

            var json = JsonSerializer.Serialize(rowRequests, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending Smartsheet unified row update request: {Json}", json);

            var response = await _httpClient.PutAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated {RowCount} Smartsheet rows", rowRequests.Count);
                return rowRequests.Count;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update SmartSheet unified timeline rows batch: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SmartSheet unified timeline rows batch");
            return 0;
        }
    }

    private async Task<List<long?>> CreateTaskBlockRowsBatchAsync(long sheetId, List<TaskBlock> taskBlocks, string accessToken)
    {
        const int BATCH_SIZE = 300;
        var allResults = new List<long?>();

        for (int i = 0; i < taskBlocks.Count; i += BATCH_SIZE)
        {
            var batch = taskBlocks.Skip(i).Take(BATCH_SIZE).ToList();
            var batchResults = await CreateTaskBlockRowsBatch(sheetId, batch, accessToken);
            allResults.AddRange(batchResults);
        }

        return allResults;
    }

    private async Task<List<long?>> CreateTaskBlockRowsBatch(long sheetId, List<TaskBlock> taskBlocks, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var rows = new List<SmartSheetRow>();
            foreach (var taskBlock in taskBlocks)
            {
                var row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
                if (row != null) rows.Add(row);
            }

            if (!rows.Any()) return new List<long?>();

            var addRowsRequest = rows.Select(r => new { toBottom = true, cells = r.Cells }).ToArray();
            var json = JsonSerializer.Serialize(addRowsRequest, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<SmartSheetRowResponse>(responseContent);
                return result?.Result?.Select(r => (long?)r.Id).ToList() ?? new List<long?>();
            }
            else
            {
                _logger.LogError("Failed to create SmartSheet TaskBlock rows batch: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return taskBlocks.Select(b => (long?)null).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SmartSheet TaskBlock rows batch");
            return taskBlocks.Select(b => (long?)null).ToList();
        }
    }

    private async Task<int> UpdateTaskBlockRowsBatchAsync(long sheetId, List<TaskBlock> taskBlocks, string accessToken)
    {
        const int BATCH_SIZE = 300;
        int totalUpdated = 0;

        for (int i = 0; i < taskBlocks.Count; i += BATCH_SIZE)
        {
            var batch = taskBlocks.Skip(i).Take(BATCH_SIZE).ToList();
            var updated = await UpdateTaskBlockRowsBatch(sheetId, batch, accessToken);
            totalUpdated += updated;
        }

        return totalUpdated;
    }

    private async Task<int> UpdateTaskBlockRowsBatch(long sheetId, List<TaskBlock> taskBlocks, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var rows = new List<SmartSheetRow>();
            foreach (var taskBlock in taskBlocks.Where(b => b.SmartsheetRowId.HasValue))
            {
                var row = await TransformTaskBlockToRowAsync(taskBlock, sheetId, accessToken);
                if (row != null) 
                {
                    row.Id = taskBlock.SmartsheetRowId!.Value;
                    rows.Add(row);
                }
            }

            if (!rows.Any()) return 0;

            var updateRowsRequest = rows.Select(r => new { id = r.Id, cells = r.Cells }).ToArray();
            var json = JsonSerializer.Serialize(updateRowsRequest, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);

            if (response.IsSuccessStatusCode)
            {
                return rows.Count;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update SmartSheet TaskBlock rows batch: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SmartSheet TaskBlock rows batch");
            return 0;
        }
    }

    private async Task<List<long?>> CreateEventRowsBatchAsync(long sheetId, List<ProjectEvent> events, ICollection<TaskBlock> taskBlocks, string accessToken)
    {
        const int BATCH_SIZE = 300;
        var allResults = new List<long?>();

        for (int i = 0; i < events.Count; i += BATCH_SIZE)
        {
            var batch = events.Skip(i).Take(BATCH_SIZE).ToList();
            var batchResults = await CreateEventRowsBatch(sheetId, batch, taskBlocks, accessToken);
            allResults.AddRange(batchResults);
        }

        return allResults;
    }

    private async Task<List<long?>> CreateEventRowsBatch(long sheetId, List<ProjectEvent> events, ICollection<TaskBlock> taskBlocks, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var rows = new List<SmartSheetRow>();
            foreach (var eventItem in events)
            {
                var row = await TransformEventToRowAsync(eventItem, sheetId, accessToken);
                if (row != null) 
                {
                    var parentRowId = GetParentRowIdForEvent(eventItem, taskBlocks);
                    if (parentRowId.HasValue)
                    {
                        row.ParentId = parentRowId.Value;
                    }
                    rows.Add(row);
                }
                else
                {
                    _logger.LogWarning("Failed to transform event {EventId} '{Description}' to Smartsheet row", 
                        eventItem.Id, eventItem.Description);
                }
            }

            _logger.LogInformation("Transformed {RowCount} events into Smartsheet rows", rows.Count);

            if (!rows.Any()) 
            {
                _logger.LogWarning("No rows created from {EventCount} events", events.Count);
                return new List<long?>();
            }

            var addRowsRequest = rows.Select(r => new { toBottom = true, cells = r.Cells }).ToArray();
            var json = JsonSerializer.Serialize(addRowsRequest, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending Smartsheet row creation request: {Json}", json);

            var response = await _httpClient.PostAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Smartsheet row creation response: {Response}", responseContent);
                
                // Try parsing as single row response first (what Smartsheet actually returns)
                var result = JsonSerializer.Deserialize<SmartSheetRowResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                var rowIds = result?.Result?.Select(r => (long?)r.Id).ToList() ?? new List<long?>();
                _logger.LogInformation("Parsed {RowCount} row IDs from Smartsheet response", rowIds.Count);
                
                return rowIds;
            }
            else
            {
                _logger.LogError("Failed to create SmartSheet event rows batch: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return events.Select(e => (long?)null).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SmartSheet event rows batch");
            return events.Select(e => (long?)null).ToList();
        }
    }

    private async Task<int> UpdateEventRowsBatchAsync(long sheetId, List<ProjectEvent> events, string accessToken)
    {
        const int BATCH_SIZE = 300;
        int totalUpdated = 0;

        for (int i = 0; i < events.Count; i += BATCH_SIZE)
        {
            var batch = events.Skip(i).Take(BATCH_SIZE).ToList();
            var updated = await UpdateEventRowsBatch(sheetId, batch, accessToken);
            totalUpdated += updated;
        }

        return totalUpdated;
    }

    private async Task<int> UpdateEventRowsBatch(long sheetId, List<ProjectEvent> events, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var rows = new List<SmartSheetRow>();
            foreach (var eventItem in events.Where(e => e.SmartsheetRowId.HasValue))
            {
                var row = await TransformEventToRowAsync(eventItem, sheetId, accessToken);
                if (row != null) 
                {
                    row.Id = eventItem.SmartsheetRowId!.Value;
                    rows.Add(row);
                }
            }

            if (!rows.Any()) return 0;

            var updateRowsRequest = rows.Select(r => new { id = r.Id, cells = r.Cells }).ToArray();
            var json = JsonSerializer.Serialize(updateRowsRequest, SmartSheetJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}/rows", content);

            if (response.IsSuccessStatusCode)
            {
                return rows.Count;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update SmartSheet event rows batch: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SmartSheet event rows batch");
            return 0;
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

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Get sheet columns
            var response = await _httpClient.GetAsync($"https://api.smartsheet.com/2.0/sheets/{sheetId}?include=columns");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get sheet columns: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return null;
            }

            var sheet = JsonSerializer.Deserialize<SmartSheetColumnResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            if (sheet?.Columns == null)
            {
                _logger.LogWarning("No columns returned from sheet {SheetId}", sheetId);
                return null;
            }

            // Build title -> ID mapping
            var mapping = new Dictionary<string, long>();
            foreach (var column in sheet.Columns)
            {
                if (!string.IsNullOrEmpty(column.Title))
                {
                    mapping[column.Title] = column.Id;
                }
            }

            // Cache the mapping
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
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Read all rows from the sheet
            var response = await _httpClient.GetAsync($"https://api.smartsheet.com/2.0/sheets/{project.SmartsheetSheetId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to read Smartsheet: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return -1;
            }

            var sheet = JsonSerializer.Deserialize<SmartSheetReadResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (sheet?.Rows == null)
            {
                _logger.LogWarning("No rows returned from Smartsheet");
                return 0;
            }

            // Build a map of SmartsheetRowId -> RowNumber
            var rowIdToRowNumber = new Dictionary<long, int>();
            foreach (var row in sheet.Rows)
            {
                rowIdToRowNumber[row.Id] = row.RowNumber;
            }

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

public class SmartSheetCreateResponse
{
    public string? Message { get; set; }
    public int ResultCode { get; set; }
    public SmartSheetResult? Result { get; set; }
}

public class SmartSheetResult
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AccessLevel { get; set; }
    public string? Permalink { get; set; }
}

public class SmartSheetRowResponse
{
    public List<SmartSheetRowResult>? Result { get; set; }
}

public class SmartSheetSingleRowResponse
{
    public string? Message { get; set; }
    public int ResultCode { get; set; }
    public SmartSheetRowResult? Result { get; set; }
}

public class SmartSheetRowResult
{
    public long Id { get; set; }
}

public class SmartSheetRow
{
    public long? Id { get; set; }
    public long? ParentId { get; set; }
    public List<SmartSheetCell> Cells { get; set; } = new();
}

public class SmartSheetCell
{
    public long ColumnId { get; set; }
    public object? Value { get; set; }
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
    public bool IsNew => SmartsheetRowId == null;
}