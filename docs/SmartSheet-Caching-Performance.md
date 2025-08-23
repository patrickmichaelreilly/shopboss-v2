# SmartSheet Caching & Performance Optimization

## Overview

Effective caching strategies are essential for SmartSheet integration performance, especially when embedding grids and providing real-time sync. This guide covers rate limits, caching patterns, and performance optimization for ShopBoss.

## SmartSheet API Rate Limits (2025)

### Current Limitations
- **Rate Limits**: Enforced per application/token (exact limits not publicly documented)
- **Bulk Operations**: Count as single request toward rate limit
- **Row Limits**: Maximum 500 rows per request for add/update operations
- **Sheet Limits**: Maximum 500,000 cells per sheet
- **Report Pagination**: Default 100 rows, maximum 10,000 rows per request
- **Debouncing**: 1-minute debounce on webhooks (as of October 2024)

### Error Handling Strategy
```csharp
public class SmartSheetRateLimitHandler
{
    private readonly ILogger<SmartSheetRateLimitHandler> _logger;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(60);

    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (RateLimitExceededException ex) when (attempt < maxRetries)
            {
                var delay = CalculateExponentialBackoff(attempt);
                _logger.LogWarning("Rate limit exceeded, retrying in {Delay}ms. Attempt {Attempt}/{MaxRetries}", 
                    delay.TotalMilliseconds, attempt + 1, maxRetries + 1);
                
                await Task.Delay(delay);
            }
        }
        
        throw new InvalidOperationException("Maximum retry attempts exceeded");
    }

    private static TimeSpan CalculateExponentialBackoff(int attempt)
    {
        return TimeSpan.FromMilliseconds(
            BaseDelay.TotalMilliseconds * Math.Pow(2, attempt) + 
            Random.Shared.Next(0, 1000)); // Add jitter
    }
}
```

## Caching Architecture for ShopBoss

### Multi-Layer Caching Strategy

```
┌─────────────────────┐
│   Browser Cache    │  ← Client-side caching (iframe, static content)
└─────────────────────┘
           │
┌─────────────────────┐
│   Memory Cache     │  ← Fast, frequently accessed data
│   (IMemoryCache)   │
└─────────────────────┘
           │
┌─────────────────────┐
│  Distributed Cache │  ← Shared across instances
│     (Redis)        │
└─────────────────────┘
           │
┌─────────────────────┐
│   SmartSheet API   │  ← External API calls
└─────────────────────┘
```

### 1. Memory Cache (L1 Cache)

For frequently accessed, small data:

```csharp
public class SmartSheetMemoryCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ISmartSheetApiService _apiService;
    private readonly ILogger<SmartSheetMemoryCacheService> _logger;

    // Cache frequently accessed project sheet metadata
    public async Task<SheetMetadata> GetSheetMetadataAsync(long sheetId)
    {
        var cacheKey = $"sheet_metadata_{sheetId}";
        
        if (_memoryCache.TryGetValue(cacheKey, out SheetMetadata? cached))
        {
            return cached!;
        }

        var metadata = await _apiService.GetSheetMetadataAsync(sheetId);
        
        _memoryCache.Set(cacheKey, metadata, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CacheItemPriority.High
        });

        return metadata;
    }

    // Cache workspace listings
    public async Task<List<Workspace>> GetWorkspacesAsync()
    {
        const string cacheKey = "all_workspaces";
        
        if (_memoryCache.TryGetValue(cacheKey, out List<Workspace>? cached))
        {
            return cached!;
        }

        var workspaces = await _apiService.GetWorkspacesAsync();
        
        _memoryCache.Set(cacheKey, workspaces, TimeSpan.FromMinutes(30));
        
        return workspaces;
    }
}
```

### 2. Distributed Cache (L2 Cache)

For larger, shared data across instances:

