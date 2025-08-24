# SmartSheet Write Operations Guide

## Overview

This guide provides comprehensive patterns for writing data to SmartSheet using the C# SDK with OAuth authentication. All examples assume session-based OAuth tokens as implemented in Phase 1.

## Basic Setup

### Authentication Pattern
```csharp
public class SmartSheetWriteService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SmartSheetWriteService> _logger;

    public SmartSheetWriteService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SmartSheetWriteService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
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
}
```

## Adding Rows

### Single Row Addition
```csharp
public async Task<long?> AddRowToSheetAsync(long sheetId, Dictionary<long, object> cellData)
{
    try
    {
        var smartsheet = GetSmartSheetClient();
        if (smartsheet == null)
        {
            throw new InvalidOperationException("No SmartSheet session found");
        }

        // Build cells from column ID -> value mapping
        var cells = cellData.Select(kvp => 
            new Cell.AddCellBuilder(kvp.Key, kvp.Value).Build()
        ).ToArray();

        // Create row
        var row = new Row.AddRowBuilder()
            .SetCells(cells)
            .Build();

        // Add row to sheet
        var result = await Task.Run(() => 
            smartsheet.SheetResources.RowResources.AddRows(sheetId, new Row[] { row })
        );

        _logger.LogInformation("Added row to SmartSheet {SheetId}, Row ID: {RowId}", 
            sheetId, result.Result[0].Id);

        return result.Result[0].Id;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error adding row to SmartSheet {SheetId}", sheetId);
        throw;
    }
}
```

### Bulk Row Addition (Recommended)
```csharp
public async Task<List<long>> AddMultipleRowsAsync(long sheetId, List<Dictionary<long, object>> rowsData)
{
    try
    {
        var smartsheet = GetSmartSheetClient();
        if (smartsheet == null)
        {
            throw new InvalidOperationException("No SmartSheet session found");
        }

        // Build rows
        var rows = rowsData.Select(rowData =>
        {
            var cells = rowData.Select(kvp => 
                new Cell.AddCellBuilder(kvp.Key, kvp.Value).Build()
            ).ToArray();

            return new Row.AddRowBuilder()
                .SetCells(cells)
                .Build();
        }).ToArray();

        // Add all rows in single request (more efficient)
        var result = await Task.Run(() => 
            smartsheet.SheetResources.RowResources.AddRows(sheetId, rows)
        );

        var rowIds = result.Result.Select(r => r.Id ?? 0).ToList();
        
        _logger.LogInformation("Added {Count} rows to SmartSheet {SheetId}", 
            rowIds.Count, sheetId);

        return rowIds;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error adding multiple rows to SmartSheet {SheetId}", sheetId);
        throw;
    }
}
```

## Updating Rows

### Single Cell Update
```csharp
public async Task<bool> UpdateCellAsync(long sheetId, long rowId, long columnId, object value)
{
    try
    {
        var smartsheet = GetSmartSheetClient();
        if (smartsheet == null)
        {
            throw new InvalidOperationException("No SmartSheet session found");
        }

        // Create cell update
        var cell = new Cell.UpdateCellBuilder(columnId, value).Build();
        
        // Create row update
        var row = new Row.UpdateRowBuilder(rowId)
            .SetCells(new Cell[] { cell })
            .Build();

        // Update row
        var result = await Task.Run(() => 
            smartsheet.SheetResources.RowResources.UpdateRows(sheetId, new Row[] { row })
        );

        _logger.LogInformation("Updated cell in SmartSheet {SheetId}, Row: {RowId}, Column: {ColumnId}", 
            sheetId, rowId, columnId);

        return result.Result != null && result.Result.Count > 0;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating cell in SmartSheet {SheetId}", sheetId);
        return false;
    }
}
```

