# SmartSheet Error Handling & Rate Limiting Patterns

## Overview

This document outlines error handling strategies, rate limiting patterns, and resilience mechanisms for SmartSheet integration in ShopBoss, based on 2025 best practices.

## SmartSheet API Error Categories

### 1. Rate Limiting Errors (HTTP 429)
SmartSheet enforces API rate limits but doesn't publish exact numeric limits.

```csharp
public class SmartSheetRateLimitException : Exception
{
    public TimeSpan RetryAfter { get; }
    public int RemainingAttempts { get; }
    
    public SmartSheetRateLimitException(TimeSpan retryAfter, int remainingAttempts)
    {
        RetryAfter = retryAfter;
        RemainingAttempts = remainingAttempts;
    }
}
```

### 2. Authentication Errors (HTTP 401/403)
Invalid or expired access tokens.

### 3. Resource Errors (HTTP 404)
Sheet, workspace, or resource not found.

### 4. Validation Errors (HTTP 400)
Invalid request data or format.

### 5. Service Errors (HTTP 500+)
SmartSheet internal server errors.

## Retry Patterns with Exponential Backoff

### 1. Exponential Backoff with Jitter

```csharp
public class SmartSheetRetryPolicy
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);
    private static readonly int MaxRetries = 5;
    
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        
        while (attempt <= MaxRetries)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt))
            {
                attempt++;
                var delay = CalculateDelay(attempt, ex);
                
                logger.LogWarning(
                    "SmartSheet operation failed on attempt {Attempt}. Retrying in {Delay}ms. Error: {Error}",
                    attempt, delay.TotalMilliseconds, ex.Message);
                
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        throw new InvalidOperationException($"Operation failed after {MaxRetries} attempts");
    }
    
    private static bool ShouldRetry(Exception ex, int attempt)
    {
        if (attempt >= MaxRetries) return false;
        
        return ex switch
        {
            SmartSheetRateLimitException => true,
            HttpRequestException httpEx when IsTransientError(httpEx) => true,
            TimeoutException => true,
            TaskCanceledException => false, // Don't retry cancellations
            SmartSheetAuthenticationException => false, // Don't retry auth failures
            _ => false
        };
    }
    
    private static TimeSpan CalculateDelay(int attempt, Exception ex)
    {
        // Start with exponential backoff
        var exponentialDelay = TimeSpan.FromMilliseconds(
            BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
        
        // Add jitter to prevent thundering herd
        var jitter = TimeSpan.FromMilliseconds(
            Random.Shared.Next(0, (int)(exponentialDelay.TotalMilliseconds * 0.1)));
        
        var totalDelay = exponentialDelay.Add(jitter);
        
        // Respect Retry-After header if available
        if (ex is SmartSheetRateLimitException rateLimitEx)
        {
            totalDelay = rateLimitEx.RetryAfter;
        }
        
        // Cap at maximum delay
        return totalDelay > MaxDelay ? MaxDelay : totalDelay;
    }
    
    private static bool IsTransientError(HttpRequestException ex)
    {
        // Check for transient HTTP errors
        var message = ex.Message.ToLower();
        return message.Contains("timeout") ||
               message.Contains("connection") ||
               message.Contains("network");
    }
}
```

### 2. Circuit Breaker Pattern

```csharp
public class SmartSheetCircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _recoveryTimeout;
    private readonly ILogger _logger;
    
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private readonly object _lock = new();
    
    public SmartSheetCircuitBreaker(
        int failureThreshold = 5,
        TimeSpan? recoveryTimeout = null,
        ILogger logger = null)
    {
        _failureThreshold = failureThreshold;
        _recoveryTimeout = recoveryTimeout ?? TimeSpan.FromMinutes(2);
        _logger = logger;
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (_state == CircuitBreakerState.Open)
        {
            if (DateTime.UtcNow - _lastFailureTime < _recoveryTimeout)
            {
                throw new SmartSheetCircuitBreakerOpenException(
                    $"Circuit breaker is open. Recovery time remaining: {_recoveryTimeout - (DateTime.UtcNow - _lastFailureTime)}");
            }
            
            // Try to transition to half-open
            lock (_lock)
            {
                if (_state == CircuitBreakerState.Open)
                {
                    _state = CircuitBreakerState.HalfOpen;
                    _logger?.LogInformation("SmartSheet circuit breaker transitioning to half-open");
                }
            }
        }
        
        try
        {
            var result = await operation();
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(ex);
            throw;
        }
    }
    
    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
            _logger?.LogDebug("SmartSheet circuit breaker reset to closed state");
        }
    }
    
    private void OnFailure(Exception ex)
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            
            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitBreakerState.Open;
                _logger?.LogWarning(
                    "SmartSheet circuit breaker opened after {FailureCount} failures. Last error: {Error}",
                    _failureCount, ex.Message);
            }
        }
    }
}

public enum CircuitBreakerState
{
    Closed,   // Normal operation
    Open,     // Blocking requests
    HalfOpen  // Testing recovery
}
```

