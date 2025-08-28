# SmartSheet Integration - Implementation Decisions

**Date:** August 25, 2025 (Updated)  
**Status:** Revised - Timeline-Centric Approach  
**Context:** Process composition platform foundation

## Project Context

This SmartSheet integration has evolved into a **process composition platform foundation** where SmartSheet provides the communication layer while ShopBoss orchestrates workflow and business entity management.

## MAJOR PIVOT: Timeline-Centric Approach

**Key Discovery:** SmartSheet grid templates are inconsistent - the chronological timeline of comments/attachments tells the real project story and represents activities within process chunks.

### Current Architecture: API-Based Timeline Processing
1. **Session-based OAuth** for user attribution ✅ IMPLEMENTED
2. **Timeline import and processing** - chronological comments/attachments ✅ IMPLEMENTED  
3. **SmartSheet cache service** for analysis and data extraction ✅ IMPLEMENTED
4. **TaskChunk entity system** for process composition (Phase 2)
5. **Memory-only caching** with SmartSheet API integration ✅ IMPLEMENTED

### What We've Built (Completed)

**Phase 1: OAuth & Project Linking ✅ COMPLETE**
- SmartSheetId field in Project model
- Session-based OAuth flow (no user storage)
- Project-SmartSheet linking functionality
- SmartSheet metadata display in project cards
- User attribution preserved in session

**Phase 2: Timeline Import & Processing ✅ COMPLETE**
- SmartSheetCacheService for data extraction and analysis
- Chronological import of comments and attachments
- ProjectEvent model with comprehensive fields
- SmartSheet analysis tools for research and debugging

### What We're Building Next

**Phase 3: TaskChunk Entity System (Current Focus)**
- TaskChunk as separate entity (not special ProjectEvents)  
- FK relationship: TaskChunk -> ProjectEvents
- Manual timeline organization UI for chunk discovery
- Server-side persistence of chunk relationships
- Foundation for process template system

**Phase 4: Process Composition (Future)**
- Convert discovered chunks into reusable templates
- Visual workflow composition interface
- Business entity flow between chunks
- Bi-directional sync with SmartSheet via OAuth

### What We're NOT Building (Avoiding Over-Engineering)
❌ iframe embedding (abandoned - no added value)
❌ Complex conflict resolution algorithms  
❌ Redis/distributed caching infrastructure  
❌ Sophisticated error recovery patterns  
❌ Circuit breakers and elaborate retry logic  
❌ Multiple template management system  
❌ Extensive fallback mechanisms  
❌ Advanced monitoring and alerting  
❌ Groups implemented as special ProjectEvents (architectural mistake)  

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

## Current Implementation Status (August 25, 2025)

### ✅ Completed Foundation
- **SmartSheet OAuth Integration**: Session-based authentication working
- **Project-Sheet Linking**: Manual ID entry with metadata display  
- **Timeline Import**: Chronological comments/attachments from SmartSheet
- **Cache Service**: SmartSheetCacheService for data analysis and extraction
- **Analysis Tools**: Research and debugging tools for SmartSheet data

### 🎯 Next Phase: TaskChunk Architecture
**Objective:** Enable manual organization of timeline events into reusable TaskChunk entities

**Critical Architecture Decision:** TaskChunk as separate entity with FK relationship to ProjectEvents (not special ProjectEvents)

**Implementation Approach:**
1. Create TaskChunk entity with proper relationships
2. Add drag-and-drop UI for event selection and chunk creation  
3. Server-side persistence of chunk organization
4. Foundation for future template system and process composition

### 📚 Key Lessons Learned
- **Timeline over Grid**: SmartSheet timelines more valuable than grid data
- **Entity Separation**: Proper entities beat clever workarounds
- **Process Discovery**: Manual organization reveals natural workflow patterns
- **API over iframe**: Direct API integration more flexible than embedding

---

**Evolution:** From temporary bridge solution to process composition platform foundation**