### Bulk Row Updates (Recommended)
```csharp
public async Task<bool> UpdateMultipleRowsAsync(long sheetId, 
    Dictionary<long, Dictionary<long, object>> rowUpdates)
{
    try
    {
        var smartsheet = GetSmartSheetClient();
        if (smartsheet == null)
        {
            throw new InvalidOperationException("No SmartSheet session found");
        }

        // Build row updates
        var rows = rowUpdates.Select(rowUpdate =>
        {
            var cells = rowUpdate.Value.Select(cellUpdate => 
                new Cell.UpdateCellBuilder(cellUpdate.Key, cellUpdate.Value).Build()
            ).ToArray();

            return new Row.UpdateRowBuilder(rowUpdate.Key)
                .SetCells(cells)
                .Build();
        }).ToArray();

        // Update all rows in single request
        var result = await Task.Run(() => 
            smartsheet.SheetResources.RowResources.UpdateRows(sheetId, rows)
        );

        _logger.LogInformation("Updated {Count} rows in SmartSheet {SheetId}", 
            rows.Length, sheetId);

        return result.Result != null && result.Result.Count > 0;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating multiple rows in SmartSheet {SheetId}", sheetId);
        return false;
    }
}
```

## Adding Comments

### Add Discussion Comment
```csharp
public async Task<long?> AddCommentAsync(long sheetId, long rowId, string comment)
{
    try
    {
        var smartsheet = GetSmartSheetClient();
        if (smartsheet == null)
        {
            throw new InvalidOperationException("No SmartSheet session found");
        }

        // Create comment
        var newComment = new Comment.CreateCommentBuilder()
            .SetText(comment)
            .Build();

        // Create discussion
        var discussion = new Discussion.CreateDiscussionBuilder()
            .SetTitle($"ShopBoss Update")
            .SetComment(newComment)
            .Build();

        // Add discussion to row
        var result = await Task.Run(() => 
            smartsheet.SheetResources.RowResources.DiscussionResources
                .CreateDiscussion(sheetId, rowId, discussion)
        );

        _logger.LogInformation("Added comment to SmartSheet {SheetId}, Row: {RowId}", 
            sheetId, rowId);

        return result.Id;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error adding comment to SmartSheet {SheetId}", sheetId);
        return null;
    }
}
```

## Column Type Handling

### Data Type Mapping
```csharp
public object ConvertValueForColumn(object value, ColumnType columnType)
{
    switch (columnType)
    {
        case ColumnType.TEXT_NUMBER:
            return value?.ToString();
            
        case ColumnType.DATE:
            if (value is DateTime dateTime)
                return dateTime.ToString("yyyy-MM-dd");
            break;
            
        case ColumnType.DATETIME:
            if (value is DateTime dateTimeValue)
                return dateTimeValue.ToString("yyyy-MM-ddTHH:mm:ssZ");
            break;
            
        case ColumnType.CHECKBOX:
            if (value is bool boolValue)
                return boolValue;
            break;
            
        case ColumnType.PICKLIST:
            return value?.ToString();
            
        case ColumnType.CONTACT_LIST:
            // Requires Contact object
            if (value is string email)
                return new Contact { Email = email };
            break;
            
        case ColumnType.DURATION:
            // Duration in days
            if (value is double duration)
                return duration;
            break;
    }
    
    return value;
}
```

## Error Handling Patterns

### Retry Logic for Rate Limiting
```csharp
public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (IsRateLimitException(ex) && attempt < maxRetries)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
            _logger.LogWarning("Rate limit hit, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})", 
                delay.TotalSeconds, attempt, maxRetries);
            
            await Task.Delay(delay);
        }
    }
    
    // This line should never be reached, but compiler requires it
    throw new InvalidOperationException("All retry attempts failed");
}

private static bool IsRateLimitException(Exception ex)
{
    // Check for SmartSheet rate limit exception
    return ex.Message?.Contains("rate limit") == true ||
           ex.Message?.Contains("429") == true;
}
```

