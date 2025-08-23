# SmartSheet Integration - Implementation Decisions

**Date:** August 23, 2025  
**Status:** Finalized  
**Context:** Temporary bridge solution for transition period

## Project Context

This SmartSheet integration is a **temporary bridge solution** to ease the transition while an in-house SmartSheet clone is being developed. Key constraints:

- **Temporary solution** - will be deprecated when in-house clone is ready
- **4 users only** - minimal concurrency concerns
- **Simplicity over robustness** - avoid over-engineering
- **Quick delivery** - focus on core functionality

## Core Implementation Approach

### Architecture: Session-Based Hybrid Integration
1. **Session-based OAuth** for user attribution (no user storage in ShopBoss)
2. **iframe embedding** with published SmartSheets
3. **Basic synchronization** with simple field mapping
4. **Memory-only caching** (no Redis complexity)
5. **Single template approach** with template ID: **8130468873457540**

### What We're Building

**Phase 1: Session-Based OAuth & Basic Linking (2-3 days)**
- Add `SmartSheetId` field to Project model
- Session-only OAuth flow (no user storage)
- "Link SmartSheet" functionality
- Display basic sheet metadata in project cards

**Phase 2: iframe Embedding (1-2 days)**
- Embed published SmartSheet URLs in project details
- Basic loading states and error handling
- iframe communication setup

**Phase 3: Minimal Sync (2-3 days)**
- Simple webhook receiver endpoint
- Basic field mapping with clear ownership rules
- Memory cache for performance
- Simple timestamp-based conflict resolution

**Phase 4: Template Creation (1-2 days)**
- "Create SmartSheet" from single template
- Auto-link new sheets to projects
- Update master project list

### What We're NOT Building
❌ Complex conflict resolution algorithms  
❌ Redis/distributed caching infrastructure  
❌ Sophisticated error recovery patterns  
❌ Circuit breakers and elaborate retry logic  
❌ Multiple template management system  
❌ Extensive fallback mechanisms  
❌ Advanced monitoring and alerting  

## Key Technical Decisions

### 1. Authentication: Session-Based OAuth
**Decision:** Session-only OAuth tokens for user attribution (no permanent user storage)
**Implementation:**
```csharp
public class SmartSheetSessionService
{
    // Store tokens ONLY in session, never in database
    public async Task<IActionResult> InitiateOAuth()
    {
        var state = Guid.NewGuid().ToString();
        HttpContext.Session.SetString("oauth_state", state);
        
        var authUrl = BuildAuthorizationUrl(state);
        return Redirect(authUrl);
    }
    
    public async Task<IActionResult> HandleCallback(string code)
    {
        var tokens = await ExchangeCodeForTokens(code);
        
        // Store ONLY in session memory (expires with browser session)
        HttpContext.Session.SetString("ss_token", tokens.AccessToken);
        HttpContext.Session.SetString("ss_refresh", tokens.RefreshToken);
        HttpContext.Session.SetString("ss_user", tokens.UserEmail);
        
        return RedirectToAction("Index", "Project");
    }
}
```

**Rationale:**
- Enables user-level audit trails in SmartSheet
- No permanent user data storage in ShopBoss
- Simple session-based approach
- Users must re-authenticate each browser session (acceptable for 4 users)

### 2. Data Synchronization: Simple Field Ownership
**Decision:** Clear field ownership with minimal conflict resolution

**ShopBoss Owns (Never Overwritten):**
- ProjectId
- ProjectName
- ProjectCategory  
- GeneralContractor
- Target dates

**SmartSheet Owns (Always Overwritten from SmartSheet):**
- Task status fields
- Completion percentages
- Comments/discussions
- File attachments
- Progress tracking

**Shared Fields:** Most recent timestamp wins

### 3. Template Management: Single Template
**Decision:** One hardcoded template ID in configuration
```json
{
  "SmartSheet": {
    "ProjectTemplateId": 8130468873457540
  }
}
```

**Rationale:**
- Simplifies template selection logic
- Reduces configuration complexity
- Sufficient for temporary solution

### 4. Caching Strategy: Memory Only
**Decision:** Use `IMemoryCache` only, no Redis
- Sheet metadata: 15 minutes
- Session tokens: Stored in ASP.NET Session only
- Project data: 5 minutes

**Rationale:**
- 4 users don't need distributed caching
- Reduces infrastructure complexity
- Sufficient performance for temporary solution

### 5. Security Model: Published Sheets
**Decision:** Accept published sheet security model
- SmartSheets must be published for iframe embedding
- Rely on obscure URLs for basic security
- Document security implications clearly

**Rationale:**
- No viable alternative for iframe embedding
- Temporary solution - acceptable trade-off
- Future in-house clone will resolve this

