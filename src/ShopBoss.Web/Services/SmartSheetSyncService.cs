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

    public SmartSheetSyncService(
        ShopBossDbContext context,
        ILogger<SmartSheetSyncService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<SmartSheetSyncResult> SyncProjectEventsAsync(string projectId, string accessToken)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.Events)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return SmartSheetSyncResult.CreateError("Project not found");

            // Get or create sheet for this project
            var sheetId = await GetOrCreateProjectSheetAsync(project, accessToken);
            if (sheetId == null)
                return SmartSheetSyncResult.CreateError("Failed to create/access SmartSheet");

            // Sync events to SmartSheet
            int created = 0, updated = 0;
            foreach (var eventItem in project.Events.OrderBy(e => e.EventDate))
            {
                if (eventItem.RowNumber == null)
                {
                    // Create new row
                    var rowId = await CreateRowAsync(sheetId.Value, eventItem, accessToken);
                    if (rowId != null)
                    {
                        eventItem.RowNumber = rowId;
                        created++;
                    }
                }
                else
                {
                    // Update existing row
                    var success = await UpdateRowAsync(sheetId.Value, eventItem, accessToken);
                    if (success) updated++;
                }
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

    private async Task<long?> GetOrCreateProjectSheetAsync(Project project, string accessToken)
    {
        try
        {
            // For now, create a new sheet each time
            // TODO: Store sheet ID on Project model for reuse
            return await CreateProjectSheetAsync(project, accessToken);
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
            var templateSheetId = _configuration["SmartSheet:TemplateSheetId"] ?? "2455059368464260";
            var workspaceId = _configuration["SmartSheet:WorkspaceId"] ?? "6590163225732996";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var createRequest = new
            {
                name = $"ShopBoss - {project.ProjectName}",
                fromId = long.Parse(templateSheetId),
                destinationType = "workspace",
                destinationId = long.Parse(workspaceId),
                include = new[] { "data", "attachments", "discussions", "cellLinks" }
            };

            var json = JsonSerializer.Serialize(createRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.smartsheet.com/2.0/sheets/copy", content);
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

    private async Task<long?> CreateRowAsync(long sheetId, ProjectEvent eventItem, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var row = TransformEventToRow(eventItem);
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

            var row = TransformEventToRow(eventItem);
            row.Id = eventItem.RowNumber!.Value; // Set the row ID for update

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

    private SmartSheetRow TransformEventToRow(ProjectEvent eventItem)
    {
        // Column IDs from template sheet (2455059368464260)
        const long TASK_NAME_COLUMN = 7794572675207044L; // "Task Name"
        const long STATUS_COLUMN = 2165073140993924L; // "Status" 
        const long START_DATE_COLUMN = 1250279466684292L; // "Start Date"
        const long END_DATE_COLUMN = 5753879094054788L; // "End Date"
        const long DURATION_COLUMN = 3502079280369540L; // "Duration"
        const long NOTES_COLUMN = 8005678907740036L; // "Notes"
        const long ASSIGNED_TO_COLUMN = 2939129326948228L; // "Assigned To"
        const long SHOPBOSS_TYPE_COLUMN = 6524743708266372L; // "ShopBoss Type"

        var cells = new List<SmartSheetCell>
        {
            new SmartSheetCell { ColumnId = TASK_NAME_COLUMN, Value = GetTaskName(eventItem) },
            new SmartSheetCell { ColumnId = STATUS_COLUMN, Value = "Open" },
            new SmartSheetCell { ColumnId = START_DATE_COLUMN, Value = eventItem.EventDate.ToString("yyyy-MM-dd") },
            new SmartSheetCell { ColumnId = ASSIGNED_TO_COLUMN, Value = eventItem.CreatedBy ?? "System" },
            new SmartSheetCell { ColumnId = NOTES_COLUMN, Value = GetEventNotes(eventItem) },
            new SmartSheetCell { ColumnId = SHOPBOSS_TYPE_COLUMN, Value = eventItem.EventType }
        };

        return new SmartSheetRow { Cells = cells };
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
    public List<SmartSheetCell> Cells { get; set; } = new();
}

public class SmartSheetCell
{
    public long ColumnId { get; set; }
    public object? Value { get; set; }
}