using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShopBoss.Web.Controllers;

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

    /// <summary>
    /// Initiate OAuth flow - redirect to SmartSheet authorization
    /// </summary>
    [HttpGet("smartsheet/auth/login")]
    public IActionResult Login()
    {
        try
        {
            var clientId = _configuration["SmartSheet:ClientId"];
            var redirectUri = _configuration["SmartSheet:RedirectUri"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("SmartSheet OAuth configuration missing");
                return BadRequest("SmartSheet not configured");
            }

            // Generate and store state parameter for CSRF protection
            var state = Guid.NewGuid().ToString();
            HttpContext.Session.SetString("oauth_state", state);

            // Build authorization URL
            var authUrl = "https://app.smartsheet.com/b/authorize" +
                         $"?response_type=code" +
                         $"&client_id={Uri.EscapeDataString(clientId)}" +
                         $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                         $"&scope=READ_SHEETS%20WRITE_SHEETS" +
                         $"&state={Uri.EscapeDataString(state)}";

            return Redirect(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating SmartSheet OAuth");
            return BadRequest("Error starting authentication");
        }
    }

    /// <summary>
    /// Handle OAuth callback from SmartSheet
    /// </summary>
    [HttpGet("smartsheet/auth/callback")]
    public async Task<IActionResult> Callback(string? code, string? state, string? error)
    {
        try
        {
            // Check for errors
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("SmartSheet OAuth error: {Error}", error);
                return View("OAuthError", error);
            }

            // Validate state parameter
            var storedState = HttpContext.Session.GetString("oauth_state");
            if (string.IsNullOrEmpty(storedState) || storedState != state)
            {
                _logger.LogWarning("Invalid OAuth state parameter");
                return BadRequest("Invalid request");
            }

            // Clear state from session
            HttpContext.Session.Remove("oauth_state");

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("No authorization code received from SmartSheet");
                return BadRequest("Authorization failed");
            }

            // Exchange code for tokens
            var tokens = await ExchangeCodeForTokens(code);
            if (tokens == null)
            {
                return BadRequest("Failed to obtain access token");
            }

            // Store tokens in session with expiry tracking
            var expiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn ?? 3600); // Default to 1 hour if not provided
            HttpContext.Session.SetString("ss_token", tokens.AccessToken);
            HttpContext.Session.SetString("ss_refresh", tokens.RefreshToken ?? "");
            HttpContext.Session.SetString("ss_user", tokens.UserEmail ?? "SmartSheet User");
            HttpContext.Session.SetString("ss_expires", expiresAt.ToString("O")); // ISO 8601 format

            _logger.LogInformation("SmartSheet OAuth successful for user {UserEmail}", tokens.UserEmail);

            // Return success page that will close the popup
            return View("OAuthSuccess");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling SmartSheet OAuth callback");
            return View("OAuthError", "An error occurred during authentication");
        }
    }

    /// <summary>
    /// Exchange authorization code for access tokens
    /// </summary>
    private async Task<SmartSheetTokens?> ExchangeCodeForTokens(string code)
    {
        try
        {
            var clientId = _configuration["SmartSheet:ClientId"];
            var clientSecret = _configuration["SmartSheet:ClientSecret"];
            var redirectUri = _configuration["SmartSheet:RedirectUri"];

            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!),
                new KeyValuePair<string, string>("redirect_uri", redirectUri!)
            });

            var response = await _httpClient.PostAsync("https://api.smartsheet.com/2.0/token", tokenRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SmartSheet token exchange failed: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                return null;
            }

            var tokenResponse = JsonSerializer.Deserialize<SmartSheetTokenResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Invalid token response from SmartSheet");
                return null;
            }

            // Get user info
            var userEmail = await GetUserEmail(tokenResponse.AccessToken);

            return new SmartSheetTokens
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                UserEmail = userEmail,
                ExpiresIn = tokenResponse.ExpiresIn
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for tokens");
            return null;
        }
    }

    /// <summary>
    /// Get user email from SmartSheet API
    /// </summary>
    private async Task<string?> GetUserEmail(string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.GetAsync("https://api.smartsheet.com/2.0/users/me");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var userInfo = JsonSerializer.Deserialize<SmartSheetUserResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return userInfo?.Email;
            }

            _logger.LogWarning("Failed to get user info from SmartSheet: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user email from SmartSheet");
            return null;
        }
    }

    /// <summary>
    /// Logout - clear session
    /// </summary>
    [HttpPost("smartsheet/auth/logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("ss_token");
        HttpContext.Session.Remove("ss_refresh");
        HttpContext.Session.Remove("ss_user");
        HttpContext.Session.Remove("ss_expires");

        return Json(new { success = true, message = "Logged out successfully" });
    }

    /// <summary>
    /// Check OAuth authentication status
    /// </summary>
    [HttpGet("smartsheet/auth/status")]
    public IActionResult GetStatus()
    {
        try
        {
            var token = HttpContext.Session.GetString("ss_token");
            var userEmail = HttpContext.Session.GetString("ss_user");
            var expiresString = HttpContext.Session.GetString("ss_expires");

            if (string.IsNullOrEmpty(token))
            {
                return Json(new { 
                    isAuthenticated = false, 
                    userEmail = (string?)null, 
                    expiresAt = (DateTime?)null 
                });
            }

            DateTime? expiresAt = null;
            if (!string.IsNullOrEmpty(expiresString) && DateTime.TryParse(expiresString, out var parsed))
            {
                expiresAt = parsed;
            }

            // Check if token is expired
            var isExpired = expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow;

            return Json(new { 
                isAuthenticated = !isExpired, 
                userEmail = userEmail, 
                expiresAt = expiresAt,
                isExpired = isExpired
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SmartSheet auth status");
            return Json(new { 
                isAuthenticated = false, 
                userEmail = (string?)null, 
                expiresAt = (DateTime?)null,
                error = "Error checking authentication status" 
            });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("smartsheet/auth/refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        try
        {
            var refreshToken = HttpContext.Session.GetString("ss_refresh");
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Json(new { success = false, message = "No refresh token available" });
            }

            var clientId = _configuration["SmartSheet:ClientId"];
            var clientSecret = _configuration["SmartSheet:ClientSecret"];

            var refreshRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!)
            });

            var response = await _httpClient.PostAsync("https://api.smartsheet.com/2.0/token", refreshRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SmartSheet token refresh failed: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                
                // Clear invalid tokens
                await Task.Run(() => Logout());
                
                return Json(new { success = false, message = "Token refresh failed. Please re-authenticate." });
            }

            var tokenResponse = JsonSerializer.Deserialize<SmartSheetTokenResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Invalid refresh token response from SmartSheet");
                return Json(new { success = false, message = "Invalid refresh response" });
            }

            // Update tokens in session
            var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600);
            HttpContext.Session.SetString("ss_token", tokenResponse.AccessToken);
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                HttpContext.Session.SetString("ss_refresh", tokenResponse.RefreshToken);
            }
            HttpContext.Session.SetString("ss_expires", expiresAt.ToString("O"));

            _logger.LogInformation("SmartSheet token refreshed successfully");
            
            return Json(new { success = true, message = "Token refreshed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing SmartSheet token");
            return Json(new { success = false, message = "Error refreshing token" });
        }
    }
}

// Response models for SmartSheet API
public class SmartSheetTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
    
    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }
}

public class SmartSheetUserResponse
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class SmartSheetTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public string? UserEmail { get; set; }
    public int? ExpiresIn { get; set; }
}