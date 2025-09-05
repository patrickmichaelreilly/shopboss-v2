using Smartsheet.Api;
using Smartsheet.Api.Models;
using ShopBoss.Web.Models;
using ShopBoss.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ShopBoss.Web.Hubs;
//

namespace ShopBoss.Web.Services;

public class SmartSheetService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SmartSheetService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ProjectAttachmentService _attachmentService;
    private readonly IHubContext<ImportProgressHub> _hubContext;
    private readonly TimelineService _timelineService;

    public SmartSheetService(
        ShopBossDbContext context,
        ILogger<SmartSheetService> logger,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ProjectAttachmentService attachmentService,
        IHubContext<ImportProgressHub> hubContext,
        TimelineService timelineService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _attachmentService = attachmentService;
        _hubContext = hubContext;
        _timelineService = timelineService;
    }

    private async Task<SmartsheetClient?> GetSmartSheetClientAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null) return null;

        // Check if token is expired and try to refresh
        if (await IsTokenExpiredAsync())
        {
            var refreshed = await RefreshTokenAsync();
            if (!refreshed)
            {
                _logger.LogWarning("Token expired and refresh failed");
                return null;
            }
        }

        var token = session.GetString("ss_token");
        if (string.IsNullOrEmpty(token)) return null;

        return new SmartsheetBuilder()
            .SetAccessToken(token)
            .Build();
    }

    private SmartsheetClient? GetSmartSheetClient()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null) return null;

        var token = session.GetString("ss_token");
        if (string.IsNullOrEmpty(token)) return null;

        return new SmartsheetBuilder()
            .SetAccessToken(token)
            .Build();
    }

    private Task<bool> IsTokenExpiredAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null) return Task.FromResult(true);

        var expiresString = session.GetString("ss_expires");
        if (string.IsNullOrEmpty(expiresString)) return Task.FromResult(true);

        if (DateTime.TryParse(expiresString, out var expiresAt))
        {
            // Consider expired if within 5 minutes of expiry (buffer time)
            return Task.FromResult(expiresAt <= DateTime.UtcNow.AddMinutes(5));
        }

        return Task.FromResult(true); // If we can't parse, assume expired
    }

    private async Task<bool> RefreshTokenAsync()
    {
        try
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return false;

            var refreshToken = session.GetString("ss_refresh");
            if (string.IsNullOrEmpty(refreshToken)) return false;

            // Use HttpClient to call the refresh endpoint
            var httpClient = new HttpClient();
            var clientId = _configuration["SmartSheet:ClientId"];
            var clientSecret = _configuration["SmartSheet:ClientSecret"];

            var refreshRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!)
            });

            var response = await httpClient.PostAsync("https://api.smartsheet.com/2.0/token", refreshRequest);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to refresh SmartSheet token: {StatusCode}", response.StatusCode);
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<SmartSheetTokenResponse>(responseContent, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Invalid refresh token response");
                return false;
            }

            // Update session with new tokens
            var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600);
            session.SetString("ss_token", tokenResponse.AccessToken);
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                session.SetString("ss_refresh", tokenResponse.RefreshToken);
            }
            session.SetString("ss_expires", expiresAt.ToString("O"));

            _logger.LogInformation("SmartSheet token refreshed automatically");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing SmartSheet token in service");
            return false;
        }
    }

    /// <summary>
    /// Check if current user has active SmartSheet session
    /// </summary>
    public bool HasSmartSheetSession()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        return session?.GetString("ss_token") != null;
    }

    /// <summary>
    /// Get current user's SmartSheet email from session
    /// </summary>
    public string? GetCurrentUserEmail()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        return session?.GetString("ss_user");
    }

    /// <summary>
    /// Get SmartSheet information by ID
    /// </summary>
    public async Task<SmartSheetLinkInfo?> GetSheetInfoAsync(long sheetId)
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null)
            {
                _logger.LogWarning("No SmartSheet session found when getting sheet info for {SheetId}", sheetId);
                return null;
            }

            var sheet = await Task.Run(() => smartsheet.SheetResources.GetSheet(sheetId, null, null, null, null, null, null, null));
            
            return new SmartSheetLinkInfo
            {
                Id = sheet.Id ?? 0,
                Name = sheet.Name ?? string.Empty,
                Permalink = sheet.Permalink,
                CreatedAt = sheet.CreatedAt,
                ModifiedAt = sheet.ModifiedAt,
                RowCount = (int)(sheet.TotalRowCount ?? 0)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SmartSheet info for sheet {SheetId}", sheetId);
            return null;
        }
    }

    /// <summary>
    /// Build a column title -> column ID map for a sheet using the Smartsheet SDK
    /// </summary>
    public async Task<Dictionary<string, long>> GetColumnMapAsync(long sheetId)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return result;

            // Columns are available on the Sheet object; no special include required
            var sheet = await Task.Run(() => smartsheet.SheetResources.GetSheet(sheetId, null, null, null, null, null, null, null));
            if (sheet.Columns == null) return result;

            foreach (var col in sheet.Columns)
            {
                if (!string.IsNullOrEmpty(col.Title) && col.Id.HasValue)
                {
                    result[col.Title!] = col.Id.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building column map for sheet {SheetId}", sheetId);
        }
        return result;
    }

    /// <summary>
    /// Build a rowId -> rowNumber map for a sheet using the Smartsheet SDK
    /// </summary>
    public async Task<Dictionary<long, int>> GetRowIdToRowNumberMapAsync(long sheetId)
    {
        var map = new Dictionary<long, int>();
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return map;

            var sheet = await Task.Run(() => smartsheet.SheetResources.GetSheet(sheetId, null, null, null, null, null, null, null));
            if (sheet.Rows == null) return map;

            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                var row = sheet.Rows[i];
                if (row.Id.HasValue)
                {
                    var number = row.RowNumber ?? (i + 1);
                    map[row.Id.Value] = number;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building rowId->rowNumber map for sheet {SheetId}", sheetId);
        }
        return map;
    }

    /// <summary>
    /// Copy a sheet into a workspace using the Smartsheet SDK and return new sheetId
    /// </summary>
    public async Task<long?> CopySheetToWorkspaceAsync(long templateSheetId, long workspaceId, string newName, IList<SheetCopyInclusion>? include = null)
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return null;

            var destination = new ContainerDestination
            {
                DestinationType = DestinationType.WORKSPACE,
                DestinationId = workspaceId,
                NewName = newName
            };

            var copied = await Task.Run(() => smartsheet.SheetResources.CopySheet(templateSheetId, destination, include));
            return copied?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying sheet {TemplateSheetId} to workspace {WorkspaceId}", templateSheetId, workspaceId);
            return null;
        }
    }

    /// <summary>
    /// Add rows to a sheet and return their IDs
    /// </summary>
    public async Task<List<long>> AddRowsAsync(long sheetId, IList<Row> rows)
    {
        var ids = new List<long>();
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return ids;

            var resultRows = await Task.Run(() => smartsheet.SheetResources.RowResources.AddRows(sheetId, rows));
            foreach (var r in resultRows ?? new List<Row>())
            {
                if (r.Id.HasValue) ids.Add(r.Id.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding rows to sheet {SheetId}", sheetId);
        }
        return ids;
    }

    /// <summary>
    /// Update rows on a sheet. Returns count updated.
    /// </summary>
    public async Task<int> UpdateRowsAsync(long sheetId, IList<Row> rows)
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return 0;

            var resultRows = await Task.Run(() => smartsheet.SheetResources.RowResources.UpdateRows(sheetId, rows));
            return resultRows?.Count ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rows on sheet {SheetId}", sheetId);
            return 0;
        }
    }

    /// <summary>
    /// Get all row IDs for a sheet
    /// </summary>
    public async Task<List<long>> GetAllRowIdsAsync(long sheetId)
    {
        var ids = new List<long>();
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return ids;

            var sheet = await Task.Run(() => smartsheet.SheetResources.GetSheet(sheetId, null, null, null, null, null, null, null));
            foreach (var row in sheet.Rows ?? new List<Row>())
            {
                if (row.Id.HasValue) ids.Add(row.Id.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching row IDs for sheet {SheetId}", sheetId);
        }
        return ids;
    }

    /// <summary>
    /// Delete rows in a sheet via SDK (supports batching by caller)
    /// </summary>
    public async Task<bool> DeleteRowsAsync(long sheetId, IList<long> rowIds)
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return false;
            await Task.Run(() => smartsheet.SheetResources.RowResources.DeleteRows(sheetId, rowIds));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rows from sheet {SheetId}", sheetId);
            return false;
        }
    }

    /// <summary>
    /// Search for SmartSheets that might match a project
    /// </summary>
    public async Task<List<SmartSheetLinkInfo>> SearchSheetsAsync(string searchTerm)
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null)
            {
                _logger.LogWarning("No SmartSheet session found when searching sheets");
                return new List<SmartSheetLinkInfo>();
            }

            // Get all sheets accessible to the user
            var sheets = new List<Smartsheet.Api.Models.Sheet>();
            var paginationParams = new PaginationParameters(false, 100, 1);
            
            PaginatedResult<Smartsheet.Api.Models.Sheet> sheetListResult;
            do
            {
                sheetListResult = await Task.Run(() => smartsheet.SheetResources.ListSheets(null, paginationParams, null));
                if (sheetListResult.Data != null)
                {
                    sheets.AddRange(sheetListResult.Data);
                }
                paginationParams.Page++;
            } while (sheetListResult.TotalPages > paginationParams.Page - 1);

            // Filter sheets that match the search term
            var matchingSheets = sheets
                .Where(s => s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Take(20) // Limit results
                .Select(s => new SmartSheetLinkInfo
                {
                    Id = s.Id ?? 0,
                    Name = s.Name ?? string.Empty,
                    Permalink = s.Permalink,
                    CreatedAt = s.CreatedAt,
                    ModifiedAt = s.ModifiedAt,
                    RowCount = 0 // Row count not available in list view
                })
                .ToList();

            return matchingSheets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching SmartSheets with term '{SearchTerm}'", searchTerm);
            return new List<SmartSheetLinkInfo>();
        }
    }

    /// <summary>
    /// Link a project to a SmartSheet
    /// </summary>
    public async Task<bool> LinkProjectToSheetAsync(string projectId, long sheetId)
    {
        try
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                _logger.LogWarning("Project {ProjectId} not found when linking to SmartSheet {SheetId}", projectId, sheetId);
                return false;
            }

            // Verify the sheet exists and is accessible
            var sheetInfo = await GetSheetInfoAsync(sheetId);
            if (sheetInfo == null)
            {
                _logger.LogWarning("SmartSheet {SheetId} not accessible when linking to project {ProjectId}", sheetId, projectId);
                return false;
            }

            project.SmartSheetId = sheetId;
            project.SmartSheetLastSync = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Linked project {ProjectId} to SmartSheet {SheetId}", projectId, sheetId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking project {ProjectId} to SmartSheet {SheetId}", projectId, sheetId);
            return false;
        }
    }

    /// <summary>
    /// Unlink a project from its SmartSheet
    /// </summary>
    public async Task<bool> UnlinkProjectFromSheetAsync(string projectId)
    {
        try
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                _logger.LogWarning("Project {ProjectId} not found when unlinking from SmartSheet", projectId);
                return false;
            }

            var previousSheetId = project.SmartSheetId;
            project.SmartSheetId = null;
            project.SmartSheetLastSync = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully unlinked project {ProjectId} ({ProjectName}) from SmartSheet {SheetId}", 
                projectId, project.ProjectName, previousSheetId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking project {ProjectId} from SmartSheet", projectId);
            return false;
        }
    }

    /// <summary>
    /// Get all projects linked to SmartSheets
    /// </summary>
    public async Task<List<Project>> GetLinkedProjectsAsync()
    {
        return await _context.Projects
            .Where(p => p.SmartSheetId.HasValue && !p.IsArchived)
            .OrderBy(p => p.ProjectName)
            .ToListAsync();
    }

    /// <summary>
    /// Get accessible workspaces with their sheets
    /// </summary>
    public async Task<List<object>> GetAccessibleWorkspacesAsync()
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return new List<object>();

            var workspaces = new List<object>();

            // Get all workspaces
            var workspaceList = await Task.Run(() => smartsheet.WorkspaceResources.ListWorkspaces());
            
            foreach (var workspace in workspaceList.Data ?? new List<Smartsheet.Api.Models.Workspace>())
            {
                // Get workspace details to include sheets
                var workspaceDetails = await Task.Run(() => 
                    smartsheet.WorkspaceResources.GetWorkspace(workspace.Id ?? 0, null));

                workspaces.Add(new
                {
                    id = workspace.Id ?? 0,
                    name = workspace.Name ?? "",
                    sheetCount = workspaceDetails.Sheets?.Count ?? 0,
                    sheets = workspaceDetails.Sheets?.Select(s => new
                    {
                        id = s.Id ?? 0,
                        name = s.Name ?? "",
                        modifiedAt = s.ModifiedAt,
                        permalink = s.Permalink
                    }).Cast<object>().ToList() ?? new List<object>()
                });
            }

            return workspaces;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accessible workspaces");
            return new List<object>();
        }
    }

    /// <summary>
    /// Get specific workspace by ID
    /// </summary>
    public async Task<object?> GetWorkspaceByIdAsync(long workspaceId)
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return null;

            // Get workspace details with sheets
            var workspaceDetails = await Task.Run(() => 
                smartsheet.WorkspaceResources.GetWorkspace(workspaceId, null));

            if (workspaceDetails == null) return null;

            return new
            {
                id = workspaceDetails.Id ?? 0,
                name = workspaceDetails.Name ?? "",
                sheetCount = workspaceDetails.Sheets?.Count ?? 0,
                sheets = workspaceDetails.Sheets?.Select(s => new
                {
                    id = s.Id ?? 0,
                    name = s.Name ?? "",
                    modifiedAt = s.ModifiedAt,
                    permalink = s.Permalink
                }).Cast<object>().ToList() ?? new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workspace {WorkspaceId}", workspaceId);
            return null;
        }
    }

    /// <summary>
    /// Get specific workspace by name
    /// </summary>
    public async Task<object?> GetWorkspaceByNameAsync(string workspaceName)
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null) return null;

            // Get all workspaces to find the one with matching name
            var workspaceList = await Task.Run(() => smartsheet.WorkspaceResources.ListWorkspaces());
            
            var matchingWorkspace = workspaceList.Data?.FirstOrDefault(w => 
                string.Equals(w.Name, workspaceName, StringComparison.OrdinalIgnoreCase));

            if (matchingWorkspace == null) return null;

            return await GetWorkspaceByIdAsync(matchingWorkspace.Id ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workspace by name '{WorkspaceName}'", workspaceName);
            return null;
        }
    }



    // Migration Tool Methods

    /// <summary>
    /// Get sheet details including summary, attachments, and comments for migration
    /// </summary>
    public async Task<SheetDetailsResult> GetSheetDetailsAsync(long sheetId)
    {
        var result = new SheetDetailsResult();
        
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null)
            {
                throw new InvalidOperationException("No SmartSheet session. Please authenticate first.");
            }

            // Get sheet with all data including discussions/comments and summary
            var includes = new List<SheetLevelInclusion> 
            { 
                SheetLevelInclusion.ATTACHMENTS,
                SheetLevelInclusion.DISCUSSIONS,
                SheetLevelInclusion.SUMMARY
            };
            var sheet = await Task.Run(() => smartsheet.SheetResources.GetSheet(sheetId, includes, null, null, null, null, null, null));
            
            result.SheetId = sheetId;
            result.SheetName = sheet.Name ?? "";

            // Build row ID to number mapping
            var rowIdToNumberMap = new Dictionary<long, int>();
            if (sheet.Rows != null)
            {
                for (int i = 0; i < sheet.Rows.Count; i++)
                {
                    var row = sheet.Rows[i];
                    if (row.Id.HasValue)
                    {
                        rowIdToNumberMap[row.Id.Value] = row.RowNumber ?? (i + 1);
                    }
                }
            }

            // Get sheet summary using separate API call (like original working code)
            try
            {
                var summary = await Task.Run(() => smartsheet.SheetResources.SummaryResources.GetSheetSummary(sheetId, null, null));
                
                if (summary?.Fields != null)
                {
                    
                    foreach (var field in summary.Fields)
                    {
                        var key = field.Title ?? "Unknown Field";
                        var value = "";
                        
                        if (field.ObjectValue != null)
                        {
                            // Handle different object value types like the original code
                            if (field.ObjectValue is Smartsheet.Api.Models.StringObjectValue stringValue)
                            {
                                value = stringValue.Value ?? "";
                            }
                            else if (field.ObjectValue is Smartsheet.Api.Models.BooleanObjectValue boolValue)
                            {
                                value = boolValue.Value.ToString();
                            }
                            else if (field.ObjectValue is Smartsheet.Api.Models.NumberObjectValue numberValue)
                            {
                                value = numberValue.Value.ToString();
                            }
                            else if (field.ObjectValue is Smartsheet.Api.Models.DateObjectValue dateValue)
                            {
                                value = dateValue.Value.ToString();
                            }
                            else
                            {
                                value = field.ObjectValue.ToString() ?? "";
                            }
                        }
                        else if (field.DisplayValue != null)
                        {
                            value = field.DisplayValue;
                        }
                        
                        result.Summary[key] = value;
                    }
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get sheet summary for {SheetId}: {ErrorMessage}", sheetId, ex.Message);
            }

            // Get attachments using the original working approach
            var attachmentsList = await Task.Run(() => smartsheet.SheetResources.AttachmentResources.ListAttachments(sheetId, new PaginationParameters(true, null, null)));
            result.Attachments = new List<AttachmentInfo>();
            
            if (attachmentsList?.Data != null)
            {
                
                foreach (var attachment in attachmentsList.Data)
                {
                    try
                    {
                        // Get full attachment details including URL - this is the key difference
                        var attachmentDetails = await Task.Run(() => smartsheet.SheetResources.AttachmentResources.GetAttachment(sheetId, attachment.Id ?? 0));
                        
                        result.Attachments.Add(new AttachmentInfo
                        {
                            Id = attachmentDetails.Id ?? 0,
                            Name = attachmentDetails.Name ?? "",
                            SizeInKb = attachmentDetails.SizeInKb,
                            CreatedAt = attachmentDetails.CreatedAt,
                            CreatedBy = attachmentDetails.CreatedBy?.Email,
                            RowNumber = attachmentDetails.ParentId.HasValue && rowIdToNumberMap.ContainsKey(attachmentDetails.ParentId.Value) 
                                ? rowIdToNumberMap[attachmentDetails.ParentId.Value] : null,
                            AttachmentType = attachmentDetails.AttachmentType?.ToString(),
                            MimeType = attachmentDetails.MimeType,
                            Url = attachmentDetails.Url // This will now have the actual download URL
                        });
                        
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get details for attachment {AttachmentId}: {AttachmentName}", 
                            attachment.Id, attachment.Name);
                    }
                }
            }

            // Get comments from discussions - use the original working approach
            // Need to get discussions first, then fetch each discussion individually to get comments
            var allComments = new List<CommentInfo>();
            
            
            try
            {
                var discussionResult = await Task.Run(() => smartsheet.SheetResources.DiscussionResources.ListDiscussions(sheetId));
                
                if (discussionResult?.Data != null && discussionResult.Data.Any())
                {
                    
                    foreach (var discussion in discussionResult.Data)
                    {
                        try
                        {
                            if (discussion.Id.HasValue)
                            {
                                
                                // Fetch the full discussion to get comments - this is the key difference
                                var fullDiscussion = await Task.Run(() => smartsheet.SheetResources.DiscussionResources.GetDiscussion(sheetId, discussion.Id.Value));
                                
                                if (fullDiscussion?.Comments != null && fullDiscussion.Comments.Any())
                                {
                                    _logger.LogInformation("Discussion {DiscussionId} has {CommentCount} comments", discussion.Id.Value, fullDiscussion.Comments.Count);
                                    
                                    // Get row number for this discussion - need to find the parent row
                                    int? rowNumber = null;
                                    if (fullDiscussion.ParentId.HasValue && rowIdToNumberMap.ContainsKey(fullDiscussion.ParentId.Value))
                                    {
                                        rowNumber = rowIdToNumberMap[fullDiscussion.ParentId.Value];
                                    }
                                    
                                    foreach (var comment in fullDiscussion.Comments)
                                    {
                                        allComments.Add(new CommentInfo
                                        {
                                            Id = comment.Id ?? 0,
                                            Text = comment.Text ?? "",
                                            CreatedAt = comment.CreatedAt,
                                            CreatedBy = comment.CreatedBy?.Email,
                                            RowNumber = rowNumber
                                        });
                                        
                                    }
                                }
                                else
                                {
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch discussion {DiscussionId}: {ErrorMessage}", discussion.Id, ex.Message);
                        }
                    }
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get discussions for sheet {SheetId}: {ErrorMessage}", sheetId, ex.Message);
            }
            
            _logger.LogInformation("Total comments extracted: {CommentCount}", allComments.Count);
            result.Comments = allComments;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sheet details for {SheetId}", sheetId);
            throw;
        }
    }

    /// <summary>
    /// Import a SmartSheet as a Project with attachments and comments
    /// </summary>
    public async Task<ImportResult> ImportProjectAsync(long sheetId, string importId = "")
    {
        // Generate importId if not provided
        if (string.IsNullOrEmpty(importId))
        {
            importId = Guid.NewGuid().ToString();
        }
        
        try
        {
            await SendProgressUpdate(importId, 5, "Starting SmartSheet import...");
            
            // Define import steps for progress tracking
            const int totalSteps = 7;
            int currentStep = 0;
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null)
            {
                await SendProgressUpdate(importId, 0, "Authentication failed - No SmartSheet session");
                return new ImportResult 
                { 
                    Success = false, 
                    Message = "No SmartSheet session. Please authenticate first." 
                };
            }

            // Step 1: Get sheet details
            currentStep++;
            await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), "Fetching sheet details from SmartSheet...");
            var sheetDetails = await GetSheetDetailsAsync(sheetId);

            // Initialize project data from SmartSheet Summary
            var projectData = new ProjectData 
            { 
                ProjectName = sheetDetails.SheetName // Default fallback
            };

            // Step 2: Process Summary fields
            currentStep++;
            await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), "Processing SmartSheet Summary fields...");
            
            // Populate project fields from SmartSheet Summary
            if (sheetDetails.Summary.Any())
            {
                _logger.LogInformation("Auto-populating project fields from {SummaryCount} summary fields", sheetDetails.Summary.Count);
                await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), $"Mapping {sheetDetails.Summary.Count} summary fields to project data...");
                
                foreach (var summaryField in sheetDetails.Summary)
                {
                    var key = summaryField.Key?.Trim() ?? "";
                    var value = summaryField.Value?.Trim() ?? "";
                    
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    
                    _logger.LogInformation("Processing summary field: '{Key}' = '{Value}'", key, value);
                    
                    // Map SmartSheet Summary fields to project data
                    switch (key.ToLower())
                    {
                        case "project id":
                            projectData.ProjectId = value;
                            _logger.LogInformation("Set ProjectId = '{Value}'", value);
                            break;
                        case "project name":
                            projectData.ProjectName = value;
                            _logger.LogInformation("Set ProjectName = '{Value}'", value);
                            break;
                        case "job address":
                            projectData.ProjectAddress = value;
                            _logger.LogInformation("Set ProjectAddress = '{Value}'", value);
                            break;
                        case "gc":
                            projectData.GeneralContractor = value;
                            _logger.LogInformation("Set GeneralContractor = '{Value}'", value);
                            break;
                        case "job contact":
                            projectData.ProjectContact = value;
                            _logger.LogInformation("Set ProjectContact = '{Value}'", value);
                            break;
                        case "job contact phone":
                            projectData.ProjectContactPhone = value;
                            _logger.LogInformation("Set ProjectContactPhone = '{Value}'", value);
                            break;
                        case "job contact email":
                            projectData.ProjectContactEmail = value;
                            _logger.LogInformation("Set ProjectContactEmail = '{Value}'", value);
                            break;
                        case "project manager":
                            projectData.ProjectManager = value;
                            _logger.LogInformation("Set ProjectManager = '{Value}'", value);
                            break;
                        case "installer":
                            projectData.Installer = value;
                            _logger.LogInformation("Set Installer = '{Value}'", value);
                            break;
                        case "target install date":
                            if (DateTime.TryParse(value, out var installDate))
                            {
                                projectData.TargetInstallDate = installDate;
                                _logger.LogInformation("Set TargetInstallDate = '{Value}'", installDate);
                            }
                            break;
                    }
                }
            }

            // Step 3: Create project in database
            currentStep++;
            await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), "Creating project in database...");
            
            // Create project with data from SmartSheet Summary
            var project = new Models.Project
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectData.ProjectId,
                ProjectName = projectData.ProjectName,
                ProjectManager = projectData.ProjectManager,
                ProjectContact = projectData.ProjectContact,
                ProjectContactPhone = projectData.ProjectContactPhone,
                ProjectContactEmail = projectData.ProjectContactEmail,
                ProjectAddress = projectData.ProjectAddress,
                GeneralContractor = projectData.GeneralContractor,
                Installer = projectData.Installer,
                TargetInstallDate = projectData.TargetInstallDate,
                ProjectCategory = projectData.ProjectCategory,
                CreatedDate = DateTime.Now,
                SmartSheetId = sheetId
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Step 4: Import attachments 
            currentStep++;
            if (sheetDetails.Attachments.Any())
            {
                await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), $"Importing {sheetDetails.Attachments.Count} attachments...");
                var userEmail = GetCurrentUserEmail() ?? "Smartsheet Migration";
                await ImportAttachmentsAsync(project.Id, sheetDetails.Attachments, userEmail);
            }
            else
            {
                await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), "No attachments to import");
            }

            // Step 5: Import comments
            currentStep++;
            if (sheetDetails.Comments.Any())
            {
                await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), $"Importing {sheetDetails.Comments.Count} comments...");
                await ImportCommentsAsync(project.Id, sheetDetails.Comments);
            }
            else
            {
                await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), "No comments to import");
            }

            // Step 6: Auto-group events by SmartSheet row number
            currentStep++;
            await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), "Auto-grouping events by SmartSheet row...");
            await AutoGroupEventsByRowAsync(project.Id);

            // Step 7: Finalize import
            currentStep++;
            await SendProgressUpdate(importId, CalculateProgress(currentStep, totalSteps), "Finalizing project import...");

            // Step 8: Complete
            currentStep++;
            await SendProgressUpdate(importId, 100, "Import completed successfully!");

            return new ImportResult
            {
                Success = true,
                Message = $"Successfully imported project '{projectData.ProjectName}' with {sheetDetails.Attachments.Count} attachments and {sheetDetails.Comments.Count} comments",
                ProjectId = project.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing project from sheet {SheetId}", sheetId);
            return new ImportResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private async Task ImportAttachmentsAsync(string projectId, List<AttachmentInfo> attachments, string userEmail)
    {
        _logger.LogInformation("Starting import of {AttachmentCount} attachments for project {ProjectId}", attachments.Count, projectId);
        
        var httpClient = new HttpClient();
        var smartsheet = GetSmartSheetClient();
        if (smartsheet == null) 
        {
            _logger.LogWarning("No SmartSheet client available for attachment import");
            return;
        }

        var successfulImports = 0;
        
        foreach (var attachment in attachments)
        {
            try
            {
                _logger.LogInformation("Processing attachment: {AttachmentName}, URL: {HasUrl}", 
                    attachment.Name, !string.IsNullOrEmpty(attachment.Url));
                
                if (string.IsNullOrEmpty(attachment.Url))
                {
                    _logger.LogWarning("Skipping attachment {AttachmentName} - no URL available", attachment.Name);
                    continue;
                }

                // Download attachment content
                var fileBytes = await httpClient.GetByteArrayAsync(attachment.Url);
                _logger.LogInformation("Downloaded {FileSize} bytes for {AttachmentName}", fileBytes.Length, attachment.Name);
                
                // Use ProjectAttachmentService to save with correct SmartSheet attribution
                await _attachmentService.SaveAttachmentAsync(
                    projectId,
                    attachment.Name,
                    fileBytes,
                    attachment.MimeType ?? "application/octet-stream",
                    "Label",
                    attachment.CreatedBy ?? "Smartsheet Import", // Use Smartsheet author, not import user
                    attachment.CreatedAt,
                    $"Imported from Smartsheet row {attachment.RowNumber}",
                    attachment.RowNumber); // Pass the row number for auto-grouping
                
                // ProjectAttachmentService already creates timeline events, so no need to create manually
                successfulImports++;
                
                _logger.LogInformation("Successfully imported attachment: {FileName} for project {ProjectId}", 
                    attachment.Name, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing attachment {AttachmentName}: {ErrorMessage}", 
                    attachment.Name, ex.Message);
            }
        }
        
        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully saved {SuccessfulImports} of {TotalAttachments} attachment events to database", 
            successfulImports, attachments.Count);
    }

    private async Task ImportCommentsAsync(string projectId, List<CommentInfo> comments)
    {
        _logger.LogInformation("Importing {CommentCount} comments for project {ProjectId}", comments.Count, projectId);
        
        foreach (var comment in comments)
        {
            try
            {
                var projectEvent = new Models.ProjectEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    ProjectId = projectId,
                    EventDate = comment.CreatedAt ?? DateTime.UtcNow,
                    EventType = "comment",
                    Description = comment.Text ?? "",
                    CreatedBy = comment.CreatedBy ?? "SmartSheet Import",
                    RowNumber = comment.RowNumber
                };

                _context.ProjectEvents.Add(projectEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing comment for project {ProjectId}", projectId);
            }
        }
        
        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully saved {CommentCount} comment events to database", comments.Count);
    }

    /// <summary>
    /// Send progress update via SignalR to the frontend
    /// </summary>
    private async Task SendProgressUpdate(string importId, int percentage, string message)
    {
        try
        {
            await _hubContext.Clients.Group($"import-{importId}")
                .SendAsync("ProgressUpdate", new { 
                    percentage = percentage, 
                    message = message,
                    timestamp = DateTime.UtcNow
                });
            
            _logger.LogInformation("Import {ImportId} progress: {Percentage}% - {Message}", importId, percentage, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send progress update for import {ImportId}", importId);
        }
    }

    /// <summary>
    /// Calculate progress percentage based on current and total steps
    /// </summary>
    private static int CalculateProgress(int currentStep, int totalSteps)
    {
        if (totalSteps <= 0) return 0;
        
        // Calculate percentage, ensuring it doesn't exceed 95% until completion
        var percentage = (int)Math.Floor((double)currentStep / totalSteps * 90);
        return Math.Min(percentage, 95);
    }

    /// <summary>
    /// Auto-group events by SmartSheet row number into TaskBlocks
    /// </summary>
    private async Task AutoGroupEventsByRowAsync(string projectId)
    {
        try
        {
            _logger.LogInformation("Starting auto-grouping of events by row number for project {ProjectId}", projectId);

            // Get all events with row numbers that aren't already assigned to blocks
            var eventsWithRows = await _context.ProjectEvents
                .Where(pe => pe.ProjectId == projectId && pe.RowNumber.HasValue && pe.ParentBlockId == null)
                .OrderBy(pe => pe.RowNumber)
                .ThenBy(pe => pe.EventDate)
                .ToListAsync();

            if (!eventsWithRows.Any())
            {
                _logger.LogInformation("No unassigned events with row numbers found for project {ProjectId}", projectId);
                return;
            }

            // Group events by row number
            var eventsByRow = eventsWithRows.GroupBy(pe => pe.RowNumber!.Value).ToList();
            _logger.LogInformation("Found {RowCount} unique rows with {EventCount} total events", 
                eventsByRow.Count, eventsWithRows.Count);

            var createdBlocks = 0;
            foreach (var rowGroup in eventsByRow)
            {
                var rowNumber = rowGroup.Key;
                var rowEvents = rowGroup.ToList();

                // Only create a block if there are events for this row
                if (rowEvents.Any())
                {
                    // Create TaskBlock for this row
                    var taskBlock = await _timelineService.CreateTaskBlockAsync(
                        projectId, 
                        $"Row {rowNumber}", 
                        null // No description
                    );

                    // Assign all events from this row to the block
                    var eventIds = rowEvents.Select(e => e.Id).ToList();
                    await _timelineService.AssignEventsToBlockAsync(taskBlock.Id, eventIds);

                    createdBlocks++;
                    _logger.LogInformation("Created TaskBlock '{BlockName}' with {EventCount} events for row {RowNumber}", 
                        taskBlock.Name, rowEvents.Count, rowNumber);
                }
            }

            _logger.LogInformation("Auto-grouping complete: created {BlockCount} TaskBlocks for project {ProjectId}", 
                createdBlocks, projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-grouping of events by row for project {ProjectId}", projectId);
            // Don't rethrow - this is a nice-to-have feature that shouldn't break the import
        }
    }

    /// <summary>
    /// Get sheet summary fields with their IDs and current values
    /// </summary>
    public async Task<Dictionary<string, long>> GetSheetSummaryFieldsAsync(long sheetId)
    {
        try
        {
            var client = await GetSmartSheetClientAsync();
            if (client == null)
            {
                _logger.LogWarning("SmartSheet client not available for getting summary fields");
                return new Dictionary<string, long>();
            }

            var summary = await Task.Run(() => client.SheetResources.SummaryResources.GetSheetSummary(sheetId, null, null));
            var fieldMap = new Dictionary<string, long>();
            
            if (summary?.Fields != null)
            {
                foreach (var field in summary.Fields)
                {
                    if (!string.IsNullOrEmpty(field.Title) && field.Id.HasValue)
                    {
                        fieldMap[field.Title] = field.Id.Value;
                    }
                }
                
                _logger.LogInformation("Retrieved {FieldCount} summary fields from sheet {SheetId}: [{FieldNames}]", 
                    fieldMap.Count, sheetId, string.Join(", ", fieldMap.Keys.Select(k => $"\"{k}\"")));
            }
            
            return fieldMap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting summary fields for sheet {SheetId}", sheetId);
            return new Dictionary<string, long>();
        }
    }


    /// <summary>
    /// Update sheet summary fields with project data
    /// </summary>
    public async Task<bool> UpdateSheetSummaryFieldsAsync(long sheetId, Dictionary<string, object?> fieldUpdates)
    {
        try
        {
            var client = await GetSmartSheetClientAsync();
            if (client == null)
            {
                _logger.LogWarning("SmartSheet client not available for updating summary fields");
                return false;
            }

            // Get existing summary fields to map names to IDs
            var fieldMap = await GetSheetSummaryFieldsAsync(sheetId);
            if (!fieldMap.Any())
            {
                _logger.LogWarning("No summary fields found for sheet {SheetId}", sheetId);
                return false;
            }

            var summaryFieldsToUpdate = new List<SummaryField>();
            int successCount = 0;
            int skippedCount = 0;

            foreach (var update in fieldUpdates)
            {
                var fieldName = update.Key;
                var fieldValue = update.Value;

                if (!fieldMap.TryGetValue(fieldName, out var fieldId))
                {
                    skippedCount++;
                    _logger.LogWarning("Summary field '{FieldName}' not found in sheet {SheetId}, skipping. Available fields: [{AvailableFields}]", 
                        fieldName, sheetId, string.Join(", ", fieldMap.Keys.Select(k => $"\"{k}\"")));
                    continue;
                }

                if (fieldValue == null)
                {
                    skippedCount++;
                    _logger.LogDebug("Value for summary field '{FieldName}' is null, skipping", fieldName);
                    continue;
                }

                var summaryField = new SummaryField();
                summaryField.Id = fieldId;

                // Set the appropriate ObjectValue based on the type
                if (fieldValue is string stringValue)
                {
                    summaryField.ObjectValue = new StringObjectValue(stringValue);
                }
                else if (fieldValue is DateTime dateValue)
                {
                    summaryField.ObjectValue = new DateObjectValue(ObjectValueType.DATE, dateValue.ToString("yyyy-MM-dd"));
                }
                else if (fieldValue is bool boolValue)
                {
                    summaryField.ObjectValue = new BooleanObjectValue(boolValue);
                }
                else if (fieldValue is int intValue)
                {
                    summaryField.ObjectValue = new NumberObjectValue(intValue);
                }
                else if (fieldValue is decimal decimalValue)
                {
                    summaryField.ObjectValue = new NumberObjectValue((double)decimalValue);
                }
                else
                {
                    // Convert other types to string
                    summaryField.ObjectValue = new StringObjectValue(fieldValue.ToString() ?? "");
                }

                summaryFieldsToUpdate.Add(summaryField);
                successCount++;
            }

            if (!summaryFieldsToUpdate.Any())
            {
                _logger.LogInformation("No valid summary fields to update for sheet {SheetId}", sheetId);
                return true;
            }

            _logger.LogInformation("Attempting to update {Count} summary fields on sheet {SheetId}: [{FieldsToUpdate}]", 
                summaryFieldsToUpdate.Count, sheetId, 
                string.Join(", ", summaryFieldsToUpdate.Select(f => $"ID:{f.Id}")));

            // Update the fields using the working method
            var result = await Task.Run(() => client.SheetResources.SummaryResources
                .UpdateSheetSummaryFields(sheetId, summaryFieldsToUpdate));

            _logger.LogInformation("Summary field update completed for sheet {SheetId}: {SuccessCount} updated, {SkippedCount} skipped", 
                sheetId, successCount, skippedCount);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating summary fields for sheet {SheetId}", sheetId);
            return false;
        }
    }
}

