# SmartSheet API Cheatsheet

## Quick Reference for SmartSheet .NET SDK

### Basic Setup
```csharp
var smartsheet = new SmartsheetBuilder()
    .SetAccessToken(accessToken)
    .Build();
```

## Core API Patterns

### 1. Workspaces & Sheets

#### List All Workspaces
```csharp
var workspaces = smartsheet.WorkspaceResources.ListWorkspaces(
    new PaginationParameters(includeAll: true, pageSize: null, page: null)
);
```

#### Get Workspace Details (with sheets)
```csharp
var workspaceDetails = smartsheet.WorkspaceResources.GetWorkspace(
    workspaceId, 
    loadAll: null, 
    include: null
);
// Access: workspaceDetails.Sheets
```

#### List All Sheets (across all workspaces)
```csharp
var sheets = smartsheet.SheetResources.ListSheets(
    new PaginationParameters(includeAll: true, pageSize: null, page: null),
    modifiedSince: null
);
```

### 2. Sheet Data

#### Get Basic Sheet Info
```csharp
var sheet = smartsheet.SheetResources.GetSheet(
    sheetId,
    includes: null,
    excludes: null,
    rowIds: null,
    rowNumbers: null,
    columnIds: null,
    pageSize: null,
    page: null
);
```

#### Get Sheet with Includes (⚠️ May not work as expected)
```csharp
// This approach had issues in our testing - use separate calls instead
var sheet = smartsheet.SheetResources.GetSheet(
    sheetId, 
    new SheetLevelInclusion[] { 
        SheetLevelInclusion.ATTACHMENTS,
        SheetLevelInclusion.DISCUSSIONS 
    },
    null, null, null, null, null, null
);
```

### 3. Attachments (✅ Recommended Approach)

#### List Sheet Attachments
```csharp
var attachments = smartsheet.SheetResources.AttachmentResources.ListAttachments(
    sheetId, 
    new PaginationParameters(includeAll: true, pageSize: null, page: null)
);

// Access: attachments.Data (List<Attachment>)
```

#### Get Attachment Details
```csharp
var attachment = smartsheet.SheetResources.AttachmentResources.GetAttachment(
    sheetId, 
    attachmentId
);
// Access: attachment.Url (for download)
```

#### Download Attachment
```csharp
var attachmentDetails = smartsheet.SheetResources.AttachmentResources.GetAttachment(sheetId, attachmentId);

if (!string.IsNullOrEmpty(attachmentDetails.Url))
{
    using var httpClient = new HttpClient();
    var fileBytes = await httpClient.GetByteArrayAsync(attachmentDetails.Url);
    await File.WriteAllBytesAsync(localPath, fileBytes);
}
```

### 4. Discussions & Comments (✅ Recommended Approach)

#### List Sheet Discussions
```csharp
var discussions = smartsheet.SheetResources.DiscussionResources.ListDiscussions(
    sheetId,
    include: null,
    new PaginationParameters(includeAll: true, pageSize: null, page: null)
);

// Access: discussions.Data (List<Discussion>)
```

#### Get Discussion Details (with comments)
```csharp
var fullDiscussion = smartsheet.SheetResources.DiscussionResources.GetDiscussion(
    sheetId, 
    discussionId
);
// Access: fullDiscussion.Comments (List<Comment>)
```

### 5. Sheet Summary Fields (✅ Fixed)

#### Get Sheet Summary
```csharp
var summary = smartsheet.SheetResources.SummaryResources.GetSheetSummary(
    sheetId, 
    include: null, 
    exclude: null
);
// Access: summary.Fields

// Extract field values (proper type casting):
foreach (var field in summary.Fields)
{
    var key = field.Title ?? "Unknown Field";
    var value = "";
    
    if (field.ObjectValue is StringObjectValue stringValue)
        value = stringValue.Value ?? "";
    else if (field.ObjectValue is BooleanObjectValue boolValue)
        value = boolValue.Value.ToString();
    else if (field.ObjectValue is NumberObjectValue numberValue)
        value = numberValue.Value.ToString();
    else if (field.ObjectValue is DateObjectValue dateValue)
        value = dateValue.Value.ToString();
    else if (field.DisplayValue != null)
        value = field.DisplayValue;
        
    // Use key/value pair
}
```

