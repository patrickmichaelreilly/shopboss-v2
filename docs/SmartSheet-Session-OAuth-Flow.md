# SmartSheet Session-Based OAuth Flow

## Overview

This document details the session-based OAuth implementation for SmartSheet integration in ShopBoss. This approach provides user attribution in SmartSheet (for comments, file uploads) without requiring permanent user data storage in ShopBoss.

## Key Design Principles

1. **No Permanent User Storage** - All tokens stored only in ASP.NET session
2. **User Attribution** - Comments and files in SmartSheet show correct user
3. **Session Lifecycle** - Users re-authenticate each browser session
4. **Simplicity** - Minimal complexity for temporary solution

## OAuth Flow Implementation

### 1. Controller Setup

```csharp
[Route("smartsheet/auth")]
public class SmartSheetAuthController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmartSheetAuthController> _logger;
    private readonly HttpClient _httpClient;

    public SmartSheetAuthController(
        IConfiguration configuration,
        ILogger<SmartSheetAuthController> logger,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    [HttpGet("login")]
    public IActionResult Login(string returnUrl = "/Project")
    {
        // Generate state for CSRF protection
        var state = Guid.NewGuid().ToString();
        HttpContext.Session.SetString("oauth_state", state);
        HttpContext.Session.SetString("oauth_return_url", returnUrl);

        var authUrl = BuildAuthorizationUrl(state);
        
        _logger.LogInformation("Initiating SmartSheet OAuth flow with state: {State}", state);
        
        return Redirect(authUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string code, string state, string error = null)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("OAuth error: {Error}", error);
            TempData["Error"] = $"SmartSheet authentication failed: {error}";
            return RedirectToAction("Index", "Project");
        }

        // Validate state to prevent CSRF
        var sessionState = HttpContext.Session.GetString("oauth_state");
        if (state != sessionState)
        {
            _logger.LogError("OAuth state mismatch. Expected: {Expected}, Received: {Received}", sessionState, state);
            TempData["Error"] = "Authentication failed due to security check.";
            return RedirectToAction("Index", "Project");
        }

        try
        {
            // Exchange authorization code for tokens
            var tokens = await ExchangeCodeForTokens(code);
            
            // Store tokens in session only (no database)
            HttpContext.Session.SetString("ss_access_token", tokens.AccessToken);
            HttpContext.Session.SetString("ss_refresh_token", tokens.RefreshToken);
            HttpContext.Session.SetString("ss_token_expires", tokens.ExpiresAt.ToString("O"));
            HttpContext.Session.SetString("ss_user_email", tokens.UserEmail);
            HttpContext.Session.SetString("ss_user_name", tokens.UserName);
            
            _logger.LogInformation("SmartSheet OAuth completed for user: {Email}", tokens.UserEmail);
            
            // Clear OAuth state
            HttpContext.Session.Remove("oauth_state");
            
            var returnUrl = HttpContext.Session.GetString("oauth_return_url") ?? "/Project";
            HttpContext.Session.Remove("oauth_return_url");
            
            TempData["Success"] = $"Connected to SmartSheet as {tokens.UserName}";
            
            return Redirect(returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing OAuth flow");
            TempData["Error"] = "Failed to complete SmartSheet authentication.";
            return RedirectToAction("Index", "Project");
        }
    }

    [HttpPost("disconnect")]
    public IActionResult Disconnect()
    {
        // Clear all SmartSheet session data
        var sessionKeys = new[]
        {
            "ss_access_token", "ss_refresh_token", "ss_token_expires",
            "ss_user_email", "ss_user_name"
        };

        foreach (var key in sessionKeys)
        {
            HttpContext.Session.Remove(key);
        }

        _logger.LogInformation("SmartSheet session disconnected");
        TempData["Success"] = "Disconnected from SmartSheet";
        
        return RedirectToAction("Index", "Project");
    }

    private string BuildAuthorizationUrl(string state)
    {
        var clientId = _configuration["SmartSheet:ClientId"];
        var redirectUri = _configuration["SmartSheet:RedirectUri"];
        var scope = "READ_SHEETS,WRITE_SHEETS,CREATE_SHEETS,DELETE_SHEETS,SHARE_SHEETS,ADMIN_SHEETS";

        return $"https://app.smartsheet.com/b/authorize?" +
               $"response_type=code&" +
               $"client_id={Uri.EscapeDataString(clientId)}&" +
               $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
               $"scope={Uri.EscapeDataString(scope)}&" +
               $"state={Uri.EscapeDataString(state)}";
    }

    private async Task<SmartSheetTokens> ExchangeCodeForTokens(string code)
    {
        var clientId = _configuration["SmartSheet:ClientId"];
        var clientSecret = _configuration["SmartSheet:ClientSecret"];
        var redirectUri = _configuration["SmartSheet:RedirectUri"];

        // SmartSheet requires special hash for security
        var hash = ComputeSha256Hash($"{code}|{clientSecret}");

        var requestData = new Dictionary<string, string>
        {
            {"grant_type", "authorization_code"},
            {"code", code},
            {"client_id", clientId},
            {"hash", hash}
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.smartsheet.com/2.0/token")
        {
            Content = new FormUrlEncodedContent(requestData)
        };

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token exchange failed: {Status} - {Content}", response.StatusCode, responseContent);
            throw new Exception($"Token exchange failed: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        return new SmartSheetTokens
        {
            AccessToken = tokenResponse.GetProperty("access_token").GetString(),
            RefreshToken = tokenResponse.GetProperty("refresh_token").GetString(),
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.GetProperty("expires_in").GetInt32()),
            UserEmail = tokenResponse.GetProperty("user_email").GetString(),
            UserName = tokenResponse.GetProperty("user_name").GetString()
        };
    }

    private static string ComputeSha256Hash(string input)
    {
        using var sha256Hash = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}

public class SmartSheetTokens
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string UserEmail { get; set; }
    public string UserName { get; set; }
}
```

