# SmartSheet Rate Limiting & Performance Guide

## Overview

SmartSheet API enforces strict rate limits to ensure service reliability. This guide provides strategies for staying within limits, handling rate limit errors, and optimizing performance in the ShopBoss integration.

## Rate Limit Specifications

### Current Limits (2024)
- **Standard Rate Limit**: 300 requests per minute per access token
- **Resource-Intensive Operations**: Count as 10 requests each
- **Burst Tolerance**: Some short-term bursts may be allowed
- **Reset Window**: Rolling 60-second window

### Resource-Intensive Operations (10x Cost)
```csharp
// These operations count as 10 requests each:
var intensiveOperations = new[]
{
    "GET /sheets/{id}?include=attachments,discussions", // With includes
    "POST /sheets/{id}/copy",                           // Copy sheet
    "GET /reports/{id}",                                // Get report
    "GET /workspaces/{id}?include=projects",           // With includes
    "POST /sheets/import",                              // Import operations
    "GET /search"                                       // Search operations
};
```

## Rate Limiting Detection and Handling

### Exception Detection
```csharp
public class RateLimitDetector
{
    public static bool IsRateLimitError(Exception ex)
    {
        if (ex == null) return false;

        var message = ex.Message?.ToLower() ?? "";
        
        // Check for common rate limit indicators
        return message.Contains("rate limit") ||
               message.Contains("429") ||
               message.Contains("too many requests") ||
               message.Contains("quota exceeded");
    }

    public static TimeSpan GetRetryDelay(Exception ex, int attempt = 1)
    {
        // Try to extract Retry-After header if available
        // For now, use exponential backoff
        var baseDelay = TimeSpan.FromSeconds(30); // Start with 30 seconds
        var exponentialDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1) * 30);
        
        return exponentialDelay > TimeSpan.FromMinutes(10) 
            ? TimeSpan.FromMinutes(10) // Cap at 10 minutes
            : exponentialDelay;
    }
}
```

### Retry Strategy Implementation
```csharp
public class SmartSheetRateLimitHandler
{
    private readonly ILogger<SmartSheetRateLimitHandler> _logger;
    private readonly SemaphoreSlim _semaphore;
    private static readonly ConcurrentQueue<DateTime> _requestTimes = new();

    public SmartSheetRateLimitHandler(ILogger<SmartSheetRateLimitHandler> logger)
    {
        _logger = logger;
        // Limit concurrent requests to prevent bursts
        _semaphore = new SemaphoreSlim(5, 5);
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName = "Unknown",
        int maxRetries = 3,
        bool isResourceIntensive = false)
    {
        await _semaphore.WaitAsync();
        
        try
        {
            await ThrottleRequestAsync(isResourceIntensive);
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug("Executing {Operation} (attempt {Attempt}/{MaxRetries})", 
                        operationName, attempt, maxRetries);
                    
                    var result = await operation();
                    
                    RecordSuccessfulRequest(isResourceIntensive);
                    return result;
                }
                catch (Exception ex) when (RateLimitDetector.IsRateLimitError(ex) && attempt < maxRetries)
                {
                    var delay = RateLimitDetector.GetRetryDelay(ex, attempt);
                    
                    _logger.LogWarning("Rate limit hit for {Operation}, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})", 
                        operationName, delay.TotalSeconds, attempt, maxRetries);
                    
                    await Task.Delay(delay);
                }
            }
            
            // If we get here, all retries failed
            throw new InvalidOperationException($"Operation {operationName} failed after {maxRetries} attempts due to rate limiting");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ThrottleRequestAsync(bool isResourceIntensive)
    {
        CleanOldRequests();
        
        var requestCost = isResourceIntensive ? 10 : 1;
        var currentLoad = CalculateCurrentLoad();
        
        // If we're approaching the limit, wait
        if (currentLoad + requestCost > 250) // Leave buffer of 50 requests
        {
            var waitTime = TimeSpan.FromSeconds(10);
            _logger.LogInformation("Throttling request due to high load ({CurrentLoad}), waiting {WaitTime}s", 
                currentLoad, waitTime.TotalSeconds);
            
            await Task.Delay(waitTime);
        }
    }

    private void RecordSuccessfulRequest(bool isResourceIntensive)
    {
        var now = DateTime.UtcNow;
        var requestCost = isResourceIntensive ? 10 : 1;
        
        // Record each "cost unit" as a separate timestamp for accurate tracking
        for (int i = 0; i < requestCost; i++)
        {
            _requestTimes.Enqueue(now);
        }
    }

    private void CleanOldRequests()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-1);
        
        while (_requestTimes.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            _requestTimes.TryDequeue(out _);
        }
    }

    private int CalculateCurrentLoad()
    {
        CleanOldRequests();
        return _requestTimes.Count;
    }
}
```

