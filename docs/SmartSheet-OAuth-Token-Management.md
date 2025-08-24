# SmartSheet OAuth Token Lifecycle Management

## Overview

This document provides comprehensive guidance for managing OAuth tokens in the ShopBoss SmartSheet integration, including session management, token refresh strategies, and error handling patterns.

## Token Lifecycle Fundamentals

### Access Token Properties
- **Duration**: 7 days (604,799 seconds)
- **Scope**: Determined during OAuth authorization
- **Storage**: Session-only in ShopBoss implementation
- **Refresh**: Required before expiration using refresh token

### Refresh Token Properties
- **Duration**: Never expires (unless enterprise admin configures otherwise)
- **Usage**: One-time use - each refresh generates new access + refresh tokens
- **Storage**: Session-only in ShopBoss implementation
- **Security**: Must be kept secure, enables continued access

## Session-Based Implementation

### Token Storage Pattern
```csharp
public class SmartSheetSessionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SmartSheetSessionService> _logger;
    
    public SmartSheetSessionService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SmartSheetSessionService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public void StoreTokens(OAuthTokenResponse tokens)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null) return;

        // Store all token information
        session.SetString("ss_access_token", tokens.AccessToken);
        session.SetString("ss_refresh_token", tokens.RefreshToken);
        session.SetString("ss_token_expires", tokens.ExpiresAt.ToString("O"));
        session.SetString("ss_user_email", tokens.UserEmail);
        session.SetString("ss_user_name", tokens.UserName);
        session.SetString("ss_token_scope", tokens.Scope);
        
        _logger.LogInformation("Stored SmartSheet tokens for user {Email}, expires {ExpiresAt}", 
            tokens.UserEmail, tokens.ExpiresAt);
    }

    public SmartSheetTokenInfo? GetTokenInfo()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null) return null;

        var accessToken = session.GetString("ss_access_token");
        var refreshToken = session.GetString("ss_refresh_token");
        var expiresString = session.GetString("ss_token_expires");

        if (string.IsNullOrEmpty(accessToken) || 
            string.IsNullOrEmpty(refreshToken) || 
            string.IsNullOrEmpty(expiresString))
        {
            return null;
        }

        if (!DateTime.TryParse(expiresString, out var expiresAt))
        {
            _logger.LogWarning("Invalid token expiration format: {ExpiresString}", expiresString);
            return null;
        }

        return new SmartSheetTokenInfo
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            UserEmail = session.GetString("ss_user_email") ?? "",
            UserName = session.GetString("ss_user_name") ?? "",
            Scope = session.GetString("ss_token_scope") ?? ""
        };
    }

    public void ClearTokens()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session == null) return;

        session.Remove("ss_access_token");
        session.Remove("ss_refresh_token");
        session.Remove("ss_token_expires");
        session.Remove("ss_user_email");
        session.Remove("ss_user_name");
        session.Remove("ss_token_scope");
        
        _logger.LogInformation("Cleared SmartSheet session tokens");
    }
}

public class SmartSheetTokenInfo
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string UserEmail { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Scope { get; set; } = "";
    
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool WillExpireSoon => DateTime.UtcNow >= ExpiresAt.AddHours(-1); // 1 hour before expiry
    public TimeSpan TimeUntilExpiry => ExpiresAt - DateTime.UtcNow;
}
```

## Automatic Token Refresh