```csharp
public class SmartSheetDistributedCacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ISmartSheetApiService _apiService;
    private readonly ILogger<SmartSheetDistributedCacheService> _logger;

    // Cache complete sheet data
    public async Task<Sheet> GetSheetAsync(long sheetId)
    {
        var cacheKey = $"sheet_data_{sheetId}";
        var cachedSheet = await GetFromDistributedCacheAsync<Sheet>(cacheKey);
        
        if (cachedSheet != null)
        {
            _logger.LogDebug("Retrieved sheet {SheetId} from distributed cache", sheetId);
            return cachedSheet;
        }

        var sheet = await _apiService.GetSheetAsync(sheetId);
        
        await SetDistributedCacheAsync(cacheKey, sheet, TimeSpan.FromMinutes(60));
        
        return sheet;
    }

    // Cache project-specific data aggregations
    public async Task<ProjectSheetSummary> GetProjectSummaryAsync(string projectId)
    {
        var cacheKey = $"project_summary_{projectId}";
        var cached = await GetFromDistributedCacheAsync<ProjectSheetSummary>(cacheKey);
        
        if (cached != null)
        {
            return cached;
        }

        var summary = await _apiService.GenerateProjectSummaryAsync(projectId);
        
        await SetDistributedCacheAsync(cacheKey, summary, TimeSpan.FromMinutes(30));
        
        return summary;
    }

    private async Task<T?> GetFromDistributedCacheAsync<T>(string key) where T : class
    {
        var cached = await _distributedCache.GetStringAsync(key);
        
        return cached != null 
            ? JsonSerializer.Deserialize<T>(cached) 
            : null;
    }

    private async Task SetDistributedCacheAsync<T>(string key, T value, TimeSpan expiration)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration,
            SlidingExpiration = TimeSpan.FromMinutes(expiration.TotalMinutes / 3)
        };

        var serialized = JsonSerializer.Serialize(value);
        await _distributedCache.SetStringAsync(key, serialized, options);
    }
}
```

### 3. Hybrid Caching (ASP.NET Core 9.0+)

Leveraging the new HybridCache API:

```csharp
public class SmartSheetHybridCacheService
{
    private readonly HybridCache _hybridCache;
    private readonly ISmartSheetApiService _apiService;

    public async Task<Sheet> GetSheetAsync(long sheetId, CancellationToken cancellationToken = default)
    {
        return await _hybridCache.GetOrCreateAsync(
            $"sheet_{sheetId}",
            async token => await _apiService.GetSheetAsync(sheetId),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(60),
                LocalCacheExpiration = TimeSpan.FromMinutes(15)
            },
            cancellationToken);
    }
}
```

## Cache Invalidation Strategies

### 1. Webhook-Driven Invalidation

```csharp
public class SmartSheetCacheInvalidationService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<SmartSheetCacheInvalidationService> _logger;

    public async Task HandleSheetUpdatedAsync(long sheetId)
    {
        // Invalidate memory cache
        _memoryCache.Remove($"sheet_metadata_{sheetId}");
        _memoryCache.Remove($"sheet_data_{sheetId}");

        // Invalidate distributed cache
        await _distributedCache.RemoveAsync($"sheet_data_{sheetId}");
        await _distributedCache.RemoveAsync($"sheet_summary_{sheetId}");

        // Invalidate project-related caches
        var projectId = await GetProjectIdFromSheetAsync(sheetId);
        if (!string.IsNullOrEmpty(projectId))
        {
            await _distributedCache.RemoveAsync($"project_summary_{projectId}");
            _memoryCache.Remove($"project_metadata_{projectId}");
        }

        _logger.LogInformation("Invalidated caches for sheet {SheetId}", sheetId);
    }

    public async Task HandleWorkspaceUpdatedAsync(long workspaceId)
    {
        // Invalidate workspace listings
        _memoryCache.Remove("all_workspaces");
        await _distributedCache.RemoveAsync("workspace_templates");
        
        // Invalidate all sheets in workspace
        var sheetIds = await GetSheetIdsInWorkspaceAsync(workspaceId);
        foreach (var sheetId in sheetIds)
        {
            await HandleSheetUpdatedAsync(sheetId);
        }
    }
}
```

### 2. Time-Based Invalidation

```csharp
public class SmartSheetCacheConfiguration
{
    public static class ExpirationPolicies
    {
        // Frequently changing data
        public static readonly TimeSpan SheetData = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan ProjectSummary = TimeSpan.FromMinutes(30);
        
        // Moderately changing data  
        public static readonly TimeSpan SheetMetadata = TimeSpan.FromHours(4);
        public static readonly TimeSpan WorkspaceList = TimeSpan.FromHours(12);
        
        // Rarely changing data
        public static readonly TimeSpan Templates = TimeSpan.FromDays(1);
        public static readonly TimeSpan UserPermissions = TimeSpan.FromHours(6);
    }
}
```

