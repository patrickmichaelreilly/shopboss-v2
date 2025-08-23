# SmartSheet Integration Architecture Patterns

## Overview

This document outlines architectural patterns and integration approaches for embedding SmartSheet functionality into ShopBoss, based on research into SmartSheet's capabilities and limitations.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     ShopBoss Application                       │
├─────────────────────────────────────────────────────────────────┤
│  Frontend (ASP.NET Core MVC + JavaScript)                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │   Project UI    │  │  iframe Embed   │  │  Custom Grids   │ │
│  │   Components    │  │   SmartSheet    │  │   Components    │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│  Backend Services Layer                                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ SmartSheetSync  │  │ CacheManager    │  │ WebhookHandler  │ │
│  │    Service      │  │    Service      │  │    Service      │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│  Data Layer                                                     │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ ShopBoss DB     │  │  Memory Cache   │  │  Redis Cache    │ │
│  │  (SQLite)       │  │ (IMemoryCache)  │  │ (Distributed)   │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    SmartSheet Cloud API                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  REST API 2.0   │  │   Webhooks      │  │ Published URLs  │ │
│  │   Operations    │  │   Real-time     │  │  (iframe src)   │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Integration Patterns

### 1. Hybrid Integration Pattern (Recommended)

This pattern combines SmartSheet's strengths while mitigating limitations:

**Data Synchronization via API**
- Use SmartSheet API SDK for data operations
- Maintain master data in ShopBoss database
- Bi-directional sync with conflict resolution

**Visual Embedding via iframe**
- Display published SmartSheets in project detail cards
- Preserve SmartSheet's native UI/UX
- Enable direct user interaction with sheets

**Enhanced UI with Custom Components**
- Extract key data for custom ShopBoss UI components
- Provide enhanced filtering, searching, and reporting
- Integrate with existing ShopBoss workflows

```csharp
public class HybridSmartSheetService
{
    // API-based data synchronization
    public async Task SyncProjectDataAsync(string projectId)
    {
        var project = await _projectService.GetProjectAsync(projectId);
        if (string.IsNullOrEmpty(project.SmartSheetId)) return;

        // Fetch latest data from SmartSheet
        var sheet = await _smartSheetApi.GetSheetAsync(long.Parse(project.SmartSheetId));
        
        // Update ShopBoss database
        await UpdateProjectFromSheet(project, sheet);
        
        // Trigger UI updates via SignalR
        await NotifyProjectUpdated(projectId);
    }

    // iframe embedding for user interaction  
    public string GetEmbedUrl(string smartSheetId)
    {
        return $"https://publish.smartsheet.com/{smartSheetId}";
    }

    // Custom UI data extraction
    public async Task<ProjectMilestones> ExtractMilestonesAsync(string projectId)
    {
        var sheet = await GetCachedSheetAsync(projectId);
        return ParseMilestonesFromSheet(sheet);
    }
}
```

### 2. Service Layer Architecture

```csharp
// Core service interfaces
public interface ISmartSheetIntegrationService
{
    Task<Sheet> GetSheetAsync(long sheetId);
    Task<ProjectWorkspace> CreateProjectWorkspaceAsync(Project project);
    Task SyncProjectDataAsync(string projectId);
    string GetEmbedUrl(long sheetId);
}

public interface ISmartSheetSyncService  
{
    Task<SyncResult> SyncFromSmartSheetAsync(string projectId);
    Task<SyncResult> SyncToSmartSheetAsync(string projectId);
    Task HandleWebhookEventAsync(SmartSheetWebhookEvent eventData);
}

public interface ISmartSheetCacheService
{
    Task<T> GetCachedAsync<T>(string key);
    Task SetCachedAsync<T>(string key, T value, TimeSpan expiration);
    Task InvalidateCacheAsync(string pattern);
}
```

### 3. Event-Driven Architecture

Implement event-driven patterns for real-time synchronization:

```csharp
// Domain events for SmartSheet integration
public class SmartSheetUpdatedEvent : IDomainEvent
{
    public long SheetId { get; set; }
    public string ProjectId { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; }
}

public class ProjectUpdatedEvent : IDomainEvent
{
    public string ProjectId { get; set; }
    public DateTime Timestamp { get; set; }
    public List<string> ChangedFields { get; set; }
}

// Event handlers
public class SmartSheetEventHandler : INotificationHandler<SmartSheetUpdatedEvent>
{
    public async Task Handle(SmartSheetUpdatedEvent notification, CancellationToken cancellationToken)
    {
        // Invalidate caches
        await _cacheService.InvalidateCacheAsync($"sheet_{notification.SheetId}*");
        
        // Sync data
        await _syncService.SyncFromSmartSheetAsync(notification.ProjectId);
        
        // Notify UI clients
        await _hubContext.Clients.Group($"Project_{notification.ProjectId}")
            .SendAsync("SheetUpdated", notification);
    }
}
```

## Data Flow Patterns

### 1. Read Operations (ShopBoss → SmartSheet)

```
User Request → Controller → Cache Check → API Service → SmartSheet API
                    ↓              ↓             ↓
                Response ←    Cache Store ←   Data Transform
```

```csharp
public async Task<ProjectSheetData> GetProjectSheetAsync(string projectId)
{
    // L1 Cache: Check memory cache
    var cacheKey = $"project_sheet_{projectId}";
    if (_memoryCache.TryGetValue(cacheKey, out ProjectSheetData cachedData))
    {
        return cachedData;
    }

    // L2 Cache: Check distributed cache
    var distributedData = await _distributedCache.GetAsync<ProjectSheetData>(cacheKey);
    if (distributedData != null)
    {
        _memoryCache.Set(cacheKey, distributedData, TimeSpan.FromMinutes(15));
        return distributedData;
    }

    // API Call: Fetch from SmartSheet
    var project = await _projectService.GetProjectAsync(projectId);
    var sheetData = await _smartSheetApi.GetSheetAsync(long.Parse(project.SmartSheetId));
    
    var transformedData = TransformSheetData(sheetData);
    
    // Cache the result
    await _distributedCache.SetAsync(cacheKey, transformedData, TimeSpan.FromHours(1));
    _memoryCache.Set(cacheKey, transformedData, TimeSpan.FromMinutes(15));
    
    return transformedData;
}
```

### 2. Write Operations (ShopBoss → SmartSheet)

```
User Update → Validation → ShopBoss DB → Background Sync → SmartSheet API
                ↓             ↓              ↓              ↓
            Error Response   Cache Update   Event Publish  Webhook Trigger
```

```csharp
public async Task<UpdateResult> UpdateProjectDataAsync(string projectId, ProjectUpdateRequest request)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    
    try
    {
        // Update ShopBoss database first
        var project = await _projectService.UpdateProjectAsync(projectId, request);
        
        // Invalidate relevant caches
        await _cacheService.InvalidateCacheAsync($"project_{projectId}*");
        
        // Queue background sync to SmartSheet
        _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
        {
            await _syncService.SyncToSmartSheetAsync(projectId);
        });
        
        await transaction.CommitAsync();
        
        // Publish domain event
        await _mediator.Publish(new ProjectUpdatedEvent 
        { 
            ProjectId = projectId,
            ChangedFields = request.GetChangedFields()
        });
        
        return UpdateResult.Success(project);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return UpdateResult.Failure(ex.Message);
    }
}
```

### 3. Real-time Sync (SmartSheet → ShopBoss)

```
SmartSheet Change → Webhook → ShopBoss API → Cache Invalidation → UI Update
                       ↓           ↓              ↓               ↓
                   Validation  → DB Update → SignalR Broadcast → Client Refresh
```

## Component Interaction Patterns

### 1. Project Detail Card with Embedded Sheet

```html
<!-- Project detail view with embedded SmartSheet -->
<div class="project-details-card">
    <!-- ShopBoss custom UI -->
    <div class="project-summary">
        <h3>@Model.ProjectName</h3>
        <div class="project-metrics" id="project-metrics-@Model.Id">
            <!-- Populated via API calls to extracted SmartSheet data -->
        </div>
    </div>
    
    <!-- Embedded SmartSheet iframe -->
    <div class="smartsheet-embed">
        @if (!string.IsNullOrEmpty(Model.SmartSheetId))
        {
            <iframe 
                id="smartsheet-frame-@Model.Id"
                src="@smartSheetService.GetEmbedUrl(Model.SmartSheetId)"
                width="100%" 
                height="600" 
                frameborder="0">
            </iframe>
        }
    </div>
    
    <!-- Enhanced ShopBoss UI -->
    <div class="project-timeline">
        <!-- Custom timeline populated from SmartSheet milestone data -->
    </div>
</div>
```

