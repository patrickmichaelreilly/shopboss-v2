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

    public SmartSheetImportService(
        ShopBossDbContext context,
        ILogger<SmartSheetImportService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
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

    public async Task<List<SmartSheetInfo>> ListAvailableSheetsAsync()
    {
        var sheets = new List<SmartSheetInfo>();
        
        try
        {
            var accessToken = _configuration["SmartSheet:AccessToken"];
            if (string.IsNullOrEmpty(accessToken))
                return sheets;

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

        return sheets;
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