### 2. Service Layer

```csharp
public interface ISmartSheetSessionService
{
    bool HasActiveSession();
    Task<string> GetValidTokenAsync();
    SmartSheetUserInfo GetCurrentUser();
    Task<SmartsheetClient> GetAuthenticatedClientAsync();
    bool IsTokenExpired();
}

public class SmartSheetSessionService : ISmartSheetSessionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmartSheetSessionService> _logger;

    public SmartSheetSessionService(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<SmartSheetSessionService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool HasActiveSession()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var token = session?.GetString("ss_access_token");
        
        if (string.IsNullOrEmpty(token))
            return false;

        if (IsTokenExpired())
        {
            _logger.LogInformation("SmartSheet token expired");
            return false;
        }

        return true;
    }

    public async Task<string> GetValidTokenAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var token = session?.GetString("ss_access_token");

        if (string.IsNullOrEmpty(token))
        {
            throw new UnauthorizedAccessException("No SmartSheet session found. Please authenticate first.");
        }

        if (IsTokenExpired())
        {
            // Attempt to refresh token
            token = await RefreshTokenAsync();
        }

        return token;
    }

    public SmartSheetUserInfo GetCurrentUser()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        
        return new SmartSheetUserInfo
        {
            Email = session?.GetString("ss_user_email"),
            Name = session?.GetString("ss_user_name")
        };
    }

    public async Task<SmartsheetClient> GetAuthenticatedClientAsync()
    {
        var token = await GetValidTokenAsync();
        
        return new SmartsheetBuilder()
            .SetAccessToken(token)
            .Build();
    }

    public bool IsTokenExpired()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var expiresString = session?.GetString("ss_token_expires");

        if (string.IsNullOrEmpty(expiresString))
            return true;

        if (DateTime.TryParse(expiresString, out var expiresAt))
        {
            return DateTime.UtcNow >= expiresAt.AddMinutes(-5); // 5 minute buffer
        }

        return true;
    }

    private async Task<string> RefreshTokenAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var refreshToken = session?.GetString("ss_refresh_token");

        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedAccessException("No refresh token available. Please re-authenticate.");
        }

        try
        {
            var clientId = _configuration["SmartSheet:ClientId"];
            var clientSecret = _configuration["SmartSheet:ClientSecret"];
            
            var hash = ComputeSha256Hash($"{refreshToken}|{clientSecret}");

            var requestData = new Dictionary<string, string>
            {
                {"grant_type", "refresh_token"},
                {"refresh_token", refreshToken},
                {"client_id", clientId},
                {"hash", hash}
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.smartsheet.com/2.0/token")
            {
                Content = new FormUrlEncodedContent(requestData)
            };

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Token refresh failed: {Status} - {Content}", response.StatusCode, responseContent);
                throw new UnauthorizedAccessException("Failed to refresh SmartSheet token. Please re-authenticate.");
            }

            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            
            // Update session with new tokens
            var newToken = tokenResponse.GetProperty("access_token").GetString();
            var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString();
            var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

            session.SetString("ss_access_token", newToken);
            session.SetString("ss_refresh_token", newRefreshToken);
            session.SetString("ss_token_expires", DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"));

            _logger.LogInformation("SmartSheet token refreshed successfully");

            return newToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing SmartSheet token");
            
            // Clear session on refresh failure
            var sessionKeys = new[]
            {
                "ss_access_token", "ss_refresh_token", "ss_token_expires",
                "ss_user_email", "ss_user_name"
            };

            foreach (var key in sessionKeys)
            {
                session?.Remove(key);
            }

            throw new UnauthorizedAccessException("Token refresh failed. Please re-authenticate.");
        }
    }

    private static string ComputeSha256Hash(string input)
    {
        using var sha256Hash = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}

public class SmartSheetUserInfo
{
    public string Email { get; set; }
    public string Name { get; set; }
}
```