## Performance Optimization Techniques

### 1. Bulk Operations

```csharp
public class SmartSheetBulkOperationService
{
    private readonly ISmartSheetApiService _apiService;
    private readonly SmartSheetRateLimitHandler _rateLimitHandler;

    public async Task<BatchUpdateResult> BulkUpdateProjectDataAsync(
        List<ProjectUpdateRequest> updates)
    {
        // Group updates by sheet to minimize API calls
        var groupedUpdates = updates.GroupBy(u => u.SheetId);
        var results = new List<UpdateResult>();

        foreach (var group in groupedUpdates)
        {
            var sheetUpdates = group.Take(500).ToList(); // Respect 500 row limit
            
            var result = await _rateLimitHandler.ExecuteWithRetryAsync(async () =>
                await _apiService.BulkUpdateRowsAsync(group.Key, sheetUpdates));
                
            results.Add(result);
        }

        return new BatchUpdateResult { Results = results };
    }
}
```

### 2. Selective Data Loading

```csharp
public async Task<ProjectSheetView> GetProjectSheetForDisplayAsync(
    string projectId, 
    SheetViewType viewType)
{
    var includeOptions = viewType switch
    {
        SheetViewType.Summary => new[] { "summary" },
        SheetViewType.Grid => new[] { "data", "columns" },
        SheetViewType.Timeline => new[] { "data", "gantt" },
        SheetViewType.Full => new[] { "data", "columns", "attachments", "discussions" },
        _ => new[] { "data" }
    };

    return await GetSheetWithSelectiveLoadingAsync(projectId, includeOptions);
}
```

### 3. Background Sync Jobs

```csharp
public class SmartSheetBackgroundSyncService : BackgroundService
{
    private readonly ISmartSheetSyncService _syncService;
    private readonly ILogger<SmartSheetBackgroundSyncService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Sync critical project data every 15 minutes
                await _syncService.SyncCriticalProjectDataAsync();
                
                // Warm frequently accessed caches
                await WarmCacheAsync();
                
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background sync");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task WarmCacheAsync()
    {
        var activeProjects = await GetActiveProjectsAsync();
        
        var tasks = activeProjects.Select(async project =>
        {
            if (!string.IsNullOrEmpty(project.SmartSheetId))
            {
                // Pre-load sheet metadata into cache
                await GetSheetMetadataAsync(long.Parse(project.SmartSheetId));
            }
        });

        await Task.WhenAll(tasks);
    }
}
```

## Configuration for ShopBoss

### Program.cs Setup

```csharp
// Memory Cache
services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 100; // 100MB limit
    options.CompactionPercentage = 0.75;
});

// Distributed Cache (Redis)
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ShopBoss";
});

// Hybrid Cache (if using .NET 9+)
services.AddHybridCache();

// Custom cache services
services.AddScoped<SmartSheetMemoryCacheService>();
services.AddScoped<SmartSheetDistributedCacheService>();
services.AddScoped<SmartSheetCacheInvalidationService>();
services.AddScoped<SmartSheetRateLimitHandler>();

// Background services
services.AddHostedService<SmartSheetBackgroundSyncService>();
```

### appsettings.json

```json
{
  "SmartSheetCache": {
    "DefaultExpiration": "01:00:00",
    "MemoryCacheSize": 104857600,
    "EnableDistributedCache": true,
    "BackgroundSyncInterval": "00:15:00"
  },
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

## Performance Monitoring

### Cache Hit Rate Tracking

```csharp
public class SmartSheetCacheMetrics
{
    private readonly IMetrics _metrics;
    
    public void RecordCacheHit(string cacheType, string operation)
    {
        _metrics.IncrementCounter("smartsheet_cache_hits", 
            new[] { ("cache_type", cacheType), ("operation", operation) });
    }

    public void RecordCacheMiss(string cacheType, string operation)
    {
        _metrics.IncrementCounter("smartsheet_cache_misses",
            new[] { ("cache_type", cacheType), ("operation", operation) });
    }

    public void RecordApiCall(string endpoint, double duration)
    {
        _metrics.RecordHistogram("smartsheet_api_duration", duration,
            new[] { ("endpoint", endpoint) });
    }
}
```

This comprehensive caching strategy will significantly improve SmartSheet integration performance while respecting API rate limits and providing responsive user experiences in ShopBoss.