## Data Models

### Common Properties
```csharp
// Most SmartSheet objects have:
.Id          // long? (nullable)
.Name        // string?
.CreatedAt   // DateTime?
.ModifiedAt  // DateTime?
.CreatedBy   // User? (.Email, .Name)
```

### Attachment Properties
```csharp
attachment.Id           // long?
attachment.Name         // string?
attachment.SizeInKb     // long? (in KB, divide by 1024 for MB)
attachment.CreatedAt    // DateTime?
attachment.CreatedBy    // User?
attachment.Url          // string? (temporary download URL)
```

### Discussion/Comment Properties
```csharp
discussion.Id           // long?
discussion.Title        // string?
discussion.Comments     // List<Comment>?

comment.Id              // long?
comment.Text            // string?
comment.CreatedAt       // DateTime?
comment.CreatedBy       // User?
```

## Best Practices

### 1. Error Handling
```csharp
try 
{
    var result = smartsheet.SheetResources.GetSheet(sheetId);
}
catch (Smartsheet.Api.InvalidRequestException ex)
{
    // Handle specific SmartSheet API errors
    _logger.LogError(ex, "SmartSheet API error: {Message}", ex.Message);
}
catch (Exception ex)
{
    // Handle general errors
    _logger.LogError(ex, "Unexpected error accessing SmartSheet");
}
```

### 2. Null Safety
```csharp
// Always check for nulls - SmartSheet SDK uses nullable types extensively
var attachmentCount = sheet.Attachments?.Count ?? 0;
var attachmentName = attachment.Name ?? "unknown";
var attachmentId = attachment.Id ?? 0;
```

### 3. Pagination
```csharp
// Use includeAll: true for small datasets
new PaginationParameters(includeAll: true, pageSize: null, page: null)

// Use pagination for large datasets
new PaginationParameters(includeAll: false, pageSize: 100, page: 1)
```

### 4. Separate API Calls Strategy (✅ Recommended)
```csharp
// Instead of trying to get everything in one call, use separate calls:

// 1. Get basic sheet info
var sheet = smartsheet.SheetResources.GetSheet(sheetId);

// 2. Get attachments separately
var attachments = smartsheet.SheetResources.AttachmentResources.ListAttachments(sheetId, pagination);

// 3. Get discussions separately
var discussions = smartsheet.SheetResources.DiscussionResources.ListDiscussions(sheetId, null, pagination);

// 4. Get each discussion's comments
foreach (var discussion in discussions.Data)
{
    var fullDiscussion = smartsheet.SheetResources.DiscussionResources.GetDiscussion(sheetId, discussion.Id);
    // Process fullDiscussion.Comments
}
```

## Common Issues & Solutions

### ❌ Problem: GetSheet with includes returns empty attachments/discussions
**Solution:** Use separate API calls for attachments and discussions

### ❌ Problem: Null reference exceptions
**Solution:** Always use null-conditional operators (`?.`) and null coalescing (`??`)

### ❌ Problem: Missing comments in discussions
**Solution:** Call GetDiscussion() for each discussion to get full comment details

### ❌ Problem: Sheet summary fields not accessible
**Solution:** May require special API permissions or different authentication scope

## Configuration

### appsettings.json
```json
{
  "SmartSheet": {
    "AccessToken": "your-api-token-here"
  }
}
```

### Required NuGet Package
```xml
<PackageReference Include="smartsheet-csharp-sdk" Version="[latest]" />
```

## Rate Limits
- SmartSheet has API rate limits
- Consider adding retry logic with exponential backoff for production use
- Cache workspace/sheet listings when possible

## Testing Notes
- Test with sheets that have attachments and discussions
- Some sheets may appear empty if they don't have the expected content types
- API responses can vary based on user permissions and sheet configuration