## Error Response Handling

### 1. Structured Error Processing

```csharp
public class SmartSheetErrorHandler
{
    private readonly ILogger<SmartSheetErrorHandler> _logger;
    
    public SmartSheetOperationResult<T> HandleResponse<T>(
        HttpResponseMessage response, 
        T data = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return SmartSheetOperationResult<T>.Success(data);
        }
        
        var error = ProcessError(response);
        
        return response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => HandleRateLimit(response, error),
            HttpStatusCode.Unauthorized => HandleAuthentication(error),
            HttpStatusCode.Forbidden => HandleAuthorization(error),
            HttpStatusCode.NotFound => HandleResourceNotFound(error),
            HttpStatusCode.BadRequest => HandleValidationError(error),
            _ when IsServerError(response.StatusCode) => HandleServerError(response, error),
            _ => SmartSheetOperationResult<T>.Failure(error.Message, error.ErrorCode)
        };
    }
    
    private SmartSheetOperationResult<T> HandleRateLimit<T>(
        HttpResponseMessage response, 
        SmartSheetError error)
    {
        var retryAfter = GetRetryAfterDelay(response);
        
        _logger.LogWarning(
            "SmartSheet rate limit exceeded. Retry after {RetryAfter}. Error: {Error}",
            retryAfter, error.Message);
        
        return SmartSheetOperationResult<T>.RateLimit(error.Message, retryAfter);
    }
    
    private SmartSheetOperationResult<T> HandleAuthentication<T>(SmartSheetError error)
    {
        _logger.LogError("SmartSheet authentication failed: {Error}", error.Message);
        
        // Could trigger token refresh logic here
        return SmartSheetOperationResult<T>.AuthenticationFailure(error.Message);
    }
    
    private SmartSheetOperationResult<T> HandleServerError<T>(
        HttpResponseMessage response,
        SmartSheetError error)
    {
        _logger.LogError(
            "SmartSheet server error {StatusCode}: {Error}",
            response.StatusCode, error.Message);
        
        return SmartSheetOperationResult<T>.ServerError(error.Message, (int)response.StatusCode);
    }
    
    private TimeSpan GetRetryAfterDelay(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }
        
        // Default retry delay if header not present
        return TimeSpan.FromSeconds(60);
    }
}

public class SmartSheetOperationResult<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public string ErrorMessage { get; set; }
    public SmartSheetErrorType ErrorType { get; set; }
    public TimeSpan? RetryAfter { get; set; }
    public bool ShouldRetry => ErrorType == SmartSheetErrorType.RateLimit || 
                               ErrorType == SmartSheetErrorType.ServerError;
    
    public static SmartSheetOperationResult<T> Success(T data) =>
        new() { Success = true, Data = data };
        
    public static SmartSheetOperationResult<T> RateLimit(string message, TimeSpan retryAfter) =>
        new() { ErrorType = SmartSheetErrorType.RateLimit, ErrorMessage = message, RetryAfter = retryAfter };
}

public enum SmartSheetErrorType
{
    Success,
    RateLimit,
    Authentication,
    Authorization, 
    Validation,
    ResourceNotFound,
    ServerError,
    NetworkError,
    Unknown
}
```

### 2. Fallback Strategies

```csharp
public class SmartSheetFallbackService
{
    private readonly ISmartSheetApiService _primaryService;
    private readonly IMemoryCache _fallbackCache;
    private readonly ILogger<SmartSheetFallbackService> _logger;
    
    public async Task<ProjectSheetData> GetProjectSheetWithFallback(string projectId)
    {
        // Try primary SmartSheet API
        try
        {
            var data = await _primaryService.GetProjectSheetAsync(projectId);
            
            // Cache successful response for fallback
            _fallbackCache.Set($"fallback_{projectId}", data, TimeSpan.FromHours(24));
            
            return data;
        }
        catch (SmartSheetException ex) when (ex.ErrorType == SmartSheetErrorType.RateLimit)
        {
            _logger.LogWarning("Rate limit hit, trying cached fallback for project {ProjectId}", projectId);
            
            // Try cached fallback
            if (_fallbackCache.TryGetValue($"fallback_{projectId}", out ProjectSheetData cached))
            {
                cached.IsFromCache = true;
                cached.CacheWarning = "Data from cache due to rate limiting";
                return cached;
            }
            
            throw; // Re-throw if no fallback available
        }
        catch (SmartSheetException ex) when (ex.ErrorType == SmartSheetErrorType.ServerError)
        {
            return await GetGracefullyDegradedData(projectId);
        }
    }
    
    private async Task<ProjectSheetData> GetGracefullyDegradedData(string projectId)
    {
        _logger.LogWarning("SmartSheet unavailable, providing degraded data for project {ProjectId}", projectId);
        
        // Return basic project data from ShopBoss database
        var project = await _projectService.GetProjectAsync(projectId);
        
        return new ProjectSheetData
        {
            ProjectId = projectId,
            ProjectName = project.ProjectName,
            IsFromCache = false,
            IsDegraded = true,
            CacheWarning = "SmartSheet integration temporarily unavailable"
        };
    }
}
```

