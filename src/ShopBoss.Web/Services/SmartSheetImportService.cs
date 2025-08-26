using Smartsheet.Api;
using Smartsheet.Api.Models;
using ShopBoss.Web.Models;
using ShopBoss.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ShopBoss.Web.Services;

public class SmartSheetImportService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SmartSheetImportService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ProjectAttachmentService _attachmentService;

    public SmartSheetImportService(
        ShopBossDbContext context,
        ILogger<SmartSheetImportService> logger,
        IConfiguration configuration,
        ProjectAttachmentService attachmentService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _attachmentService = attachmentService;
    }

    public async Task<SmartSheetImportResult> ImportProjectsFromMasterListAsync()
    {
        var result = new SmartSheetImportResult();
        
        try
        {
            // Get access token from configuration
            var accessToken = _configuration["SmartSheet:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
            {
                result.Success = false;
                result.ErrorMessage = "SmartSheet access token not configured";
                return result;
            }

            // Initialize SmartSheet client
            var smartsheet = new SmartsheetBuilder()
                .SetAccessToken(accessToken)
                .Build();

            // Get all sheets and find Master Project List
            var allSheets = new List<Smartsheet.Api.Models.Sheet>();
            var paginationParams = new Smartsheet.Api.Models.PaginationParameters(false, 100, 1);
            
            Smartsheet.Api.Models.PaginatedResult<Smartsheet.Api.Models.Sheet> sheetListResult;
            do
            {
                sheetListResult = smartsheet.SheetResources.ListSheets(null, paginationParams, null);
                allSheets.AddRange(sheetListResult.Data);
                paginationParams.Page++;
            } while (sheetListResult.TotalPages >= paginationParams.Page);
            
            var masterSheet = allSheets.FirstOrDefault(s => s.Name == "_Master Project List");
            if (masterSheet == null)
            {
                result.Success = false;
                result.ErrorMessage = "Could not find '_Master Project List' sheet";
                return result;
            }

            var sheet = smartsheet.SheetResources.GetSheet(masterSheet.Id!.Value, null, null, null, null, null, null, null);
            
            if (sheet == null)
            {
                result.Success = false;
                result.ErrorMessage = "Sheet not found";
                return result;
            }

            _logger.LogInformation("Successfully retrieved sheet '{SheetName}' with {RowCount} rows", sheet.Name, sheet.Rows?.Count ?? 0);

            // Create column mapping (SmartSheet column title -> Project property)
            var columnMapping = CreateColumnMapping(sheet.Columns);
            
            if (sheet.Rows == null || !sheet.Rows.Any())
            {
                result.Success = false;
                result.ErrorMessage = "No rows found in sheet";
                return result;
            }

            // Import each row as a project
            foreach (var row in sheet.Rows)
            {
                try
                {
                    var project = MapRowToProject(row, columnMapping);
                    
                    // Check if project already exists by ProjectId
                    var existingProject = await _context.Projects
                        .FirstOrDefaultAsync(p => p.ProjectId == project.ProjectId);
                    
                    if (existingProject == null)
                    {
                        _context.Projects.Add(project);
                        result.ProjectsCreated++;
                        _logger.LogDebug("Created new project: {ProjectId} - {ProjectName}", project.ProjectId, project.ProjectName);
                    }
                    else
                    {
                        result.ProjectsSkipped++;
                        _logger.LogDebug("Skipped existing project: {ProjectId}", project.ProjectId);
                    }
                }
                catch (Exception ex)
                {
                    result.ProjectsWithErrors++;
                    result.ErrorDetails.Add($"Row error: {ex.Message}");
                    _logger.LogWarning(ex, "Error processing row");
                }
            }

            // Save all changes
            await _context.SaveChangesAsync();
            
            result.Success = true;
            result.SheetName = sheet.Name;
            result.TotalRowsProcessed = sheet.Rows.Count;
            
            _logger.LogInformation("SmartSheet import completed. Created: {Created}, Skipped: {Skipped}, Errors: {Errors}", 
                result.ProjectsCreated, result.ProjectsSkipped, result.ProjectsWithErrors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SmartSheet import");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private Dictionary<long, string> CreateColumnMapping(IList<Column> columns)
    {
        var mapping = new Dictionary<long, string>();
        
        foreach (var column in columns)
        {
            if (column.Id.HasValue && !string.IsNullOrEmpty(column.Title))
            {
                mapping[column.Id.Value] = column.Title;
            }
        }
        
        return mapping;
    }

    private Project MapRowToProject(Row row, Dictionary<long, string> columnMapping)
    {
        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            CreatedDate = DateTime.UtcNow,
            IsArchived = false
        };

        // Map each cell to project properties based on exact column titles from CSV
        foreach (var cell in row.Cells)
        {
            if (!cell.ColumnId.HasValue || !columnMapping.ContainsKey(cell.ColumnId.Value))
                continue;

            var columnTitle = columnMapping[cell.ColumnId.Value].Trim();
            var value = cell.Value?.ToString();

            if (string.IsNullOrWhiteSpace(value))
                continue;

            // Exact mapping based on Master Project List CSV columns
            switch (columnTitle)
            {
                case "Project ID":
                    project.ProjectId = value;
                    break;
                    
                case "Status":
                    // Status values: "Yes" = active (not archived), "No" = inactive (archived), null = archived
                    if (value == "Yes")
                        project.IsArchived = false;
                    else // "No" or null
                        project.IsArchived = true;
                    break;
                    
                case "Project Name":
                    project.ProjectName = value;
                    break;
                    
                case "Job Address":
                    project.ProjectAddress = value;
                    break;
                    
                case "Job Contact":
                    project.ProjectContact = value;
                    break;
                    
                case "Job Contact Phone":
                    project.ProjectContactPhone = value;
                    break;
                    
                case "Job Contact Email":
                    project.ProjectContactEmail = value;
                    break;
                    
                case "GC":
                    project.GeneralContractor = value;
                    break;
                    
                case "Project Manager":
                    project.ProjectManager = value;
                    break;
                    
                case "Installer":
                    project.Installer = value;
                    break;
                    
                case "Notes":
                    project.Notes = value;
                    break;
                    
                case "Bid Request Date":
                    if (DateTime.TryParse(value, out var bidDate))
                        project.BidRequestDate = bidDate;
                    break;
                    
                case "Target Install Date":
                    if (DateTime.TryParse(value, out var installDate))
                        project.TargetInstallDate = installDate;
                    break;
                    
                case "Project Category":
                    // Map SmartSheet categories to ProjectCategory enum
                    switch (value.ToLower().Trim())
                    {
                        case "standard products":
                        case "standard":
                            project.ProjectCategory = ProjectCategory.StandardProducts;
                            break;
                        case "custom products":
                        case "custom":
                            project.ProjectCategory = ProjectCategory.CustomProducts;
                            break;
                        case "small project":
                        case "small":
                            project.ProjectCategory = ProjectCategory.SmallProject;
                            break;
                        default:
                            project.ProjectCategory = ProjectCategory.StandardProducts; // Default
                            break;
                    }
                    break;
                    
                // Skip boolean columns and other fields we don't need:
                // Stone Tops, Shop Drawings, Drawing Approval, Material Submittals, 
                // Undermount Sinks, Field Measure, In-Wall Supports, CNC, Finishing, 
                // Solid Surface, Custom, Knock Up, Delivery, Punch Items, PLAM Colors, 
                // Submission Date, Submitter, Allocation %, Duration, Predecessors
            }
        }

        // Ensure required fields have values
        if (string.IsNullOrEmpty(project.ProjectId))
            project.ProjectId = $"SS-{Guid.NewGuid().ToString()[..8]}";
        
        if (string.IsNullOrEmpty(project.ProjectName))
            project.ProjectName = "Imported from SmartSheet";

        return project;
    }

    public Task<List<SmartSheetInfo>> ListAvailableSheetsAsync()
    {
        var sheets = new List<SmartSheetInfo>();
        
        try
        {
            var accessToken = _configuration["SmartSheet:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
                return Task.FromResult(sheets);

            var smartsheet = new SmartsheetBuilder()
                .SetAccessToken(accessToken)
                .Build();

            var sheetList = smartsheet.SheetResources.ListSheets(null, null, null);
            
            foreach (var sheet in sheetList.Data)
            {
                sheets.Add(new SmartSheetInfo
                {
                    Id = sheet.Id ?? 0,
                    Name = sheet.Name ?? "Unknown",
                    ModifiedAt = sheet.ModifiedAt,
                    CreatedAt = sheet.CreatedAt
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing SmartSheet sheets");
        }

        return Task.FromResult(sheets);
    }

    // New methods for SmartSheet Migration Tool
    public Task<WorkspaceListResult> GetWorkspacesAsync()
    {
        var result = new WorkspaceListResult();
        
        try
        {
            var accessToken = _configuration["SmartSheet:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("SmartSheet access token not configured");
            }

            var smartsheet = new SmartsheetBuilder()
                .SetAccessToken(accessToken)
                .Build();

            // Get all workspaces
            var workspaces = smartsheet.WorkspaceResources.ListWorkspaces(new PaginationParameters(true, null, null));
            
            foreach (var workspace in workspaces.Data)
            {
                // Get sheets in this workspace
                var workspaceDetails = smartsheet.WorkspaceResources.GetWorkspace(workspace.Id ?? 0, null, null);
                
                if (workspaceDetails.Sheets != null)
                {
                    var sheetInfos = workspaceDetails.Sheets.Select(s => new SheetInfo
                    {
                        Id = s.Id ?? 0,
                        Name = s.Name ?? "",
                        ModifiedAt = s.ModifiedAt
                    }).ToList();

                    if (workspace.Name == "Active Jobs")
                    {
                        result.ActiveJobs = sheetInfos;
                    }
                    else if (workspace.Name == "_Archived Jobs")
                    {
                        result.ArchivedJobs = sheetInfos;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workspaces");
            throw;
        }

        return Task.FromResult(result);
    }

    public Task<SheetDetailsResult> GetSheetDetailsAsync(long sheetId)
    {
        _logger.LogInformation("GetSheetDetailsAsync called for sheet {SheetId}", sheetId);
        var result = new SheetDetailsResult();
        
        try
        {
            var accessToken = _configuration["SmartSheet:AccessToken"];
            _logger.LogInformation("Access token configured: {HasToken}", !string.IsNullOrEmpty(accessToken));
            
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new InvalidOperationException("SmartSheet access token not configured");
            }
            
            var smartsheet = new SmartsheetBuilder()
                .SetAccessToken(accessToken)
                .Build();

            _logger.LogInformation("Built SmartSheet client, calling GetSheet for {SheetId}", sheetId);

            // Get basic sheet info first
            var sheet = smartsheet.SheetResources.GetSheet(sheetId, null, null, null, null, null, null, null);

            _logger.LogInformation("Successfully retrieved basic sheet '{SheetName}'", sheet.Name);

            result.SheetName = sheet.Name ?? "";
            result.SheetId = sheetId;

            // Get sheet with rows to map row numbers for attachments and comments
            Dictionary<long, int> rowIdToNumberMap = new Dictionary<long, int>();
            try
            {
                var sheetWithRows = smartsheet.SheetResources.GetSheet(sheetId, null, null, null, null, null, null, null);
                if (sheetWithRows?.Rows != null)
                {
                    for (int i = 0; i < sheetWithRows.Rows.Count; i++)
                    {
                        var row = sheetWithRows.Rows[i];
                        if (row.Id.HasValue)
                        {
                            rowIdToNumberMap[row.Id.Value] = row.RowNumber ?? (i + 1);
                        }
                    }
                    _logger.LogInformation("Mapped {RowCount} row IDs to row numbers", rowIdToNumberMap.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get row mapping for sheet {SheetId}", sheetId);
            }

            // Get sheet summary
            try
            {
                _logger.LogInformation("Getting sheet summary for sheet {SheetId}", sheetId);
                var summary = smartsheet.SheetResources.SummaryResources.GetSheetSummary(sheetId, null, null);
                
                result.Summary = new Dictionary<string, string>();
                if (summary?.Fields != null)
                {
                    _logger.LogInformation("Found {FieldCount} summary fields", summary.Fields.Count);
                    
                    foreach (var field in summary.Fields)
                    {
                        var key = field.Title ?? "Unknown Field";
                        var value = "";
                        
                        if (field.ObjectValue != null)
                        {
                            // Try to extract the actual value from the ObjectValue
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
                        _logger.LogDebug("Summary field: {Key} = {Value} (Type: {Type})", key, value, field.ObjectValue?.GetType().Name ?? "null");
                    }
                }
                else
                {
                    _logger.LogInformation("No summary fields found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get sheet summary for {SheetId}: {ErrorMessage}", sheetId, ex.Message);
                result.Summary = new Dictionary<string, string>();
            }

            // Get attachments using separate API call
            try
            {
                _logger.LogInformation("Getting attachments for sheet {SheetId}", sheetId);
                var attachmentResult = smartsheet.SheetResources.AttachmentResources.ListAttachments(sheetId, new PaginationParameters(true, null, null));
                
                if (attachmentResult?.Data != null)
                {
                    _logger.LogInformation("Found {AttachmentCount} attachments", attachmentResult.Data.Count);
                    result.Attachments = attachmentResult.Data.Select(a => {
                        var attachmentInfo = new AttachmentInfo
                        {
                            Id = a.Id ?? 0,
                            Name = a.Name ?? "",
                            SizeInKb = (a.SizeInKb ?? 0) / 1024.0, // Convert to MB
                            CreatedAt = a.CreatedAt,
                            AttachedBy = a.CreatedBy?.Email ?? "Unknown",
                            AttachmentType = a.AttachmentType?.ToString() ?? "UNKNOWN"
                        };
                        
                        // Set row information if available
                        if (a.ParentId.HasValue && rowIdToNumberMap.ContainsKey(a.ParentId.Value))
                        {
                            attachmentInfo.RowId = a.ParentId.Value;
                            attachmentInfo.RowNumber = rowIdToNumberMap[a.ParentId.Value];
                        }
                        
                        return attachmentInfo;
                    }).ToList();
                }
                else
                {
                    result.Attachments = new List<AttachmentInfo>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get attachments for sheet {SheetId}", sheetId);
                result.Attachments = new List<AttachmentInfo>();
            }

            // Get discussions using separate API call
            try
            {
                _logger.LogInformation("Getting discussions for sheet {SheetId}", sheetId);
                var discussionResult = smartsheet.SheetResources.DiscussionResources.ListDiscussions(sheetId, null, new PaginationParameters(true, null, null));
                
                result.Comments = new List<CommentInfo>();
                if (discussionResult?.Data != null)
                {
                    _logger.LogInformation("Found {DiscussionCount} discussions", discussionResult.Data.Count);
                    
                    foreach (var discussion in discussionResult.Data)
                    {
                        // Get full discussion details including comments
                        try
                        {
                            var fullDiscussion = smartsheet.SheetResources.DiscussionResources.GetDiscussion(sheetId, discussion.Id ?? 0);
                            if (fullDiscussion?.Comments != null)
                            {
                                result.Comments.AddRange(fullDiscussion.Comments.Select(c => {
                                    var commentInfo = new CommentInfo
                                    {
                                        Id = c.Id ?? 0,
                                        Text = c.Text ?? "",
                                        CreatedAt = c.CreatedAt,
                                        CreatedBy = c.CreatedBy?.Email ?? "Unknown",
                                        DiscussionTitle = fullDiscussion.Title ?? ""
                                    };
                                    
                                    // Set row information if discussion is attached to a row
                                    if (fullDiscussion.ParentId.HasValue && rowIdToNumberMap.ContainsKey(fullDiscussion.ParentId.Value))
                                    {
                                        commentInfo.RowId = fullDiscussion.ParentId.Value;
                                        commentInfo.RowNumber = rowIdToNumberMap[fullDiscussion.ParentId.Value];
                                    }
                                    
                                    return commentInfo;
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get discussion details for discussion {DiscussionId}", discussion.Id);
                        }
                    }
                    
                    _logger.LogInformation("Total comments extracted: {CommentCount}", result.Comments.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get discussions for sheet {SheetId}", sheetId);
                result.Comments = new List<CommentInfo>();
            }

            _logger.LogInformation("Returning sheet details for {SheetId}: {AttachmentCount} attachments, {CommentCount} comments", 
                sheetId, result.Attachments.Count, result.Comments.Count);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sheet details for {SheetId}: {ErrorMessage}", sheetId, ex.Message);
            throw;
        }
    }

    public async Task<ImportResult> ImportProjectAsync(ImportProjectRequest request)
    {
        var result = new ImportResult();
        
        try
        {
            // Create the project
            var project = new Project
            {
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
                CreatedDate = DateTime.UtcNow
            };

            _context.Projects.Add(project);

            // Create timeline events if requested
            if (request.CreateTimeline)
            {
                var sheetDetails = await GetSheetDetailsAsync(request.SheetId);
                
                // Add events from comments
                foreach (var comment in sheetDetails.Comments ?? new List<CommentInfo>())
                {
                    var evt = new ProjectEvent
                    {
                        ProjectId = project.Id,
                        EventDate = comment.CreatedAt ?? DateTime.UtcNow,
                        EventType = "comment",
                        Description = comment.Text,
                        CreatedBy = comment.CreatedBy,
                        RowNumber = comment.RowNumber
                    };
                    _context.ProjectEvents.Add(evt);
                }

                // Note: Attachment events will be created during the download phase
                // to ensure proper linking with ProjectAttachment records
            }

            // Download attachments if requested
            if (request.DownloadAttachments && request.SheetId > 0)
            {
                try
                {
                    await DownloadAttachmentsForProject(request.SheetId, project.Id, request.CreateTimeline);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download attachments for project {ProjectId}", project.Id);
                    // Don't fail the import if attachments fail
                }
            }

            await _context.SaveChangesAsync();
            
            result.Success = true;
            result.ProjectId = project.Id;
            result.Message = $"Successfully imported project {project.ProjectName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing project");
            result.Success = false;
            result.Message = ex.Message;
        }

        return result;
    }

    private async Task DownloadAttachmentsForProject(long sheetId, string projectId, bool createTimelineEvents = true)
    {
        try
        {
            var accessToken = _configuration["SmartSheet:AccessToken"];
            var smartsheet = new SmartsheetBuilder()
                .SetAccessToken(accessToken ?? "")
                .Build();

            var attachments = smartsheet.SheetResources.AttachmentResources.ListAttachments(sheetId, new PaginationParameters(true, null, null));

            foreach (var attachment in attachments.Data)
            {
                try
                {
                    var attachmentDetails = smartsheet.SheetResources.AttachmentResources.GetAttachment(sheetId, attachment.Id ?? 0);
                    
                    if (!string.IsNullOrEmpty(attachmentDetails.Url))
                    {
                        using var httpClient = new HttpClient();
                        var fileBytes = await httpClient.GetByteArrayAsync(attachmentDetails.Url);
                        
                        var originalFileName = attachment.Name ?? "unknown_file";
                        var uploadedBy = attachment.CreatedBy?.Email ?? "SmartSheet Migration";
                        var uploadDate = attachment.CreatedAt ?? DateTime.UtcNow;

                        // Use shared attachment service for consistent file storage
                        await _attachmentService.SaveAttachmentAsync(
                            projectId,
                            originalFileName,
                            fileBytes,
                            "application/octet-stream", // SmartSheet doesn't provide detailed MIME types
                            "SmartSheet", // Category to identify SmartSheet imports
                            uploadedBy,
                            uploadDate);
                        
                        _logger.LogInformation("Downloaded and saved SmartSheet attachment: {FileName} for project {ProjectId}", 
                            originalFileName, projectId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download attachment {AttachmentName}", attachment.Name ?? "unknown");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading attachments for sheet {SheetId}", sheetId);
            throw;
        }
    }
}

public class SmartSheetImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SheetName { get; set; }
    public int TotalRowsProcessed { get; set; }
    public int ProjectsCreated { get; set; }
    public int ProjectsSkipped { get; set; }
    public int ProjectsWithErrors { get; set; }
    public List<string> ErrorDetails { get; set; } = new();
}

public class SmartSheetInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}

// New models for SmartSheet Migration Tool
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
    public double SizeInKb { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string AttachedBy { get; set; } = string.Empty;
    public long? RowId { get; set; }
    public int? RowNumber { get; set; }
    public string AttachmentType { get; set; } = string.Empty; // "SHEET", "ROW", "COMMENT"
}

public class CommentInfo
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public long? RowId { get; set; }
    public int? RowNumber { get; set; }
    public string DiscussionTitle { get; set; } = string.Empty;
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
    public bool DownloadAttachments { get; set; } = true;
    public bool CreateTimeline { get; set; } = true;
}