### Smart Client with Auto-Refresh
```csharp
public class SmartSheetClientService
{
    private readonly SmartSheetSessionService _sessionService;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmartSheetClientService> _logger;

    public SmartSheetClientService(
        SmartSheetSessionService sessionService,
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<SmartSheetClientService> logger)
    {
        _sessionService = sessionService;
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SmartsheetClient?> GetClientAsync()
    {
        var tokenInfo = _sessionService.GetTokenInfo();
        if (tokenInfo == null) return null;

        // Check if token needs refresh
        if (tokenInfo.WillExpireSoon)
        {
            _logger.LogInformation("Token expires soon ({TimeUntilExpiry}), refreshing...", 
                tokenInfo.TimeUntilExpiry);
                
            var refreshed = await RefreshTokenAsync(tokenInfo.RefreshToken);
            if (refreshed)
            {
                tokenInfo = _sessionService.GetTokenInfo();
                if (tokenInfo == null) return null;
            }
            else
            {
                _logger.LogWarning("Token refresh failed, clearing session");
                _sessionService.ClearTokens();
                return null;
            }
        }

        return new SmartsheetBuilder()
            .SetAccessToken(tokenInfo.AccessToken)
            .Build();
    }

    private async Task<bool> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.smartsheet.com/2.0/token");
            request.Headers.Add("Authorization", $"Bearer {refreshToken}");
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", _configuration["SmartSheet:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _configuration["SmartSheet:ClientSecret"])
            });

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (tokenResponse != null)
                {
                    // Calculate expiration time
                    tokenResponse.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    
                    // Store new tokens
                    _sessionService.StoreTokens(tokenResponse);
                    
                    _logger.LogInformation("Successfully refreshed SmartSheet token");
                    return true;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token refresh failed: {StatusCode} - {Content}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during token refresh");
        }

        return false;
    }
}

public class OAuthTokenResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string TokenType { get; set; } = "";
    public int ExpiresIn { get; set; }
    public string Scope { get; set; } = "";
    public DateTime ExpiresAt { get; set; } // Calculated field
    public string UserEmail { get; set; } = "";
    public string UserName { get; set; } = "";
}
```

## Proactive Token Management

### Background Token Refresh Service
```csharp
public class SmartSheetTokenMaintenanceService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SmartSheetTokenMaintenanceService> _logger;
    private Timer? _timer;

    public SmartSheetTokenMaintenanceService(
        IServiceProvider serviceProvider,
        ILogger<SmartSheetTokenMaintenanceService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SmartSheet Token Maintenance Service starting");
        
        // Check every hour for tokens that need refresh
        _timer = new Timer(CheckAndRefreshTokens, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        
        return Task.CompletedTask;
    }

    private async void CheckAndRefreshTokens(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionProvider = scope.ServiceProvider.GetRequiredService<ISessionProvider>();
            
            // Note: This is conceptual - actual implementation depends on session storage strategy
            var activeSessions = await sessionProvider.GetActiveSessionsAsync();
            
            foreach (var session in activeSessions)
            {
                var tokenInfo = GetTokenInfoFromSession(session);
                if (tokenInfo?.WillExpireSoon == true)
                {
                    _logger.LogInformation("Proactively refreshing token for {UserEmail}", tokenInfo.UserEmail);
                    // Refresh token logic here
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token maintenance");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SmartSheet Token Maintenance Service stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
```

## Error Handling Strategies

### Token Validation and Recovery
```csharp
public class SmartSheetTokenValidator
{
    private readonly SmartSheetClientService _clientService;
    private readonly ILogger<SmartSheetTokenValidator> _logger;

    public SmartSheetTokenValidator(
        SmartSheetClientService clientService,
        ILogger<SmartSheetTokenValidator> logger)
    {
        _clientService = clientService;
        _logger = logger;
    }

    public async Task<TokenValidationResult> ValidateAndRecoverAsync()
    {
        try
        {
            var client = await _clientService.GetClientAsync();
            if (client == null)
            {
                return TokenValidationResult.NoSession();
            }

            // Test token with minimal API call
            var user = await Task.Run(() => client.UserResources.GetCurrentUser());
            
            _logger.LogDebug("Token validation successful for user {Email}", user.Email);
            return TokenValidationResult.Valid(user.Email);
        }
        catch (Exception ex) when (IsUnauthorizedException(ex))
        {
            _logger.LogWarning("Token validation failed - unauthorized: {Message}", ex.Message);
            
            // Try to refresh token
            var client = await _clientService.GetClientAsync(); // Will attempt refresh
            if (client != null)
            {
                try
                {
                    var user = await Task.Run(() => client.UserResources.GetCurrentUser());
                    _logger.LogInformation("Token refresh and validation successful");
                    return TokenValidationResult.RefreshedAndValid(user.Email);
                }
                catch
                {
                    _logger.LogError("Token refresh succeeded but validation still failed");
                }
            }
            
            return TokenValidationResult.Invalid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return TokenValidationResult.Error(ex.Message);
        }
    }

    private static bool IsUnauthorizedException(Exception ex)
    {
        return ex.Message?.Contains("401") == true ||
               ex.Message?.Contains("Unauthorized") == true ||
               ex.Message?.Contains("invalid_token") == true;
    }
}

public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public bool WasRefreshed { get; set; }
    public bool HasSession { get; set; }
    public string? UserEmail { get; set; }
    public string? ErrorMessage { get; set; }

    public static TokenValidationResult Valid(string userEmail) => new()
    {
        IsValid = true,
        HasSession = true,
        UserEmail = userEmail
    };

    public static TokenValidationResult RefreshedAndValid(string userEmail) => new()
    {
        IsValid = true,
        WasRefreshed = true,
        HasSession = true,
        UserEmail = userEmail
    };

    public static TokenValidationResult Invalid() => new()
    {
        IsValid = false,
        HasSession = true,
        ErrorMessage = "Token is invalid and could not be refreshed"
    };

    public static TokenValidationResult NoSession() => new()
    {
        IsValid = false,
        HasSession = false,
        ErrorMessage = "No SmartSheet session found"
    };

    public static TokenValidationResult Error(string message) => new()
    {
        IsValid = false,
        HasSession = true,
        ErrorMessage = message
    };
}
```

