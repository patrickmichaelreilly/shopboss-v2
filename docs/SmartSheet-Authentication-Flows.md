# SmartSheet Authentication Flows for Embedded Applications

## Authentication Methods Overview

SmartSheet provides several authentication approaches suitable for different integration scenarios. Understanding these flows is critical for embedding SmartSheet functionality in ShopBoss.

## 1. Personal Access Tokens (Current ShopBoss Implementation)

### Description
Direct API access using a personal access token from a SmartSheet account.

### How It Works
```javascript
const smartsheet = require('smartsheet').createClient({
  accessToken: 'YOUR_PERSONAL_ACCESS_TOKEN'
});
```

### Characteristics
- ✅ **Simple**: No OAuth flow required
- ✅ **Server-to-Server**: Perfect for backend operations
- ✅ **Long-lived**: Doesn't expire (until manually revoked)
- ⚠️ **Single User**: Limited to one SmartSheet account's permissions
- ⚠️ **Security**: Token represents full account access

### Best For
- Backend data synchronization (current SmartSheet import)
- Server-side operations
- Machine-to-machine communication

### Security Configuration
```json
// appsettings.json
{
  "SmartSheet": {
    "AccessToken": "your-personal-token-here",
    "BaseUrl": "https://api.smartsheet.com/2.0/"
  }
}
```

## 2. OAuth 2.0 Flow (For User-Specific Access)

### Description
3-legged OAuth flow enabling user-specific permissions and consent.

### OAuth Flow Steps
1. **App Registration**: Register app with SmartSheet to get `client_id` and `client_secret`
2. **Authorization Request**: Redirect user to SmartSheet login
3. **User Consent**: User authorizes app permissions
4. **Authorization Code**: SmartSheet returns authorization code
5. **Token Exchange**: Exchange code for access/refresh tokens

### Implementation Details
```javascript
// Step 1: Redirect to SmartSheet
const authUrl = `https://app.smartsheet.com/b/authorize?` +
  `response_type=code&` +
  `client_id=${clientId}&` +
  `redirect_uri=${redirectUri}&` +
  `scope=${scopes}&` +
  `state=${state}`;

// Step 2: Handle callback and exchange code
const tokenResponse = await fetch('https://api.smartsheet.com/2.0/token', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    grant_type: 'authorization_code',
    client_id: clientId,
    hash: sha256(authCode + '|' + clientSecret),
    code: authCode
  })
});
```

### Token Lifecycle
- **Access Token**: Expires after 7 days (604,799 seconds)
- **Refresh Token**: Used to obtain new access tokens
- **Automatic Refresh**: Required for long-running applications

### Characteristics
- ✅ **User-Specific**: Each user's SmartSheet permissions
- ✅ **Secure**: User controls what app can access
- ✅ **Refreshable**: Long-term access via refresh tokens
- ⚠️ **Complex**: Requires OAuth implementation
- ⚠️ **User Interaction**: Initial authorization required

### Best For
- Multi-user applications
- User-specific sheet access
- Applications requiring user consent

## 3. Embedded Authentication Patterns

### For iframe Embedding
```html
<!-- Published sheets - no authentication required -->
<iframe 
  src="https://publish.smartsheet.com/[sheet-id]" 
  width="100%" 
  height="600">
</iframe>
```

### For API-Driven Embedding
```javascript
// Use stored tokens for API calls
const sheet = await smartsheet.sheets.getSheet({
  id: sheetId,
  accessToken: userToken // User-specific or service token
});

// Render custom grid with sheet data
renderCustomGrid(sheet.rows, sheet.columns);
```

## 4. ShopBoss Integration Patterns

### Current Implementation (Personal Access Token)
```csharp
// SmartSheetImportService.cs
var smartsheet = new SmartsheetBuilder()
    .SetAccessToken(_configuration["SmartSheet:AccessToken"])
    .Build();
