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

## 2025 Updates & Embedding Patterns

### Embedding via iframe
```html
<!-- Published sheet embedding -->
<iframe 
  src="https://publish.smartsheet.com/[sheet-id]" 
  width="100%" 
  height="600" 
  frameborder="0">
</iframe>
```

### Webhook Integration (2025)
```csharp
// Creating webhooks for real-time sync
var webhook = new Webhook
{
    Name = "ShopBoss Project Sync",
    CallbackUrl = "https://shopboss.domain.com/api/smartsheet/webhook",
    Scope = "sheet",
    ScopeObjectId = sheetId,
    Events = new[] { "*.*" }, // All events
    Version = 1
};

var result = await smartsheet.WebhookResources.CreateWebhook(webhook);
```

### Multi-Layer Caching Pattern
```csharp
// L1: Memory cache for frequently accessed metadata
_memoryCache.Set($"sheet_meta_{sheetId}", metadata, TimeSpan.FromMinutes(15));

// L2: Distributed cache for larger shared data
await _distributedCache.SetAsync($"sheet_data_{sheetId}", serializedData, 
    new DistributedCacheEntryOptions 
    { 
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) 
    });
```

### Rate Limiting with Exponential Backoff
```csharp
public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
{
    var attempt = 0;
    var baseDelay = TimeSpan.FromSeconds(2);
    var maxDelay = TimeSpan.FromMinutes(5);
    
    while (attempt < 5)
    {
        try
        {
            return await operation();
        }
        catch (RateLimitException) when (attempt < 4)
        {
            var delay = TimeSpan.FromMilliseconds(
                baseDelay.TotalMilliseconds * Math.Pow(2, attempt) +
                Random.Shared.Next(0, 1000)); // Add jitter
                
            await Task.Delay(delay > maxDelay ? maxDelay : delay);
            attempt++;
        }
    }
    
    throw new InvalidOperationException("Max retries exceeded");
}
```

## Configuration

### appsettings.json
```json
{
  "SmartSheet": {
    "AccessToken": "your-api-token-here",
    "BaseUrl": "https://api.smartsheet.com/2.0/",
    "Templates": {
      "StandardProject": 1234567890123456,
      "CustomProject": 2345678901234567
    },
    "MasterProjectListId": 9876543210987654,
    "Cache": {
      "DefaultExpiration": "01:00:00",
      "MemoryCacheSize": 104857600
    }
  }
}
```

### Required NuGet Packages
```xml
<PackageReference Include="smartsheet-csharp-sdk" Version="[latest]" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
```

## Performance & Rate Limits (2025)
- **Rate Limits**: Enforced but not publicly documented (varies by plan)
- **Bulk Operations**: Count as single request (up to 500 rows recommended)
- **Debouncing**: 1-minute debounce on webhooks since October 2024
- **IP Migration**: New webhook domain `webhooks.smartsheet.com` (legacy IPs sunset Sept 2025)
- **Sheet Limits**: Webhooks disabled on sheets >20K rows, >400 columns, >500K cells

## Integration Architecture Patterns

### Hybrid Integration (Recommended)
- **API SDK**: Data synchronization and operations
- **iframe Embedding**: User interaction with sheets
- **Custom UI**: Enhanced ShopBoss-specific functionality
- **Webhook Sync**: Real-time updates

### Graceful Degradation
```csharp
try 
{
    var enhancedData = await _smartSheetService.GetProjectDataAsync(id);
    project.SmartSheetData = enhancedData;
    project.IntegrationStatus = "Available";
}
catch (SmartSheetException)
{
    project.IntegrationStatus = "Degraded";
    // Core ShopBoss functionality continues
}
```

## Testing Notes
- **Development**: Use ngrok for webhook testing
- **Authentication**: Validate tokens before operations  
- **Fallback**: Test behavior when SmartSheet unavailable
- **Rate Limits**: Implement proper retry logic with backoff
- **Caching**: Verify cache invalidation on webhook events