### 6. Error Handling: Basic Patterns Only
**Decision:** Simple error handling without complex patterns
- Basic retry on rate limits (3 attempts max)
- Graceful degradation when SmartSheet unavailable
- Simple logging and user notification

**Rationale:**
- Avoid over-engineering temporary solution
- 4 users can handle occasional manual intervention
- Focus development time on core functionality

## Implementation Sequence

### Phase 1: Foundation (2-3 days)
```csharp
// Database changes (minimal)
public class Project 
{
    // ... existing fields
    public string? SmartSheetId { get; set; }
    public DateTime? SmartSheetLastSync { get; set; }
}

// Session-based service (no user storage)
public class SmartSheetIntegrationService
{
    // Check if user has active SmartSheet session
    public bool HasSmartSheetSession()
    {
        return HttpContext.Session.GetString("ss_token") != null;
    }
    
    // Use token from session for operations
    public async Task<Sheet> CreateSheetFromTemplate(string projectName)
    {
        var token = HttpContext.Session.GetString("ss_token");
        if (token == null)
        {
            throw new Exception("Please sign in to SmartSheet first");
        }
        
        var smartsheet = new SmartsheetBuilder().SetAccessToken(token).Build();
        return await smartsheet.SheetResources.CreateSheetFromTemplate(...);
    }
}
```

### Phase 2: Embedding (1-2 days)
```html
<!-- In _ProjectDetails.cshtml -->
<!-- Session-based SmartSheet status -->
<div class="smartsheet-status">
    @if (HasSmartSheetSession())
    {
        <span>✓ SmartSheet Connected (@Session["ss_user"])</span>
    }
    else
    {
        <button onclick="connectSmartSheet()">Connect SmartSheet</button>
    }
</div>

@if (!string.IsNullOrEmpty(Model.SmartSheetId))
{
    <div class="smartsheet-container">
        <iframe src="https://publish.smartsheet.com/@Model.SmartSheetId" 
                width="100%" height="600" frameborder="0">
        </iframe>
    </div>
}

<script>
function connectSmartSheet() {
    // Open OAuth in popup
    const popup = window.open('/smartsheet/auth/login', 'smartsheet', 'width=500,height=600');
    
    // Check when complete
    const timer = setInterval(() => {
        if (popup.closed) {
            clearInterval(timer);
            location.reload(); // Refresh to show connected status
        }
    }, 1000);
}
</script>
```

### Phase 3: Sync (2-3 days)
```csharp
[HttpPost("api/smartsheet/webhook")]
public async Task<IActionResult> HandleWebhook([FromBody] WebhookPayload payload)
{
    // Simple sync logic
    await SyncProjectFromSmartSheet(payload.SheetId);
    return Ok();
}
```

### Phase 4: Template (1-2 days)
```csharp
public async Task<string> CreateProjectSmartSheet(string projectId)
{
    // Use session token for sheet creation
    var token = HttpContext.Session.GetString("ss_token");
    if (token == null) throw new Exception("Please connect SmartSheet first");
    
    var smartsheet = new SmartsheetBuilder().SetAccessToken(token).Build();
    var templateId = 8130468873457540; // Your template ID
    
    var newSheet = await CopyFromTemplate(templateId, projectId, smartsheet);
    await LinkProjectToSheet(projectId, newSheet.Id);
    return newSheet.Id;
}
```

## ✅ Resolved Questions

1. **Authentication Method**: ✅ Session-based OAuth for user attribution (no user storage)
2. **Template ID**: ✅ **8130468873457540**
3. **Sync Method**: ✅ Webhooks for real-time sync
4. **User Management**: ✅ No permanent user data storage in ShopBoss

## Configuration Requirements

```json
{
  "SmartSheet": {
    "ClientId": "oauth-client-id",
    "ClientSecret": "oauth-client-secret", 
    "RedirectUri": "https://localhost:5001/smartsheet/auth/callback",
    "ProjectTemplateId": 8130468873457540,
    "MasterProjectListId": 9876543210987654,
    "Cache": {
      "DefaultExpiration": "00:15:00"
    }
  }
}
```

## Success Criteria

- Users can link existing SmartSheets to ShopBoss projects
- Users can view/interact with SmartSheets within ShopBoss interface
- Basic data flows both directions with minimal conflicts
- New projects can create SmartSheets from template
- Solution works reliably for 4 concurrent users
- **Clean deprecation path** when in-house clone is ready

## Migration/Deprecation Plan

When the in-house SmartSheet clone is ready:
1. Export key data from SmartSheets back to ShopBoss
2. Disable new SmartSheet creation
3. Gradually migrate projects to in-house system
4. Remove SmartSheet integration code
5. Clean up session data (no permanent storage to remove)

---

**Key Principle: Maximum simplicity for a temporary solution - don't over-engineer!**