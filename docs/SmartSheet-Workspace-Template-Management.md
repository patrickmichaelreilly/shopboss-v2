# SmartSheet Workspace & Template Management

## Overview

SmartSheet workspaces provide organizational structure for sheets, dashboards, reports, and templates. Understanding workspace operations and template management is essential for ShopBoss project automation.

## Workspace Structure (2025)

### Workspace Hierarchy
```
Workspace (e.g., "Active Projects")
├── Sheets (individual project sheets)
├── Reports (aggregated data views)
├── Dashboards (visual summaries)
├── Templates (project templates)
└── Folders (sub-organization)
```

### Key Characteristics
- **Permissions**: Inherit sharing permissions and branding
- **Organization**: Keep related project items grouped
- **Automation**: Apply consistent workflows across projects
- **Templates**: Store reusable project structures

## Workspace API Operations

### 1. List All Workspaces

```csharp
public async Task<List<Workspace>> GetWorkspacesAsync()
{
    var workspaces = await _smartsheet.WorkspaceResources.ListWorkspaces(
        new PaginationParameters(includeAll: true, pageSize: null, page: null)
    );
    return workspaces.Data.ToList();
}
```

```javascript
// JavaScript SDK
const workspaces = await smartsheet.workspaces.listWorkspaces({
    includeAll: true
});
```

### 2. Get Workspace Details

```csharp
public async Task<Workspace> GetWorkspaceWithContentsAsync(long workspaceId)
{
    var workspace = await _smartsheet.WorkspaceResources.GetWorkspace(
        workspaceId, 
        loadAll: true,  // Include all nested content
        include: null   // Can specify specific includes
    );
    return workspace;
}
```

### 3. Create New Workspace

```csharp
public async Task<Workspace> CreateWorkspaceAsync(string name)
{
    var workspace = new Workspace
    {
        Name = name,
        AccessLevel = AccessLevel.OWNER
    };
    
    var result = await _smartsheet.WorkspaceResources.CreateWorkspace(workspace);
    return result.Result;
}
```

## Template Management

### 1. Copy Workspace (Template Approach)

```csharp
public async Task<Workspace> CreateProjectFromWorkspaceTemplate(
    long templateWorkspaceId, 
    string projectName,
    string jobNumber)
{
    var copyWorkspace = new ContainerDestination
    {
        DestinationType = DestinationType.WORKSPACE,
        NewName = $"{jobNumber} - {projectName}"
    };

    // Include all elements in the copy
    var includes = new List<WorkspaceCopyInclusion>
    {
        WorkspaceCopyInclusion.DATA,
        WorkspaceCopyInclusion.ATTACHMENTS,
        WorkspaceCopyInclusion.DISCUSSIONS,
        WorkspaceCopyInclusion.CELLLINKS,
        WorkspaceCopyInclusion.FORMS,
        WorkspaceCopyInclusion.RULES,
        WorkspaceCopyInclusion.RULERECIPIENTS,
        WorkspaceCopyInclusion.SHARES
    };

    var result = await _smartsheet.WorkspaceResources.CopyWorkspace(
        templateWorkspaceId,
        copyWorkspace,
        includes
    );

    return result.Result;
}
```

### 2. Create Sheet from Template in Workspace

```csharp
public async Task<Sheet> CreateSheetFromTemplate(
    long workspaceId,
    long templateId,
    string sheetName,
    Project project)
{
    var sheet = new Sheet
    {
        Name = $"{project.ProjectId} - {sheetName}",
        FromId = templateId  // Template sheet ID
    };

    // Include essential elements from template
    var includes = new List<SheetCopyInclusion>
    {
        SheetCopyInclusion.DATA,
        SheetCopyInclusion.ATTACHMENTS,
        SheetCopyInclusion.DISCUSSIONS,
        SheetCopyInclusion.FORMS,
        SheetCopyInclusion.RULES
    };

    var result = await _smartsheet.WorkspaceResources.SheetResources
        .CreateSheetInWorkspace(workspaceId, sheet, includes);

    return result.Result;
}
```