## Batching Strategies

### Bulk Operations Implementation
```csharp
public class SmartSheetBulkOperations
{
    private readonly SmartSheetRateLimitHandler _rateLimitHandler;
    private readonly ILogger<SmartSheetBulkOperations> _logger;

    public SmartSheetBulkOperations(
        SmartSheetRateLimitHandler rateLimitHandler,
        ILogger<SmartSheetBulkOperations> logger)
    {
        _rateLimitHandler = rateLimitHandler;
        _logger = logger;
    }

    public async Task<List<T>> ProcessInBatchesAsync<T, TInput>(
        IEnumerable<TInput> items,
        Func<IEnumerable<TInput>, Task<IEnumerable<T>>> batchProcessor,
        int batchSize = 100,
        string operationName = "Batch Operation")
    {
        var results = new List<T>();
        var batches = items.Chunk(batchSize);
        
        foreach (var batch in batches)
        {
            var batchResults = await _rateLimitHandler.ExecuteWithRetryAsync(
                () => batchProcessor(batch),
                $"{operationName} (batch of {batch.Count()})"
            );
            
            results.AddRange(batchResults);
            
            // Small delay between batches to be API-friendly
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
        
        return results;
    }

    public async Task<BulkRowResult> AddRowsInBatchesAsync(
        SmartsheetClient client,
        long sheetId,
        IEnumerable<Row> rows,
        int batchSize = 50)
    {
        var allResults = new List<Row>();
        var allFailures = new List<BulkItemFailure>();
        
        var batches = rows.Chunk(batchSize);
        
        foreach (var batch in batches)
        {
            var result = await _rateLimitHandler.ExecuteWithRetryAsync(
                async () => await Task.Run(() => 
                    client.SheetResources.RowResources.AddRows(
                        sheetId, 
                        batch.ToArray(), 
                        null, null, null, 
                        true)), // Allow partial success
                $"Add rows batch ({batch.Count()} rows)"
            );
            
            if (result.Result != null)
            {
                allResults.AddRange(result.Result);
            }
            
            if (result.FailedItems != null)
            {
                allFailures.AddRange(result.FailedItems);
            }
            
            // Brief pause between batches
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
        
        return new BulkRowResult
        {
            SuccessfulRows = allResults,
            FailedItems = allFailures,
            TotalProcessed = rows.Count()
        };
    }
}

public class BulkRowResult
{
    public List<Row> SuccessfulRows { get; set; } = new();
    public List<BulkItemFailure> FailedItems { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int SuccessCount => SuccessfulRows.Count;
    public int FailureCount => FailedItems.Count;
}
```

## Request Optimization Strategies