```

### Recommended Approach: Session-Based OAuth

#### Session-Only OAuth Pattern (No User Storage)
- Use **session-based OAuth** for user attribution in SmartSheet
- Display **published sheets** in iframes for user interaction
- **No permanent user data storage** in ShopBoss

```csharp
public class SmartSheetSessionService
{
    // Check if user has active SmartSheet session
    public bool HasSmartSheetSession()
    {
        return HttpContext.Session.GetString("ss_token") != null;
    }
    
    // OAuth flow storing tokens only in session
    public async Task<IActionResult> HandleOAuthCallback(string code)
    {
        var tokens = await ExchangeCodeForTokens(code);
        
        // Store ONLY in session memory (no database)
        HttpContext.Session.SetString("ss_token", tokens.AccessToken);
        HttpContext.Session.SetString("ss_refresh", tokens.RefreshToken);
        HttpContext.Session.SetString("ss_user", tokens.UserEmail);
        
        return RedirectToAction("Index", "Project");
    }
    
    // Use session token for sheet operations
    public async Task<Sheet> CreateSheetFromTemplate(string templateId, string projectName)
    {
        var token = HttpContext.Session.GetString("ss_token");
        if (token == null)
        {
            throw new UnauthorizedAccessException("Please connect SmartSheet first");
        }
        
        var smartsheet = new SmartsheetBuilder().SetAccessToken(token).Build();
        return await smartsheet.SheetResources.CreateSheetFromTemplate(...);
    }
    
    // Generate iframe URL for embedding
    public string GetEmbedUrl(string sheetId)
    {
        return $"https://publish.smartsheet.com/{sheetId}";
    }
}
```

## 5. Security Considerations

### Personal Access Tokens
- Store in **secure configuration** (Azure Key Vault, etc.)
- **Never expose** in client-side code
- **Rotate regularly** for security
- **Monitor usage** for suspicious activity

### Session-Based OAuth Tokens
- Store **tokens only in session** (no database persistence)
- Implement **token refresh** logic for session duration
- Handle **token revocation** gracefully
- **Respect user consent** and permissions
- **Automatic cleanup** when session expires

### iframe Security
- Validate **published sheet URLs**
- Implement **CSP headers** to prevent XSS
- Consider **sandbox attributes** for iframes
- Monitor for **data exposure** in published content

## 6. Recommendations for ShopBoss

### Session-Based OAuth Implementation (Final Decision)
1. Implement **session-only OAuth** for user attribution in SmartSheet
2. Add **published sheet embedding** via iframe  
3. Implement **webhook receivers** for real-time updates
4. **No permanent user data storage** in ShopBoss database

### Implementation Priority
```
High Priority:  Session-based OAuth + iframe embedding
High Priority:  Webhook integration for real-time sync  
N/A:           No permanent user management system needed
```

## 7. Code Examples

### Session-Based OAuth Pattern
```csharp
public class ProjectService
{
    public async Task<Project> GetProjectWithSmartSheetAsync(string projectId)
    {
        var project = await _context.Projects.FindAsync(projectId);
        
        if (!string.IsNullOrEmpty(project.SmartSheetId) && HasSmartSheetSession())
        {
            // Sync latest data using session token
            await _smartSheetSessionService.SyncProjectData(project.SmartSheetId);
        }
        
        return project;
    }
}
```

### Session-Based Pattern with UI
```html
<!-- SmartSheet Connection Status -->
<div class="smartsheet-status mb-3">
    @if (HasSmartSheetSession())
    {
        <div class="alert alert-success">
            <i class="fas fa-check-circle"></i>
            SmartSheet Connected: @Session["ss_user"]
        </div>
    }
    else
    {
        <div class="alert alert-info">
            <button class="btn btn-info" onclick="connectSmartSheet()">
                <i class="fas fa-link"></i> Connect SmartSheet
            </button>
            <small class="d-block mt-1">Connect to create sheets and sync data</small>
        </div>
    }
</div>

@if (!string.IsNullOrEmpty(project.SmartSheetId))
{
    <div class="smartsheet-embed">
        <iframe src="https://publish.smartsheet.com/@project.SmartSheetId"
                width="100%" height="600" frameborder="0">
        </iframe>
    </div>
}
```

This authentication strategy provides a clear path from current implementation to enhanced embedding capabilities while maintaining security and user experience.