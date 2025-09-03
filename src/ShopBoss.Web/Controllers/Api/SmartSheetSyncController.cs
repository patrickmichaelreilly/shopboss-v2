using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;

namespace ShopBoss.Web.Controllers.Api;

[ApiController]
[Route("api/smartsheet/sync")]
public class SmartSheetSyncController : ControllerBase
{
    private readonly SmartSheetSyncService _syncService;
    private readonly ILogger<SmartSheetSyncController> _logger;

    public SmartSheetSyncController(
        SmartSheetSyncService syncService,
        ILogger<SmartSheetSyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Trigger sync for a specific project
    /// </summary>
    [HttpPost("{projectId}")]
    public async Task<IActionResult> SyncProject(string projectId)
    {
        try
        {
            // Validate SmartSheet authentication
            var accessToken = HttpContext.Session.GetString("ss_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Ok(new { 
                    success = false, 
                    message = "Smartsheet authentication required. Please connect to Smartsheet first." 
                });
            }

            // Check if token is expired
            var expiresString = HttpContext.Session.GetString("ss_expires");
            if (!string.IsNullOrEmpty(expiresString) && 
                DateTime.TryParse(expiresString, out var expiresAt) &&
                expiresAt <= DateTime.UtcNow)
            {
                return Ok(new { 
                    success = false, 
                    message = "Smartsheet token expired. Please re-authenticate." 
                });
            }

            // Perform sync
            var result = await _syncService.SyncProjectEventsAsync(projectId, accessToken);

            if (result.Success)
            {
                return Ok(new { 
                    success = true, 
                    message = $"Sync completed: {result.Created} created, {result.Updated} updated",
                    created = result.Created,
                    updated = result.Updated,
                    sheetId = result.SheetId
                });
            }
            else
            {
                return Ok(new { 
                    success = false, 
                    message = result.Message 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing project {ProjectId}", projectId);
            return Ok(new { 
                success = false, 
                message = "An error occurred during sync. Please try again." 
            });
        }
    }

    /// <summary>
    /// Sync FROM Smartsheet - read sheet and update ShopBoss
    /// </summary>
    [HttpPost("sync-from/{projectId}")]
    public async Task<IActionResult> SyncFromSmartsheet(string projectId)
    {
        try
        {
            // Validate SmartSheet authentication
            var accessToken = HttpContext.Session.GetString("ss_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Ok(new { 
                    success = false, 
                    message = "Smartsheet authentication required. Please connect to Smartsheet first." 
                });
            }

            // Check if token is expired
            var expiresString = HttpContext.Session.GetString("ss_expires");
            if (!string.IsNullOrEmpty(expiresString) && 
                DateTime.TryParse(expiresString, out var expiresAt) &&
                expiresAt <= DateTime.UtcNow)
            {
                return Ok(new { 
                    success = false, 
                    message = "Smartsheet token expired. Please re-authenticate." 
                });
            }

            // Perform inbound sync
            var result = await _syncService.SyncFromSmartsheetAsync(projectId, accessToken);

            if (result.Success)
            {
                return Ok(new { 
                    success = true, 
                    message = $"Sync from Smartsheet completed: {result.Updated} updated",
                    updated = result.Updated
                });
            }
            else
            {
                return Ok(new { 
                    success = false, 
                    message = result.Message 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing from Smartsheet for project {ProjectId}", projectId);
            return Ok(new { 
                success = false, 
                message = "An error occurred during sync from Smartsheet. Please try again." 
            });
        }
    }

    /// <summary>
    /// Get sync status for a project
    /// </summary>
    [HttpGet("status/{projectId}")]
    public IActionResult GetSyncStatus(string projectId)
    {
        try
        {
            // TODO: Implement sync status checking
            // For now, just return basic info about synced events
            
            return Ok(new { 
                success = true,
                message = "Status check not yet implemented",
                isSynced = false,
                lastSyncTime = (DateTime?)null,
                syncedEvents = 0,
                totalEvents = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking sync status for project {ProjectId}", projectId);
            return Ok(new { 
                success = false, 
                message = "Error checking sync status" 
            });
        }
    }

}