### 3. Copy Inclusion Options (2025)

#### Available Include Parameters
```csharp
public enum TemplateIncludeOptions
{
    DATA,           // Cell values and formatting
    ATTACHMENTS,    // File attachments
    DISCUSSIONS,    // Comments and discussions  
    CELLLINKS,      // Cell linking relationships
    FORMS,          // SmartSheet forms
    RULES,          // Automation workflows
    RULERECIPIENTS, // Automation notification recipients
    SHARES,         // Sharing permissions
    FILTERS         // Column filters
}
```

#### ⚠️ Important Notes (2025)
- **Cell History**: Not copied regardless of include parameters
- **Data Formatting**: Requires `DATA` inclusion for populated cells
- **Empty Sheets**: Without `DATA`, only structure is copied
- **Link Remapping**: Available when copying within same workspace

## ShopBoss Integration Patterns

### 1. Project Workspace Creation Service

```csharp
public class SmartSheetProjectWorkspaceService
{
    private readonly SmartsheetClient _smartsheet;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmartSheetProjectWorkspaceService> _logger;

    public async Task<ProjectWorkspaceResult> CreateProjectWorkspaceAsync(
        Project project, 
        ProjectCategory category)
    {
        try
        {
            // Select appropriate template based on category
            var templateId = GetTemplateIdForCategory(category);
            
            // Create project workspace from template
            var workspace = await CreateProjectFromWorkspaceTemplate(
                templateId,
                project.ProjectName,
                project.ProjectId
            );

            // Update project with SmartSheet references
            project.SmartSheetWorkspaceId = workspace.Id?.ToString();
            project.SmartSheetId = FindPrimarySheetId(workspace);

            // Customize workspace for project
            await CustomizeWorkspaceForProject(workspace, project);

            return new ProjectWorkspaceResult
            {
                Success = true,
                Workspace = workspace,
                PrimarySheetId = project.SmartSheetId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workspace for project {ProjectId}", 
                project.ProjectId);
            return new ProjectWorkspaceResult { Success = false, Error = ex.Message };
        }
    }

    private long GetTemplateIdForCategory(ProjectCategory category)
    {
        return category switch
        {
            ProjectCategory.StandardProducts => 
                _configuration.GetValue<long>("SmartSheet:Templates:StandardProject"),
            ProjectCategory.CustomProducts => 
                _configuration.GetValue<long>("SmartSheet:Templates:CustomProject"),
            ProjectCategory.SmallProject => 
                _configuration.GetValue<long>("SmartSheet:Templates:SmallProject"),
            _ => _configuration.GetValue<long>("SmartSheet:Templates:DefaultProject")
        };
    }
}
```

### 2. Master List Integration

```csharp
public async Task UpdateMasterProjectListAsync(Project project, long sheetId)
{
    var masterListId = _configuration.GetValue<long>("SmartSheet:MasterProjectListId");
    var masterSheet = await _smartsheet.SheetResources.GetSheet(masterListId);
    
    // Create new row for project
    var row = new Row
    {
        ToTop = true,
        Cells = CreateMasterListCells(project, sheetId, masterSheet.Columns)
    };

    var rowsToAdd = new Row[] { row };
    await _smartsheet.SheetResources.RowResources.AddRows(masterListId, rowsToAdd);
}

private List<Cell> CreateMasterListCells(Project project, long sheetId, IList<Column> columns)
{
    var cells = new List<Cell>();
    
    foreach (var column in columns)
    {
        var cell = new Cell { ColumnId = column.Id };
        
        switch (column.Title?.ToLower())
        {
            case "project id":
            case "job number":
                cell.Value = project.ProjectId;
                break;
            case "project name":
                cell.Value = project.ProjectName;
                break;
            case "target install date":
                cell.Value = project.TargetInstallDate;
                break;
            case "project manager":
                cell.Value = project.ProjectManager;
                break;
            case "sheet id":
                cell.Value = sheetId;
                break;
            case "shopboss link":
                cell.Value = $"https://shopboss.com/Project/Details/{project.Id}";
                break;
        }
        
        cells.Add(cell);
    }
    
    return cells;
}
```

