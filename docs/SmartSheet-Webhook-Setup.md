# SmartSheet Webhook Setup Guide

## Overview

SmartSheet webhooks enable real-time notifications when changes occur in sheets, providing immediate sync capabilities for ShopBoss project management integration.

## Webhook System Architecture (2025)

### Key Updates for 2025
- **One-minute debounce**: Introduced October 2024 to optimize event handling
- **IP address migration**: New webhooks domain `webhooks.smartsheet.com` 
- **Sunset date**: Legacy IP addresses discontinued as early as September 1, 2025
- **Performance optimizations**: Reduced latency and improved stability

### Webhook Limitations
⚠️ **Important**: Webhooks are automatically disabled on sheets exceeding:
- 20,000 rows
- 400 columns  
- 500,000 cells (whichever comes first)

## Webhook Setup Process

### 1. Endpoint Requirements

Your webhook endpoint must:
- Use **HTTPS** (self-signed certificates not supported)
- Handle **POST requests**
- Respond with **200 status code**
- Parse JSON request body

### 2. Creating a Webhook

#### API Request
```bash
curl https://api.smartsheet.com/2.0/webhooks \
  -H "Authorization: Bearer ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -X POST \
  -d '{
    "name": "ShopBoss Project Sync",
    "callbackUrl": "https://shopboss.yourdomain.com/api/smartsheet/webhook",
    "scope": "sheet",
    "scopeObjectId": 2258256056870788,
    "version": 1,
    "events": ["*.*"]
  }'
```

#### Parameters
- **name**: Descriptive webhook name
- **callbackUrl**: HTTPS endpoint in your application
- **scope**: `sheet`, `workspace`, or `folder`
- **scopeObjectId**: ID of the SmartSheet object to monitor
- **version**: Always use `1`
- **events**: Array of events to monitor (use `["*.*"]` for all)

### 3. Event Types

Common event patterns:
- `*.*` - All events (recommended for comprehensive sync)
- `sheet.*` - All sheet events
- `row.*` - Row modifications (create, update, delete)
- `cell.*` - Cell value changes
- `attachment.*` - File attachments
- `discussion.*` - Comments and discussions

## ShopBoss Integration Implementation

### 1. Webhook Controller

```csharp
[ApiController]
[Route("api/smartsheet")]
public class SmartSheetWebhookController : ControllerBase
{
    private readonly ISmartSheetSyncService _syncService;
    private readonly ILogger<SmartSheetWebhookController> _logger;

    public SmartSheetWebhookController(
        ISmartSheetSyncService syncService,
        ILogger<SmartSheetWebhookController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook([FromBody] SmartSheetWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Received SmartSheet webhook for {EventType}", payload.Challenge);
            
            // Handle verification challenge
            if (!string.IsNullOrEmpty(payload.Challenge))
            {
                return Ok(new { smartsheetHookResponse = payload.Challenge });
            }

            // Process events
            foreach (var evt in payload.Events ?? new List<SmartSheetEvent>())
            {
                await ProcessWebhookEvent(evt);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SmartSheet webhook");
            return StatusCode(500);
        }
    }

    private async Task ProcessWebhookEvent(SmartSheetEvent evt)
    {
        switch (evt.ObjectType?.ToLower())
        {
            case "sheet":
                await _syncService.SyncSheetChanges(evt.Id, evt.EventType);
                break;
            case "row":
                await _syncService.SyncRowChanges(evt.Id, evt.EventType);
                break;
            case "cell":
                await _syncService.SyncCellChanges(evt.Id, evt.EventType);
                break;
            default:
                _logger.LogWarning("Unhandled event type: {ObjectType}", evt.ObjectType);
                break;
        }
    }
}
```

### 2. Webhook Payload Models

```csharp
public class SmartSheetWebhookPayload
{
    public string? Challenge { get; set; }
    public List<SmartSheetEvent>? Events { get; set; }
}

public class SmartSheetEvent
{
    public long Id { get; set; }
    public string? ObjectType { get; set; }
    public string? EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public long? UserId { get; set; }
    public string? RequestId { get; set; }
}
```

### 3. Sync Service Implementation

```csharp
public interface ISmartSheetSyncService
{
    Task SyncSheetChanges(long sheetId, string eventType);
    Task SyncRowChanges(long rowId, string eventType);
    Task SyncCellChanges(long cellId, string eventType);
}

public class SmartSheetSyncService : ISmartSheetSyncService
{
    private readonly ShopBossDbContext _context;
    private readonly ISmartSheetService _smartSheetService;
    private readonly IHubContext<StatusHub> _hubContext;

    public async Task SyncSheetChanges(long sheetId, string eventType)
    {
        // Find associated project
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.SmartSheetId == sheetId.ToString());
            
        if (project == null) return;

        // Fetch updated sheet data
        var sheet = await _smartSheetService.GetSheetAsync(sheetId);
        
        // Update project with latest data
        await UpdateProjectFromSheet(project, sheet);
        
        // Notify connected clients
        await _hubContext.Clients.Group($"Project_{project.Id}")
            .SendAsync("ProjectUpdated", project);
    }
}
```

