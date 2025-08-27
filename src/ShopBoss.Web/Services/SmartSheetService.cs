using Smartsheet.Api;
using Smartsheet.Api.Models;
using ShopBoss.Web.Models;
using ShopBoss.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ShopBoss.Web.Services;

public class SmartSheetService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SmartSheetService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ProjectAttachmentService _attachmentService;

    public SmartSheetService(
        ShopBossDbContext context,
        ILogger<SmartSheetService> logger,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ProjectAttachmentService attachmentService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _attachmentService = attachmentService;
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
    public async Task<ImportResult> ImportProjectAsync(ImportProjectRequest request)
    {
        try
        {
            var smartsheet = GetSmartSheetClient();
            if (smartsheet == null)
            {
                return new ImportResult 
                { 
                    Success = false, 
                    Message = "No SmartSheet session. Please authenticate first." 
                };
            }

            // Get sheet details
            var sheetDetails = await GetSheetDetailsAsync(request.SheetId);

            // Auto-populate request fields from SmartSheet Summary data if not already provided
            if (sheetDetails.Summary.Any())
            {
                _logger.LogInformation("Auto-populating project fields from {SummaryCount} summary fields", sheetDetails.Summary.Count);
                
                foreach (var summaryField in sheetDetails.Summary)
                {
                    var key = summaryField.Key?.Trim() ?? "";
                    var value = summaryField.Value?.Trim() ?? "";
                    
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    
                    _logger.LogInformation("Processing summary field: '{Key}' = '{Value}'", key, value);
                    
                    // Auto-populate fields if they weren't provided in the request
                    switch (key.ToLower())
                    {
                        case "project id" when string.IsNullOrWhiteSpace(request.ProjectId):
                            request.ProjectId = value;
                            _logger.LogInformation("Set ProjectId = '{Value}'", value);
                            break;
                        case "project name" when string.IsNullOrWhiteSpace(request.ProjectName):
                            request.ProjectName = value;
                            _logger.LogInformation("Set ProjectName = '{Value}'", value);
                            break;
                        case "job address" when string.IsNullOrWhiteSpace(request.ProjectAddress):
                            request.ProjectAddress = value;
                            _logger.LogInformation("Set ProjectAddress = '{Value}'", value);
                            break;
                        case "gc" when string.IsNullOrWhiteSpace(request.GeneralContractor):
                            request.GeneralContractor = value;
                            _logger.LogInformation("Set GeneralContractor = '{Value}'", value);
                            break;
                        case "job contact" when string.IsNullOrWhiteSpace(request.ProjectContact):
                            request.ProjectContact = value;
                            _logger.LogInformation("Set ProjectContact = '{Value}'", value);
                            break;
                        case "job contact phone" when string.IsNullOrWhiteSpace(request.ProjectContactPhone):
                            request.ProjectContactPhone = value;
                            _logger.LogInformation("Set ProjectContactPhone = '{Value}'", value);
                            break;
                        case "job contact email" when string.IsNullOrWhiteSpace(request.ProjectContactEmail):
                            request.ProjectContactEmail = value;
                            _logger.LogInformation("Set ProjectContactEmail = '{Value}'", value);
                            break;
                        case "project manager" when string.IsNullOrWhiteSpace(request.ProjectManager):
                            request.ProjectManager = value;
                            _logger.LogInformation("Set ProjectManager = '{Value}'", value);
                            break;
                        case "installer" when string.IsNullOrWhiteSpace(request.Installer):
                            request.Installer = value;
                            _logger.LogInformation("Set Installer = '{Value}'", value);
                            break;
                        case "target install date" when !request.TargetInstallDate.HasValue:
                            if (DateTime.TryParse(value, out var installDate))
                            {
                                request.TargetInstallDate = installDate;
                                _logger.LogInformation("Set TargetInstallDate = '{Value}'", installDate);
                            }
                            break;
                    }
                }
            }

            // Create project with data from the request (now populated with Summary data)
            var project = new Models.Project
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                ProjectName = request.ProjectName,
                ProjectManager = request.ProjectManager,
                ProjectContact = request.ProjectContact,
                ProjectContactPhone = request.ProjectContactPhone,
                ProjectContactEmail = request.ProjectContactEmail,
                ProjectAddress = request.ProjectAddress,
                GeneralContractor = request.GeneralContractor,
                Installer = request.Installer,
                TargetInstallDate = request.TargetInstallDate,
                ProjectCategory = request.ProjectCategory,
                CreatedDate = DateTime.Now,
                SmartSheetId = request.SheetId
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            // Import attachments if requested
            if (request.ImportAttachments && sheetDetails.Attachments.Any())
            {
                var userEmail = GetCurrentUserEmail() ?? "SmartSheet Migration";
                await ImportAttachmentsAsync(project.Id, sheetDetails.Attachments, userEmail);
            }

            // Import comments if requested
            if (request.ImportComments && sheetDetails.Comments.Any())
            {
                await ImportCommentsAsync(project.Id, sheetDetails.Comments);
            }

            return new ImportResult
            {
                Success = true,
                Message = $"Successfully imported project '{request.ProjectName}' with {sheetDetails.Attachments.Count} attachments and {sheetDetails.Comments.Count} comments",
                ProjectId = project.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing project from sheet {SheetId}", request.SheetId);
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
                    "SmartSheet Import",
                    attachment.CreatedBy ?? "SmartSheet Import", // Use SmartSheet author, not import user
                    attachment.CreatedAt,
                    $"Imported from SmartSheet row {attachment.RowNumber}");
                
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
}

/// <summary>
/// SmartSheet information for display in UI
/// </summary>
public class SmartSheetLinkInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Permalink { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public int RowCount { get; set; }
}

/// <summary>
/// Response model for SmartSheet token API
/// </summary>
public class SmartSheetTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }
}

// Migration Tool Models
public class WorkspaceListResult
{
    public List<SheetInfo> ActiveJobs { get; set; } = new();
    public List<SheetInfo> ArchivedJobs { get; set; } = new();
}

public class SheetInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
}

public class SheetDetailsResult
{
    public long SheetId { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public Dictionary<string, string> Summary { get; set; } = new();
    public List<AttachmentInfo> Attachments { get; set; } = new();
    public List<CommentInfo> Comments { get; set; } = new();
}

public class AttachmentInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? SizeInKb { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public int? RowNumber { get; set; }
    public string? AttachmentType { get; set; }
    public string? MimeType { get; set; }
    public string? Url { get; set; }
}

public class CommentInfo
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public int? RowNumber { get; set; }
}

public class ImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
}

public class ImportProjectRequest
{
    public long SheetId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectManager { get; set; }
    public string? ProjectContact { get; set; }
    public string? ProjectContactPhone { get; set; }
    public string? ProjectContactEmail { get; set; }
    public string? ProjectAddress { get; set; }
    public string? GeneralContractor { get; set; }
    public string? Installer { get; set; }
    public DateTime? TargetInstallDate { get; set; }
    public ProjectCategory ProjectCategory { get; set; }
    public bool ImportAttachments { get; set; } = true;
    public bool ImportComments { get; set; } = true;
}