### Efficient Data Loading
```csharp
public class OptimizedSmartSheetReader
{
    private readonly SmartSheetRateLimitHandler _rateLimitHandler;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OptimizedSmartSheetReader> _logger;

    public OptimizedSmartSheetReader(
        SmartSheetRateLimitHandler rateLimitHandler,
        IMemoryCache cache,
        ILogger<OptimizedSmartSheetReader> logger)
    {
        _rateLimitHandler = rateLimitHandler;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Sheet> GetSheetWithCachingAsync(
        SmartsheetClient client,
        long sheetId,
        IEnumerable<SheetLevelInclusion>? includes = null,
        TimeSpan? cacheTimeout = null)
    {
        var cacheKey = $"sheet_{sheetId}_{string.Join(",", includes ?? Array.Empty<SheetLevelInclusion>())}";
        
        if (_cache.TryGetValue(cacheKey, out Sheet cachedSheet))
        {
            _logger.LogDebug("Retrieved sheet {SheetId} from cache", sheetId);
            return cachedSheet;
        }

        var sheet = await _rateLimitHandler.ExecuteWithRetryAsync(
            () => Task.Run(() => client.SheetResources.GetSheet(
                sheetId, includes, null, null, null, null, null, null)),
            $"Get sheet {sheetId}",
            isResourceIntensive: includes?.Any() == true
        );

        var timeout = cacheTimeout ?? TimeSpan.FromMinutes(5);
        _cache.Set(cacheKey, sheet, timeout);
        
        _logger.LogDebug("Cached sheet {SheetId} for {Timeout}", sheetId, timeout);
        
        return sheet;
    }

    public async Task<List<Sheet>> GetMultipleSheetsAsync(
        SmartsheetClient client,
        IEnumerable<long> sheetIds,
        IEnumerable<SheetLevelInclusion>? includes = null)
    {
        var sheets = new List<Sheet>();
        
        // Process sheets in small batches to avoid overwhelming the API
        foreach (var sheetId in sheetIds)
        {
            var sheet = await GetSheetWithCachingAsync(client, sheetId, includes);
            sheets.Add(sheet);
            
            // Small delay between individual sheet requests
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
        
        return sheets;
    }
}
```

### Smart Caching Strategy
```csharp
public class SmartSheetCacheManager
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SmartSheetCacheManager> _logger;

    public SmartSheetCacheManager(IMemoryCache memoryCache, ILogger<SmartSheetCacheManager> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public void CacheSheet(long sheetId, Sheet sheet, TimeSpan? timeout = null)
    {
        var cacheTimeout = timeout ?? GetOptimalCacheTimeout(sheet);
        var cacheKey = $"sheet_{sheetId}_full";
        
        _memoryCache.Set(cacheKey, sheet, cacheTimeout);
        
        // Also cache metadata separately for quick access
        var metadataKey = $"sheet_{sheetId}_metadata";
        var metadata = new SheetMetadata
        {
            Id = sheet.Id ?? 0,
            Name = sheet.Name ?? "",
            ModifiedAt = sheet.ModifiedAt,
            RowCount = (int)(sheet.TotalRowCount ?? 0),
            ColumnCount = sheet.Columns?.Count ?? 0
        };
        
        _memoryCache.Set(metadataKey, metadata, cacheTimeout);
        
        _logger.LogDebug("Cached sheet {SheetId} with timeout {Timeout}", sheetId, cacheTimeout);
    }

    public Sheet? GetCachedSheet(long sheetId)
    {
        var cacheKey = $"sheet_{sheetId}_full";
        return _memoryCache.Get<Sheet>(cacheKey);
    }

    public SheetMetadata? GetCachedMetadata(long sheetId)
    {
        var cacheKey = $"sheet_{sheetId}_metadata";
        return _memoryCache.Get<SheetMetadata>(cacheKey);
    }

    public void InvalidateSheet(long sheetId)
    {
        var keys = new[]
        {
            $"sheet_{sheetId}_full",
            $"sheet_{sheetId}_metadata"
        };
        
        foreach (var key in keys)
        {
            _memoryCache.Remove(key);
        }
        
        _logger.LogDebug("Invalidated cache for sheet {SheetId}", sheetId);
    }

    private static TimeSpan GetOptimalCacheTimeout(Sheet sheet)
    {
        // Adjust cache timeout based on sheet activity patterns
        var lastModified = sheet.ModifiedAt ?? DateTime.MinValue;
        var timeSinceModified = DateTime.UtcNow - lastModified;
        
        return timeSinceModified switch
        {
            var t when t < TimeSpan.FromHours(1) => TimeSpan.FromMinutes(2),  // Recent activity - short cache
            var t when t < TimeSpan.FromDays(1) => TimeSpan.FromMinutes(15), // Daily activity - medium cache
            _ => TimeSpan.FromHours(1) // Older sheets - longer cache
        };
    }
}

public class SheetMetadata
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? ModifiedAt { get; set; }
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
}
```

## Performance Monitoring