## Queue-Based Processing

### 1. Background Queue for Non-Critical Operations

```csharp
public class SmartSheetBackgroundQueue
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmartSheetBackgroundQueue> _logger;
    
    public async Task QueueSheetUpdateAsync(string projectId, ProjectUpdateData updateData)
    {
        _taskQueue.QueueBackgroundWorkItem(async token =>
        {
            using var scope = _scopeFactory.CreateScope();
            var smartSheetService = scope.ServiceProvider.GetRequiredService<ISmartSheetService>();
            
            try
            {
                await SmartSheetRetryPolicy.ExecuteWithRetryAsync(
                    () => smartSheetService.UpdateProjectSheetAsync(projectId, updateData),
                    _logger,
                    token);
                    
                _logger.LogInformation("Successfully updated SmartSheet for project {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update SmartSheet for project {ProjectId} after all retries", projectId);
                
                // Could queue for manual intervention or alternative processing
                await HandleFailedUpdate(projectId, updateData, ex);
            }
        });
    }
    
    private async Task HandleFailedUpdate(string projectId, ProjectUpdateData updateData, Exception ex)
    {
        // Log to dead letter queue, send notification, etc.
        _logger.LogCritical(
            "SmartSheet update permanently failed for project {ProjectId}: {Error}",
            projectId, ex.Message);
            
        // Could implement dead letter queue or manual intervention workflow
    }
}
```

### 2. Bulk Operation Error Handling

```csharp
public class SmartSheetBulkOperationService
{
    public async Task<BulkOperationResult> BulkUpdateProjectsAsync(
        List<ProjectUpdateRequest> updates)
    {
        var results = new List<OperationResult>();
        var batches = updates.Batch(100); // SmartSheet recommends max 500 rows
        
        foreach (var batch in batches)
        {
            try
            {
                var batchResult = await SmartSheetRetryPolicy.ExecuteWithRetryAsync(
                    () => ProcessBatch(batch.ToList()),
                    _logger);
                    
                results.AddRange(batchResult.Results);
            }
            catch (SmartSheetRateLimitException)
            {
                // Queue remaining batches for later processing
                await QueueRemainingBatches(batch);
                results.Add(OperationResult.Deferred("Batch deferred due to rate limiting"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch operation failed");
                results.Add(OperationResult.Failed(ex.Message));
            }
        }
        
        return new BulkOperationResult
        {
            TotalRequests = updates.Count,
            SuccessfulRequests = results.Count(r => r.Success),
            FailedRequests = results.Count(r => r.Failed),
            DeferredRequests = results.Count(r => r.Deferred),
            Results = results
        };
    }
}
```

## Monitoring and Alerting

### 1. Error Rate Monitoring

```csharp
public class SmartSheetMetrics
{
    private readonly IMetricsCollector _metrics;
    
    public void RecordApiCall(string operation, bool success, double duration)
    {
        _metrics.Counter("smartsheet_api_calls_total")
            .WithTag("operation", operation)
            .WithTag("success", success.ToString())
            .Increment();
            
        _metrics.Histogram("smartsheet_api_duration_ms")
            .WithTag("operation", operation)
            .Record(duration);
    }
    
    public void RecordRateLimit(string operation)
    {
        _metrics.Counter("smartsheet_rate_limits_total")
            .WithTag("operation", operation)
            .Increment();
    }
    
    public void RecordCircuitBreakerState(CircuitBreakerState state)
    {
        _metrics.Gauge("smartsheet_circuit_breaker_state")
            .WithTag("state", state.ToString())
            .Set(1);
    }
}
```

### 2. Health Check Integration

```csharp
public class SmartSheetHealthCheck : IHealthCheck
{
    private readonly ISmartSheetService _smartSheetService;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple API call to verify connectivity
            await _smartSheetService.GetUserInfoAsync();
            
            return HealthCheckResult.Healthy("SmartSheet API is responsive");
        }
        catch (SmartSheetRateLimitException)
        {
            return HealthCheckResult.Degraded("SmartSheet API rate limited");
        }
        catch (SmartSheetAuthenticationException)
        {
            return HealthCheckResult.Unhealthy("SmartSheet authentication failed");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"SmartSheet API error: {ex.Message}");
        }
    }
}
```

This comprehensive error handling strategy ensures robust SmartSheet integration that can gracefully handle various failure scenarios while maintaining system reliability and user experience.