## Webhook Management

### 1. List Existing Webhooks

```csharp
public async Task<List<Webhook>> GetWebhooksAsync()
{
    var response = await _httpClient.GetAsync("https://api.smartsheet.com/2.0/webhooks");
    var content = await response.Content.ReadAsStringAsync();
    var result = JsonSerializer.Deserialize<WebhookListResponse>(content);
    return result.Data ?? new List<Webhook>();
}
```

### 2. Update Webhook

```csharp
public async Task UpdateWebhookAsync(long webhookId, WebhookUpdateRequest update)
{
    var json = JsonSerializer.Serialize(update);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    
    var response = await _httpClient.PutAsync($"https://api.smartsheet.com/2.0/webhooks/{webhookId}", content);
    response.EnsureSuccessStatusCode();
}
```

### 3. Delete Webhook

```csharp
public async Task DeleteWebhookAsync(long webhookId)
{
    var response = await _httpClient.DeleteAsync($"https://api.smartsheet.com/2.0/webhooks/{webhookId}");
    response.EnsureSuccessStatusCode();
}
```

## Error Handling and Retry Logic

### 1. Webhook Delivery Retry

SmartSheet automatically retries failed webhooks:
- First 7 attempts: Exponential backoff
- Remaining attempts: Every 3 hours
- Maximum: 14 total attempts

### 2. Handling Failed Deliveries

```csharp
[HttpPost("webhook")]
public async Task<IActionResult> HandleWebhook([FromBody] SmartSheetWebhookPayload payload)
{
    try
    {
        await ProcessWebhookEvents(payload.Events);
        return Ok(); // Must return 200 for successful delivery
    }
    catch (TemporaryException ex)
    {
        _logger.LogWarning(ex, "Temporary error processing webhook");
        return StatusCode(500); // Trigger SmartSheet retry
    }
    catch (PermanentException ex)
    {
        _logger.LogError(ex, "Permanent error processing webhook");
        return Ok(); // Return 200 to avoid unnecessary retries
    }
}
```

## Security Considerations

### 1. IP Allow-listing (2025 Update)

```csharp
// Add to Startup.cs or Program.cs
services.Configure<ForwardedHeadersOptions>(options =>
{
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    
    // Allow SmartSheet webhook domain (2025)
    // Use domain allow-listing instead of IP addresses
    options.AllowedHosts.Add("webhooks.smartsheet.com");
});
```

### 2. Webhook Verification

```csharp
// Verify webhook authenticity using challenge response
if (!string.IsNullOrEmpty(payload.Challenge))
{
    return Ok(new { smartsheetHookResponse = payload.Challenge });
}
```

### 3. Rate Limiting Protection

```csharp
[EnableRateLimiting("SmartSheetWebhook")]
[HttpPost("webhook")]
public async Task<IActionResult> HandleWebhook([FromBody] SmartSheetWebhookPayload payload)
{
    // Implementation with rate limiting
}
```

## Monitoring and Debugging

### 1. Webhook Status Monitoring

```csharp
public async Task<WebhookStatus> CheckWebhookStatusAsync(long webhookId)
{
    var webhook = await GetWebhookAsync(webhookId);
    return new WebhookStatus
    {
        IsEnabled = webhook.Enabled,
        LastCallbackAttempt = webhook.Stats?.LastCallbackAttempt,
        LastCallbackSuccess = webhook.Stats?.LastCallbackSuccess,
        LastCallbackFailure = webhook.Stats?.LastCallbackFailure
    };
}
```

### 2. Event Logging

```csharp
private async Task ProcessWebhookEvent(SmartSheetEvent evt)
{
    using var scope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["SheetId"] = evt.Id,
        ["EventType"] = evt.EventType ?? "unknown",
        ["UserId"] = evt.UserId ?? 0,
        ["RequestId"] = evt.RequestId ?? "unknown"
    });

    _logger.LogInformation("Processing SmartSheet event");
    
    // Process event...
}
```

## Testing Webhooks

### 1. Local Development with ngrok

```bash
# Install ngrok
npm install -g ngrok

# Expose local ShopBoss instance
ngrok http 5000

# Use ngrok URL for webhook callback
# https://abc123.ngrok.io/api/smartsheet/webhook
```

### 2. Webhook Testing Service

```csharp
[HttpPost("test-webhook")]
public IActionResult TestWebhook([FromBody] object payload)
{
    _logger.LogInformation("Test webhook received: {Payload}", 
        JsonSerializer.Serialize(payload));
    return Ok();
}
```

This comprehensive webhook setup enables real-time synchronization between SmartSheet and ShopBoss, providing immediate updates when project data changes in SmartSheet.