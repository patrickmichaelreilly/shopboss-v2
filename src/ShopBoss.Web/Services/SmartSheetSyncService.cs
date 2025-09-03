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

            // Get or create sheet for this project
            var sheetId = await GetOrCreateProjectSheetAsync(project, accessToken);
            if (sheetId == null)
                return SmartSheetSyncResult.CreateError("Failed to create/access SmartSheet");

            // First, sync TaskBlocks as parent rows (batched)
            int blocksCreated = 0, blocksUpdated = 0;
            var newTaskBlocks = project.TaskBlocks.Where(b => b.SmartsheetRowId == null).OrderBy(b => b.DisplayOrder).ToList();
            var existingTaskBlocks = project.TaskBlocks.Where(b => b.SmartsheetRowId != null).OrderBy(b => b.DisplayOrder).ToList();

            if (newTaskBlocks.Any())
            {
                var createdIds = await CreateTaskBlockRowsBatchAsync(sheetId.Value, newTaskBlocks, accessToken);
                for (int i = 0; i < newTaskBlocks.Count && i < createdIds.Count; i++)
                {
                    if (createdIds[i] != null)
                    {
                        newTaskBlocks[i].SmartsheetRowId = createdIds[i];
                        blocksCreated++;
                    }
                }
            }

            if (existingTaskBlocks.Any())
            {
                var updatedCount = await UpdateTaskBlockRowsBatchAsync(sheetId.Value, existingTaskBlocks, accessToken);
                blocksUpdated = updatedCount;
            }

            // Then, sync events to SmartSheet (batched)
            int created = 0, updated = 0;
            var newEvents = project.Events.Where(e => e.SmartsheetRowId == null).OrderBy(e => e.EventDate).ToList();
            var existingEvents = project.Events.Where(e => e.SmartsheetRowId != null).OrderBy(e => e.EventDate).ToList();

            if (newEvents.Any())
            {
                var createdIds = await CreateEventRowsBatchAsync(sheetId.Value, newEvents, project.TaskBlocks, accessToken);
                for (int i = 0; i < newEvents.Count && i < createdIds.Count; i++)
                {
                    if (createdIds[i] != null)
                    {
                        newEvents[i].SmartsheetRowId = createdIds[i];
                        created++;
                    }
                }
            }

            if (existingEvents.Any())
            {
                var updatedCount = await UpdateEventRowsBatchAsync(sheetId.Value, existingEvents, accessToken);
                updated = updatedCount;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("SmartSheet sync completed for project {ProjectId}: {Created} created, {Updated} updated", 
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
                name = $"ShopBoss - {project.ProjectName}",
                fromId = long.Parse(templateSheetId)
            };

            var json = JsonSerializer.Serialize(createRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"https://api.smartsheet.com/2.0/workspaces/{workspaceId}/sheets", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<SmartSheetCreateResponse>(responseContent);
                _logger.LogInformation("Created SmartSheet for project {ProjectName} with ID {SheetId}", 
                    project.ProjectName, result?.Result?.Id);
                return result?.Result?.Id;
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
            
            var addRowsRequest = new { toBottom = true, rows = new[] { row } };

            var json = JsonSerializer.Serialize(addRowsRequest);
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

            var updateRowsRequest = new { rows = new[] { row } };

            var json = JsonSerializer.Serialize(updateRowsRequest);
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
            var addRowsRequest = new { toBottom = true, rows = new[] { row } };

            var json = JsonSerializer.Serialize(addRowsRequest);
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

            var updateRowsRequest = new { rows = new[] { row } };

            var json = JsonSerializer.Serialize(updateRowsRequest);
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

            var addRowsRequest = new { toBottom = true, rows };
            var json = JsonSerializer.Serialize(addRowsRequest);
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

            var updateRowsRequest = new { rows };
            var json = JsonSerializer.Serialize(updateRowsRequest);
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
            }

            if (!rows.Any()) return new List<long?>();

            var addRowsRequest = new { toBottom = true, rows };
            var json = JsonSerializer.Serialize(addRowsRequest);
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

            var updateRowsRequest = new { rows };
            var json = JsonSerializer.Serialize(updateRowsRequest);
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
                PropertyNameCaseInsensitive = true
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
    public SmartSheetResult? Result { get; set; }
}

public class SmartSheetResult
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SmartSheetRowResponse
{
    public List<SmartSheetRowResult>? Result { get; set; }
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