### 2. JavaScript Integration Layer

```javascript
class SmartSheetProjectIntegration {
    constructor(projectId) {
        this.projectId = projectId;
        this.signalRConnection = null;
        this.setupSignalR();
    }

    async setupSignalR() {
        this.signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl("/statusHub")
            .build();

        await this.signalRConnection.start();
        
        // Listen for SmartSheet updates
        this.signalRConnection.on("SheetUpdated", (data) => {
            if (data.projectId === this.projectId) {
                this.refreshProjectData();
            }
        });

        // Join project-specific group
        await this.signalRConnection.invoke("JoinGroup", `Project_${this.projectId}`);
    }

    async refreshProjectData() {
        try {
            // Refresh custom UI components
            const metrics = await fetch(`/api/projects/${this.projectId}/metrics`);
            const metricsData = await metrics.json();
            this.updateMetricsDisplay(metricsData);

            // Refresh timeline data
            const timeline = await fetch(`/api/projects/${this.projectId}/timeline`);
            const timelineData = await timeline.json();
            this.updateTimelineDisplay(timelineData);

            // iframe will automatically show updated SmartSheet data
        } catch (error) {
            console.error('Failed to refresh project data:', error);
        }
    }
}
```

## Error Handling and Resilience Patterns

### 1. Circuit Breaker Pattern

```csharp
public class SmartSheetCircuitBreakerService
{
    private readonly CircuitBreakerPolicy _circuitBreaker;
    
    public SmartSheetCircuitBreakerService()
    {
        _circuitBreaker = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(2),
                onBreak: (ex, duration) => 
                {
                    _logger.LogWarning("SmartSheet circuit breaker opened for {Duration}", duration);
                },
                onReset: () => 
                {
                    _logger.LogInformation("SmartSheet circuit breaker reset");
                });
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        return await _circuitBreaker.ExecuteAsync(operation);
    }
}
```

### 2. Graceful Degradation

```csharp
public async Task<ProjectDisplayData> GetProjectDisplayDataAsync(string projectId)
{
    var project = await _projectService.GetProjectAsync(projectId);
    var displayData = new ProjectDisplayData(project);

    try
    {
        // Try to get enhanced data from SmartSheet
        var enhancedData = await _smartSheetService.GetEnhancedProjectDataAsync(projectId);
        displayData.EnhancedData = enhancedData;
        displayData.SmartSheetAvailable = true;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "SmartSheet data unavailable for project {ProjectId}, using basic data", projectId);
        
        // Fall back to basic ShopBoss data
        displayData.SmartSheetAvailable = false;
        displayData.FallbackMessage = "SmartSheet integration temporarily unavailable";
    }

    return displayData;
}
```

## Security Patterns

### 1. Token Management

```csharp
public class SmartSheetTokenManager
{
    private readonly IConfiguration _configuration;
    private readonly ISecretManager _secretManager;

    public async Task<string> GetAccessTokenAsync()
    {
        // Prefer secure secret management over configuration
        return await _secretManager.GetSecretAsync("SmartSheet:AccessToken") ??
               _configuration["SmartSheet:AccessToken"];
    }

    public async Task ValidateTokenAsync(string token)
    {
        // Implement token validation logic
        var isValid = await _smartSheetApi.ValidateTokenAsync(token);
        
        if (!isValid)
        {
            throw new UnauthorizedAccessException("SmartSheet token is invalid or expired");
        }
    }
}
```

### 2. iframe Security

```csharp
public class SmartSheetSecurityService
{
    public string GenerateSecureEmbedUrl(long sheetId, string projectId)
    {
        // Validate user has permission to access this project
        if (!_authorizationService.CanAccessProject(User, projectId))
        {
            throw new UnauthorizedAccessException("Access denied to project");
        }

        // Generate time-limited, signed URL if needed
        var baseUrl = $"https://publish.smartsheet.com/{sheetId}";
        
        // Add security parameters if supported
        var secureUrl = $"{baseUrl}?source=shopboss&project={projectId}";
        
        return secureUrl;
    }
}
```

This architectural approach provides a robust foundation for SmartSheet integration while maintaining performance, security, and user experience in ShopBoss.