### Partial Success Handling
```csharp
public async Task<BulkOperationResult> AddRowsWithPartialSuccessAsync(
    long sheetId, List<Dictionary<long, object>> rowsData)
{
    try
    {
        var smartsheet = GetSmartSheetClient();
        if (smartsheet == null)
        {
            throw new InvalidOperationException("No SmartSheet session found");
        }

        // Build rows
        var rows = rowsData.Select(rowData =>
        {
            var cells = rowData.Select(kvp => 
                new Cell.AddCellBuilder(kvp.Key, kvp.Value).Build()
            ).ToArray();

            return new Row.AddRowBuilder()
                .SetCells(cells)
                .Build();
        }).ToArray();

        // Enable partial success
        var result = await Task.Run(() => 
            smartsheet.SheetResources.RowResources.AddRows(
                sheetId, rows, null, null, null, true) // allowPartialSuccess = true
        );

        var successCount = result.Result?.Count ?? 0;
        var failureCount = result.FailedItems?.Count ?? 0;

        _logger.LogInformation("Bulk add result: {Success} success, {Failed} failed", 
            successCount, failureCount);

        return new BulkOperationResult
        {
            SuccessCount = successCount,
            FailureCount = failureCount,
            SuccessfulRowIds = result.Result?.Select(r => r.Id ?? 0).ToList() ?? new List<long>(),
            FailedItems = result.FailedItems?.ToList() ?? new List<BulkItemFailure>()
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in bulk add operation for SmartSheet {SheetId}", sheetId);
        throw;
    }
}

public class BulkOperationResult
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<long> SuccessfulRowIds { get; set; } = new();
    public List<BulkItemFailure> FailedItems { get; set; } = new();
}
```

## Process Module Patterns

### Adding Process Module as Rows
```csharp
public async Task<List<long>> AddProcessModuleAsync(long sheetId, string processName, 
    List<string> steps, long? parentRowId = null)
{
    var rowsData = new List<Dictionary<long, object>>();
    
    // Assume columns: Task Name, Description, Status, Assigned To, Due Date
    long taskNameColumnId = 1; // Get from sheet metadata
    long descriptionColumnId = 2;
    long statusColumnId = 3;
    long assignedToColumnId = 4;
    long dueDateColumnId = 5;
    
    // Add header row for process module
    rowsData.Add(new Dictionary<long, object>
    {
        [taskNameColumnId] = $"🔧 {processName}",
        [descriptionColumnId] = "Process Module",
        [statusColumnId] = "Not Started"
    });
    
    // Add step rows
    foreach (var step in steps)
    {
        rowsData.Add(new Dictionary<long, object>
        {
            [taskNameColumnId] = $"  • {step}",
            [descriptionColumnId] = $"Step in {processName}",
            [statusColumnId] = "Not Started"
        });
    }
    
    return await AddMultipleRowsAsync(sheetId, rowsData);
}
```

## Usage Examples

### ShopBoss Integration Pattern
```csharp
public class ProjectSmartSheetService
{
    private readonly SmartSheetWriteService _writeService;
    
    public async Task<bool> UpdateProjectStatusAsync(string projectId, string newStatus)
    {
        // Get project and linked sheet
        var project = await GetProjectAsync(projectId);
        if (!project.SmartSheetId.HasValue) return false;
        
        // Find status column and project row
        var statusColumnId = await GetStatusColumnIdAsync(project.SmartSheetId.Value);
        var projectRowId = await FindProjectRowAsync(project.SmartSheetId.Value, project.Name);
        
        // Update status
        return await _writeService.UpdateCellAsync(
            project.SmartSheetId.Value, 
            projectRowId, 
            statusColumnId, 
            newStatus
        );
    }
    
    public async Task<bool> AddProjectMilestoneAsync(string projectId, string milestone, DateTime dueDate)
    {
        var project = await GetProjectAsync(projectId);
        if (!project.SmartSheetId.HasValue) return false;
        
        var milestoneData = new Dictionary<long, object>
        {
            [GetTaskNameColumnId()] = milestone,
            [GetDueDateColumnId()] = dueDate,
            [GetStatusColumnId()] = "Not Started",
            [GetTypeColumnId()] = "Milestone"
        };
        
        var rowId = await _writeService.AddRowToSheetAsync(project.SmartSheetId.Value, milestoneData);
        return rowId.HasValue;
    }
}
```

## Best Practices Summary

1. **Use Bulk Operations**: Always batch multiple changes into single requests
2. **Handle Rate Limits**: Implement exponential backoff retry logic
3. **Enable Partial Success**: Allow bulk operations to partially succeed
4. **Proper Authentication**: Always check for valid OAuth session
5. **Column Type Awareness**: Convert values appropriately for column types
6. **Comprehensive Logging**: Log all operations for debugging and audit
7. **Error Handling**: Gracefully handle and report all exceptions
8. **Session Management**: Check token validity before operations

This guide provides the foundation for all SmartSheet write operations in ShopBoss Phase 2 implementation.