# SmartSheet Migration Tool - MVP

## Overview
Build a lightweight, manual validation tool for exploring SmartSheet data and importing projects one at a time into ShopBoss. This is an isolated exploration tool that will not affect any existing ShopBoss functionality.

## MVP Approach
- **Manual Process**: User reviews and approves all data before import
- **Simple UI**: The interface itself IS the validation system
- **Minimal Error Handling**: Basic try/catch only - user handles edge cases
- **No Automation**: No retry logic, progress tracking, or complex error recovery
- **Completely Isolated**: Zero changes to existing ShopBoss code

## Primary Goals
1. View all data available through SmartSheet SDK
2. Display project timelines reconstructed from comments/attachments
3. Allow user to manually review and correct data before import
4. Test attachment download and storage process

## Required New Components

### 1. ProjectEvent Model
```csharp
public class ProjectEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; }
    public DateTime EventDate { get; set; }
    public string EventType { get; set; } // "comment", "attachment", "status_change"
    public string Description { get; set; }
    public string CreatedBy { get; set; }
    public Project Project { get; set; }
}
```

## Implementation Structure

### 1. SmartSheetMigrationController
Simple controller with no authorization needed.

```csharp
public class SmartSheetMigrationController : Controller
{
    private readonly SmartSheetImportService _service;
    
    public async Task<IActionResult> Index()
    {
        try 
        {
            var workspaces = await _service.GetWorkspacesAsync();
            return View(workspaces);
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Failed to load workspaces: {ex.Message}";
            return View();
        }
    }
}
```

### 2. Service Extensions
Extend existing SmartSheetImportService with minimal new methods:

```csharp
// Add these methods to SmartSheetImportService:

public async Task<WorkspaceListResult> GetWorkspacesAsync()
{
    // List sheets from "Active Jobs" and "_Archived Jobs" workspaces
    // Return simple object with two lists
}

public async Task<SheetDetailsResult> GetSheetDetailsAsync(long sheetId)
{
    // Fetch all available data:
    // - Sheet Summary fields
    // - Attachments list
    // - Comments/discussions
    // Return everything for UI display
}

public async Task<ImportResult> ImportProjectAsync(ImportRequest request)
{
    // Create Project entity
    // Create ProjectEvent entities from timeline
    // Download attachments if requested
    // Save everything in a transaction
    // Return success/failure with project ID
}
```

### 3. View Layout: `/Views/SmartSheetMigration/Index.cshtml`