### 3. Template Configuration

```json
{
  "SmartSheet": {
    "Templates": {
      "StandardProject": 1234567890123456,
      "CustomProject": 2345678901234567,
      "SmallProject": 3456789012345678,
      "DefaultProject": 1234567890123456
    },
    "MasterProjectListId": 9876543210987654,
    "ActiveProjectsWorkspaceId": 8765432109876543
  }
}
```

## Workspace Management Best Practices

### 1. Naming Conventions

```csharp
public string GenerateWorkspaceName(Project project)
{
    return $"{project.ProjectId} - {project.ProjectName}";
}

public string GenerateSheetName(Project project, string sheetType)
{
    return $"{project.ProjectId} - {sheetType}";
}
```

### 2. Permission Management

```csharp
public async Task SetupWorkspacePermissions(long workspaceId, Project project)
{
    var shares = new List<Share>();
    
    // Add project manager
    if (!string.IsNullOrEmpty(project.ProjectManager))
    {
        shares.Add(new Share
        {
            Email = GetProjectManagerEmail(project.ProjectManager),
            AccessLevel = AccessLevel.EDITOR,
            Subject = $"Project {project.ProjectId} - {project.ProjectName}"
        });
    }
    
    // Add general contractor if specified
    if (!string.IsNullOrEmpty(project.GeneralContractor))
    {
        shares.Add(new Share
        {
            Email = GetContractorEmail(project.GeneralContractor),
            AccessLevel = AccessLevel.VIEWER,
            Subject = $"Project {project.ProjectId} - View Access"
        });
    }

    if (shares.Any())
    {
        await _smartsheet.WorkspaceResources.ShareResources
            .ShareWorkspace(workspaceId, shares, true);
    }
}
```

### 3. Cleanup and Archiving

```csharp
public async Task ArchiveProjectWorkspace(Project project)
{
    if (string.IsNullOrEmpty(project.SmartSheetWorkspaceId)) return;

    var workspaceId = long.Parse(project.SmartSheetWorkspaceId);
    var archiveWorkspaceId = _configuration.GetValue<long>("SmartSheet:ArchivedProjectsWorkspaceId");
    
    // Move workspace to archive
    var destination = new ContainerDestination
    {
        DestinationType = DestinationType.WORKSPACE,
        DestinationId = archiveWorkspaceId
    };

    await _smartsheet.WorkspaceResources.MoveWorkspace(workspaceId, destination);
    
    // Update project status
    project.IsArchived = true;
    project.ArchivedDate = DateTime.UtcNow;
}
```

## Error Handling and Edge Cases

### 1. Template Not Found

```csharp
public async Task<bool> ValidateTemplateExists(long templateId)
{
    try
    {
        await _smartsheet.WorkspaceResources.GetWorkspace(templateId);
        return true;
    }
    catch (ResourceNotFoundException)
    {
        return false;
    }
}
```

### 2. Workspace Name Conflicts

```csharp
public async Task<string> GenerateUniqueWorkspaceName(string baseName)
{
    var workspaces = await GetWorkspacesAsync();
    var existingNames = workspaces.Select(w => w.Name).ToHashSet();
    
    var uniqueName = baseName;
    var counter = 1;
    
    while (existingNames.Contains(uniqueName))
    {
        uniqueName = $"{baseName} ({counter++})";
    }
    
    return uniqueName;
}
```

This comprehensive workspace and template management approach enables ShopBoss to automatically create structured SmartSheet workspaces for new projects while maintaining consistency and organization.