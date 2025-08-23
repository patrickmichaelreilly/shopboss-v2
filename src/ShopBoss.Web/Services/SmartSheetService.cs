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

    public SmartSheetService(
        ShopBossDbContext context,
        ILogger<SmartSheetService> logger,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Get SmartSheet client using session token (session-based OAuth)
    /// </summary>
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

            _logger.LogInformation("Successfully linked project {ProjectId} ({ProjectName}) to SmartSheet {SheetId} ({SheetName})", 
                projectId, project.ProjectName, sheetId, sheetInfo.Name);

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