```html
@model SmartSheetMigrationViewModel

<div class="container-fluid">
    <h2>SmartSheet Project Migration Tool</h2>
    
    <!-- Section 1: Sheet Selection -->
    <div class="card mb-3">
        <div class="card-header">
            <h4>1. Select Source Sheet</h4>
        </div>
        <div class="card-body">
            <div class="row">
                <div class="col-md-6">
                    <h5>Active Jobs</h5>
                    <div id="activeJobsList" class="list-group" style="max-height: 300px; overflow-y: auto;">
                        <!-- Populate with sheets from Active Jobs workspace -->
                    </div>
                </div>
                <div class="col-md-6">
                    <h5>Archived Jobs</h5>
                    <div id="archivedJobsList" class="list-group" style="max-height: 300px; overflow-y: auto;">
                        <!-- Populate with sheets from _Archived Jobs workspace -->
                    </div>
                </div>
            </div>
            <button class="btn btn-primary mt-3" onclick="loadSheetDetails()">
                Load Selected Sheet
            </button>
        </div>
    </div>
    
    <!-- Section 2: Sheet Analysis -->
    <div class="card mb-3" id="sheetAnalysis" style="display: none;">
        <div class="card-header">
            <h4>2. Sheet Analysis</h4>
        </div>
        <div class="card-body">
            <div class="row">
                <div class="col-md-4">
                    <h5>Sheet Summary</h5>
                    <div id="sheetSummary">
                        <!-- Display Sheet Summary fields -->
                        <!-- Highlight Job ID field -->
                    </div>
                </div>
                <div class="col-md-4">
                    <h5>Attachments (<span id="attachmentCount">0</span>)</h5>
                    <div id="attachmentList" style="max-height: 200px; overflow-y: auto;">
                        <!-- List all attachments with sizes -->
                    </div>
                </div>
                <div class="col-md-4">
                    <h5>Comments/History (<span id="commentCount">0</span>)</h5>
                    <div id="commentList" style="max-height: 200px; overflow-y: auto;">
                        <!-- Show comment timeline -->
                    </div>
                </div>
            </div>
            
            <!-- Timeline Reconstruction Preview -->
            <div class="mt-3">
                <h5>Reconstructed Timeline</h5>
                <div id="timelinePreview" class="timeline-container">
                    <!-- Show interpreted events from comments/changes -->
                </div>
            </div>
        </div>
    </div>
    
    <!-- Section 3: Project Creation Form -->
    <div class="card mb-3" id="projectCreation" style="display: none;">
        <div class="card-header">
            <h4>3. Create ShopBoss Project</h4>
        </div>
        <div class="card-body">
            <!-- Reuse existing _ProjectForm partial but with pre-filled data -->
            <div id="projectFormContainer">
                @await Html.PartialAsync("_ProjectForm", new Project())
            </div>
            
            <!-- Migration-specific options -->
            <div class="mt-3 p-3 bg-light">
                <h5>Migration Options</h5>
                <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="downloadAttachments" checked>
                    <label class="form-check-label" for="downloadAttachments">
                        Download and attach all files
                    </label>
                </div>
                <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="createTimeline" checked>
                    <label class="form-check-label" for="createTimeline">
                        Create timeline events from comments
                    </label>
                </div>
                <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="linkMasterList" checked>
                    <label class="form-check-label" for="linkMasterList">
                        Link to Master Project List entry
                    </label>
                </div>
            </div>
            
            <button class="btn btn-success mt-3" onclick="importProject()">
                Import Project to ShopBoss
            </button>
        </div>
    </div>
    
    <!-- Section 4: Import Results -->
    <div class="card" id="importResults" style="display: none;">
        <div class="card-header">
            <h4>4. Import Results</h4>
        </div>
        <div class="card-body">
            <div id="resultsContent">
                <!-- Show what was imported successfully -->
                <!-- List any errors or warnings -->
                <!-- Provide links to view in ShopBoss -->
            </div>
        </div>
    </div>
</div>
```

### 4. JavaScript Support: `smartsheet-migration.js`