## Middleware Integration

### Automatic Token Check Middleware
```csharp
public class SmartSheetTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SmartSheetTokenMiddleware> _logger;

    public SmartSheetTokenMiddleware(RequestDelegate next, ILogger<SmartSheetTokenMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, SmartSheetTokenValidator validator)
    {
        // Only check on SmartSheet-related requests
        if (context.Request.Path.StartsWithSegments("/api/smartsheet") ||
            context.Request.Path.StartsWithSegments("/Project"))
        {
            var validation = await validator.ValidateAndRecoverAsync();
            
            if (!validation.IsValid && validation.HasSession)
            {
                // Token invalid - redirect to re-auth
                _logger.LogWarning("Invalid SmartSheet token, redirecting to auth");
                context.Response.Redirect("/smartsheet/auth/login");
                return;
            }
            
            if (validation.WasRefreshed)
            {
                _logger.LogInformation("Token was refreshed for user {Email}", validation.UserEmail);
            }
        }

        await _next(context);
    }
}

// Register in Program.cs
app.UseMiddleware<SmartSheetTokenMiddleware>();
```

## Configuration and Registration

### Service Registration
```csharp
// In Program.cs
services.AddScoped<SmartSheetSessionService>();
services.AddScoped<SmartSheetClientService>();
services.AddScoped<SmartSheetTokenValidator>();
services.AddHostedService<SmartSheetTokenMaintenanceService>();

// Configure session
services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // 8 hour session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
```

### Configuration Settings
```json
{
  "SmartSheet": {
    "ClientId": "your-oauth-client-id",
    "ClientSecret": "your-oauth-client-secret",
    "RedirectUri": "https://localhost:5001/smartsheet/auth/callback",
    "TokenRefresh": {
      "RefreshBeforeExpiryHours": 1,
      "MaintenanceIntervalHours": 1,
      "MaxRetryAttempts": 3
    }
  },
  "Session": {
    "TimeoutHours": 8
  }
}
```

## Best Practices Summary

### Security
- **Never store tokens in database** - session-only storage prevents permanent compromise
- **Use HTTPS always** - protect tokens in transit
- **Log token operations** - but never log actual token values
- **Clear tokens on logout** - prevent session hijacking

### Performance  
- **Proactive refresh** - refresh 1 hour before expiry
- **Cache client instances** - reuse SmartsheetClient within request scope
- **Batch token checks** - don't validate on every API call

### Reliability
- **Graceful degradation** - handle token failures elegantly
- **Retry with backoff** - handle temporary network issues
- **Clear error messages** - help users understand auth state
- **Monitor token health** - log refresh patterns and failures

### User Experience
- **Transparent refresh** - users shouldn't notice token management
- **Clear auth state** - show connection status in UI
- **Easy re-auth** - simple path back to working state
- **Session persistence** - maintain auth across browser sessions

This token management strategy ensures reliable, secure access to SmartSheet while maintaining the session-only approach that aligns with ShopBoss's temporary integration strategy.