### 3. UI Integration

```html
<!-- In _Layout.cshtml -->
<div class="smartsheet-status-indicator">
    @if (smartSheetSession.HasActiveSession())
    {
        var user = smartSheetSession.GetCurrentUser();
        <span class="badge bg-success">
            <i class="fas fa-link"></i> SmartSheet: @user.Name
        </span>
        <form method="post" action="/smartsheet/auth/disconnect" class="d-inline">
            <button type="submit" class="btn btn-sm btn-link text-light p-0 ms-2" title="Disconnect">
                <i class="fas fa-times"></i>
            </button>
        </form>
    }
    else
    {
        <a href="/smartsheet/auth/login" class="badge bg-secondary text-decoration-none">
            <i class="fas fa-link"></i> Connect SmartSheet
        </a>
    }
</div>
```

### 4. JavaScript Helper

```javascript
// SmartSheet session management
window.SmartSheetSession = {
    connectInPopup: function(returnUrl) {
        const popup = window.open(
            `/smartsheet/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`,
            'smartsheet-oauth',
            'width=500,height=600,scrollbars=yes,resizable=yes'
        );

        // Monitor popup closure
        const timer = setInterval(() => {
            if (popup.closed) {
                clearInterval(timer);
                // Refresh current page to show connected status
                window.location.reload();
            }
        }, 1000);

        return false; // Prevent default link behavior
    },

    requireConnection: function(callback, errorMessage = 'Please connect SmartSheet first') {
        fetch('/api/smartsheet/session-status')
            .then(response => response.json())
            .then(data => {
                if (data.hasSession) {
                    callback();
                } else {
                    if (confirm(errorMessage + '\n\nConnect now?')) {
                        this.connectInPopup(window.location.pathname);
                    }
                }
            })
            .catch(error => {
                console.error('Error checking SmartSheet session:', error);
                alert('Unable to verify SmartSheet connection');
            });
    }
};
```

## Configuration Requirements

```json
{
  "SmartSheet": {
    "ClientId": "your-oauth-client-id-here",
    "ClientSecret": "your-oauth-client-secret-here",
    "RedirectUri": "https://your-domain.com/smartsheet/auth/callback",
    "ProjectTemplateId": "8130468873457540"
  }
}
```

## Session Management

### Session Keys Used
- `ss_access_token` - Current access token
- `ss_refresh_token` - Token for refreshing access
- `ss_token_expires` - Expiration timestamp (ISO format)
- `ss_user_email` - User's email address
- `ss_user_name` - User's display name
- `oauth_state` - CSRF protection (temporary)
- `oauth_return_url` - Post-auth redirect (temporary)

### Automatic Cleanup
- Tokens automatically cleared when session expires
- Failed token refresh clears all session data
- Manual disconnect clears all session data

## Security Considerations

1. **State Parameter** - CSRF protection during OAuth flow
2. **Hash Verification** - SmartSheet-specific security requirement
3. **Token Expiration** - Automatic refresh with fallback to re-auth
4. **Session Timeout** - Respects ASP.NET session configuration
5. **HTTPS Required** - OAuth redirects require secure connections

## Benefits of This Approach

- ✅ **No User Database** - Zero permanent storage
- ✅ **Proper Attribution** - Comments/files show correct user
- ✅ **Automatic Cleanup** - Session expiry handles token lifecycle
- ✅ **Simple Deployment** - No user management migrations
- ✅ **Security** - Standard OAuth 2.0 with SmartSheet requirements
- ✅ **Temporary Solution** - Perfect for bridge implementation

This session-based approach provides the user attribution you need for SmartSheet while maintaining the simplicity required for a temporary integration.