```javascript
let selectedSheetId = null;
let sheetDetails = null;
let preparedProjectData = null;

async function loadWorkspaces() {
    const response = await fetch('/SmartSheetMigration/GetWorkspaces');
    const data = await response.json();
    
    // Populate Active Jobs list
    data.activeJobs.forEach(sheet => {
        $('#activeJobsList').append(
            `<a href="#" class="list-group-item list-group-item-action" 
                data-sheet-id="${sheet.id}" onclick="selectSheet(${sheet.id}, '${sheet.name}')">
                ${sheet.name}
                <small class="text-muted d-block">Modified: ${sheet.modifiedAt}</small>
            </a>`
        );
    });
    
    // Populate Archived Jobs list
    data.archivedJobs.forEach(sheet => {
        $('#archivedJobsList').append(
            `<a href="#" class="list-group-item list-group-item-action" 
                data-sheet-id="${sheet.id}" onclick="selectSheet(${sheet.id}, '${sheet.name}')">
                ${sheet.name}
                <small class="text-muted d-block">Modified: ${sheet.modifiedAt}</small>
            </a>`
        );
    });
}

async function loadSheetDetails() {
    if (!selectedSheetId) {
        alert('Please select a sheet first');
        return;
    }
    
    // Show loading state
    $('#sheetAnalysis').show();
    
    const response = await fetch(`/SmartSheetMigration/GetSheetDetails?sheetId=${selectedSheetId}`);
    sheetDetails = await response.json();
    
    // Display Sheet Summary
    displaySheetSummary(sheetDetails.summary);
    
    // Display Attachments
    displayAttachments(sheetDetails.attachments);
    
    // Display Comments
    displayComments(sheetDetails.comments);
    
    // Build Timeline
    buildTimeline(sheetDetails);
    
    // Prepare Project Form
    prepareProjectForm();
}

function buildTimeline(details) {
    const events = [];
    
    // Extract events from comments
    details.comments.forEach(comment => {
        events.push({
            date: comment.createdAt,
            type: 'comment',
            description: comment.text,
            user: comment.createdBy
        });
    });
    
    // Extract events from attachments
    details.attachments.forEach(attachment => {
        events.push({
            date: attachment.createdAt,
            type: 'attachment',
            description: `File uploaded: ${attachment.name}`,
            user: attachment.attachedBy
        });
    });
    
    // Sort by date and display
    events.sort((a, b) => new Date(a.date) - new Date(b.date));
    
    const timeline = $('#timelinePreview');
    timeline.empty();
    events.forEach(event => {
        timeline.append(`
            <div class="timeline-event">
                <span class="badge bg-${event.type === 'comment' ? 'info' : 'secondary'}">${event.type}</span>
                <small>${event.date}</small>
                <p>${event.description}</p>
                <small class="text-muted">by ${event.user}</small>
            </div>
        `);
    });
}

async function importProject() {
    const projectData = {
        ...preparedProjectData,
        downloadAttachments: $('#downloadAttachments').is(':checked'),
        createTimeline: $('#createTimeline').is(':checked'),
        linkMasterList: $('#linkMasterList').is(':checked')
    };
    
    const response = await fetch('/SmartSheetMigration/ImportProject', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(projectData)
    });
    
    const result = await response.json();
    
    // Show results
    $('#importResults').show();
    $('#resultsContent').html(`
        <div class="alert alert-${result.success ? 'success' : 'danger'}">
            ${result.message}
        </div>
        ${result.projectId ? `
            <a href="/Project/Details/${result.projectId}" class="btn btn-primary">
                View Imported Project
            </a>
        ` : ''}
    `);
}
```

### 5. Key Data Mappings

```csharp
// SmartSheet → ShopBoss Project
public class SheetToProjectMapper
{
    public Project MapSheetToProject(SheetDetailedInfo sheet)
    {
        return new Project
        {
            // From Sheet Summary
            ProjectId = sheet.Summary["Job ID"],
            ProjectName = sheet.Name,
            CustomerName = sheet.Summary["Customer"],
            ProjectManager = sheet.Summary["PM"],
            
            // From Master List (via Job ID lookup)
            Address = masterListEntry.Address,
            InstallDate = masterListEntry.InstallDate,
            ContractAmount = masterListEntry.ContractAmount,
            
            // Generated from timeline
            CreatedDate = sheet.CreatedAt,
            LastModifiedDate = sheet.ModifiedAt,
            
            // New timeline events from comments/attachments
            ProjectEvents = BuildEventsFromHistory(sheet)
        };
    }
}
```

### 6. UI Flow

1. **Select Sheet**: User browses workspace lists and selects a sheet
2. **View Data**: System displays ALL available data from SmartSheet
3. **Manual Review**: User reviews and corrects any issues in the form
4. **Import**: User clicks import, system creates Project + ProjectEvents
5. **Result**: Success/failure message with link to view imported project

## What This Tool Does NOT Do

- ❌ No automatic data correction or validation
- ❌ No progress tracking or complex UI updates  
- ❌ No authorization or access control
- ❌ No retry logic or error recovery
- ❌ No duplicate detection (existing ShopBoss validation handles this)
- ❌ No modification of existing ShopBoss code

## File Structure

```
/Controllers/SmartSheetMigrationController.cs  (new)
/Models/ProjectEvent.cs                        (new)  
/Views/SmartSheetMigration/Index.cshtml       (new)
/wwwroot/js/smartsheet-migration.js           (new)
/Services/SmartSheetImportService.cs          (extend existing)
```

## Success Criteria

- View all SmartSheet data in the UI
- Successfully import a project with timeline events
- Download and attach files from SmartSheet
- No changes to existing ShopBoss functionality