### Request Tracking and Analytics
```csharp
public class SmartSheetPerformanceMonitor
{
    private readonly ILogger<SmartSheetPerformanceMonitor> _logger;
    private readonly ConcurrentDictionary<string, OperationStats> _operationStats = new();

    public SmartSheetPerformanceMonitor(ILogger<SmartSheetPerformanceMonitor> logger)
    {
        _logger = logger;
    }

    public void RecordOperation(string operationName, TimeSpan duration, bool success, bool wasRetried = false)
    {
        _operationStats.AddOrUpdate(operationName, 
            new OperationStats { OperationName = operationName },
            (key, existing) => existing);

        var stats = _operationStats[operationName];
        stats.RecordExecution(duration, success, wasRetried);
    }

    public void LogPerformanceStats()
    {
        foreach (var kvp in _operationStats)
        {
            var stats = kvp.Value;
            
            _logger.LogInformation(
                "SmartSheet Operation Stats - {Operation}: " +
                "Total: {Total}, Success: {Success}%, " +
                "Avg Duration: {AvgDuration}ms, Retries: {Retries}%",
                stats.OperationName,
                stats.TotalExecutions,
                Math.Round(stats.SuccessRate * 100, 1),
                Math.Round(stats.AverageDuration.TotalMilliseconds, 1),
                Math.Round(stats.RetryRate * 100, 1)
            );
        }
    }
}

public class OperationStats
{
    public string OperationName { get; set; } = "";
    public int TotalExecutions { get; private set; }
    public int SuccessfulExecutions { get; private set; }
    public int RetriedExecutions { get; private set; }
    public TimeSpan TotalDuration { get; private set; }

    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0;
    public double RetryRate => TotalExecutions > 0 ? (double)RetriedExecutions / TotalExecutions : 0;
    public TimeSpan AverageDuration => TotalExecutions > 0 ? 
        TimeSpan.FromTicks(TotalDuration.Ticks / TotalExecutions) : TimeSpan.Zero;

    private readonly object _lock = new();

    public void RecordExecution(TimeSpan duration, bool success, bool wasRetried)
    {
        lock (_lock)
        {
            TotalExecutions++;
            TotalDuration = TotalDuration.Add(duration);
            
            if (success)
                SuccessfulExecutions++;
                
            if (wasRetried)
                RetriedExecutions++;
        }
    }
}
```

## Configuration and Best Practices

### Service Registration
```csharp
// In Program.cs
services.AddSingleton<SmartSheetRateLimitHandler>();
services.AddSingleton<SmartSheetPerformanceMonitor>();
services.AddScoped<SmartSheetBulkOperations>();
services.AddScoped<OptimizedSmartSheetReader>();
services.AddScoped<SmartSheetCacheManager>();

// Background service for performance monitoring
services.AddHostedService<PerformanceReportingService>();
```

### Configuration Settings
```json
{
  "SmartSheet": {
    "RateLimit": {
      "RequestsPerMinute": 300,
      "ResourceIntensiveMultiplier": 10,
      "MaxRetryAttempts": 3,
      "BaseRetryDelaySeconds": 30,
      "MaxConcurrentRequests": 5,
      "ThrottleThreshold": 250
    },
    "Performance": {
      "DefaultCacheTimeoutMinutes": 5,
      "BulkOperationBatchSize": 50,
      "BatchDelayMilliseconds": 200,
      "RequestDelayMilliseconds": 50
    }
  }
}
```

## Rate Limiting Best Practices

### 1. Design Patterns
- **Use bulk operations** whenever possible (AddRows vs individual AddRow)
- **Batch related requests** together with small delays
- **Cache frequently accessed data** to reduce API calls
- **Prioritize critical operations** during high-usage periods

### 2. Error Recovery
- **Implement exponential backoff** with jitter
- **Allow partial success** in bulk operations
- **Graceful degradation** when rate limited
- **Clear user feedback** about rate limit delays

### 3. Monitoring and Alerting
- **Track request patterns** and identify optimization opportunities
- **Monitor success/retry rates** for different operations
- **Alert on sustained rate limiting** indicating design issues
- **Regular performance reviews** to adjust strategies

### 4. Development Guidelines
- **Test with realistic data volumes** to identify rate limit issues early
- **Use staging environment** to validate bulk operation performance
- **Profile API usage** in development to optimize before production
- **Document rate limit costs** for each operation type

This comprehensive rate limiting strategy ensures reliable SmartSheet integration while maximizing throughput within API constraints.