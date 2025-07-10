using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ScannerController : ControllerBase
{
    private readonly UniversalScannerService _scannerService;
    private readonly ILogger<ScannerController> _logger;

    public ScannerController(UniversalScannerService scannerService, ILogger<ScannerController> logger)
    {
        _scannerService = scannerService;
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessScan([FromBody] ScanRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var sessionId = request.SessionId ?? HttpContext.Session.Id;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await _scannerService.ProcessScanAsync(
                request.Barcode, 
                request.Station, 
                sessionId, 
                ipAddress);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scan request for barcode {Barcode} at station {Station}", 
                request.Barcode, request.Station);
            
            return StatusCode(500, new
            {
                success = false,
                message = "A system error occurred while processing the scan.",
                barcodeType = "Unknown",
                scanType = "api_error"
            });
        }
    }

    [HttpGet("recent-scans")]
    public async Task<IActionResult> GetRecentScans([FromQuery] string station, [FromQuery] int limit = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(station))
            {
                return BadRequest("Station parameter is required");
            }

            if (limit < 1 || limit > 50)
            {
                limit = 10;
            }

            var recentScans = await _scannerService.GetRecentScansAsync(station, limit);
            return Ok(recentScans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent scans for station {Station}", station);
            return StatusCode(500, "Failed to retrieve recent scans");
        }
    }

    [HttpPost("validate")]
    public IActionResult ValidateBarcode([FromBody] ValidationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var validationResult = _scannerService.ValidateBarcode(request.Barcode);
            return Ok(validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating barcode {Barcode}", request.Barcode);
            return StatusCode(500, "Failed to validate barcode");
        }
    }

    [HttpPost("analyze")]
    public IActionResult AnalyzeBarcode([FromBody] ValidationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var barcodeInfo = _scannerService.AnalyzeBarcode(request.Barcode);
            return Ok(barcodeInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing barcode {Barcode}", request.Barcode);
            return StatusCode(500, "Failed to analyze barcode");
        }
    }

    [HttpGet("help")]
    public IActionResult GetHelp()
    {
        var helpInfo = new
        {
            navigationCommands = new[]
            {
                new { command = "NAV:ADMIN", description = "Navigate to Admin Panel" },
                new { command = "NAV:CNC", description = "Navigate to CNC Station" },
                new { command = "NAV:SORTING", description = "Navigate to Sorting Station" },
                new { command = "NAV:ASSEMBLY", description = "Navigate to Assembly Station" },
                new { command = "NAV:SHIPPING", description = "Navigate to Shipping Station" },
                new { command = "NAV:HEALTH", description = "Navigate to Health Dashboard" },
                new { command = "NAV:BACKUP", description = "Navigate to Backup Management" },
                new { command = "NAV:RACKS", description = "Navigate to Rack Configuration" }
            },
            systemCommands = new[]
            {
                new { command = "CMD:REFRESH", description = "Refresh current page" },
                new { command = "CMD:HELP", description = "Show scanner help" },
                new { command = "CMD:CANCEL", description = "Cancel current operation" },
                new { command = "CMD:CLEAR", description = "Clear session data" },
                new { command = "CMD:LOGOUT", description = "Logout and return to admin" },
                new { command = "CMD:RECENT", description = "Show recent scans" },
                new { command = "CMD:SUMMARY", description = "Show work order summary" }
            },
            adminCommands = new[]
            {
                new { command = "ADMIN:BACKUP", description = "Create database backup" },
                new { command = "ADMIN:ARCHIVE", description = "Archive active work order" },
                new { command = "ADMIN:CLEARSESSIONS", description = "Clear all sessions" },
                new { command = "ADMIN:HEALTHCHECK", description = "Run system health check" },
                new { command = "ADMIN:AUDITLOG", description = "View audit log" }
            },
            stationCommands = new[]
            {
                new { command = "STN:CNC:RECENT", description = "Show recent nest sheets" },
                new { command = "STN:CNC:UNPROCESSED", description = "Show unprocessed nest sheets" },
                new { command = "STN:SORTING:RACKS", description = "Show rack summary" },
                new { command = "STN:SORTING:READY", description = "Show assembly readiness" },
                new { command = "STN:ASSEMBLY:QUEUE", description = "Show assembly queue" },
                new { command = "STN:ASSEMBLY:PROGRESS", description = "Show product progress" },
                new { command = "STN:SHIPPING:QUEUE", description = "Show shipping queue" },
                new { command = "STN:SHIPPING:PROGRESS", description = "Show work order progress" }
            },
            entityTypes = new[]
            {
                new { prefix = "NS-", type = "NestSheet", description = "Nest sheet barcode" },
                new { prefix = "PART-", type = "Part", description = "Part barcode" },
                new { prefix = "PROD-", type = "Product", description = "Product barcode" },
                new { prefix = "HW-", type = "Hardware", description = "Hardware barcode" },
                new { prefix = "DP-", type = "DetachedProduct", description = "Detached product barcode" }
            }
        };

        return Ok(helpInfo);
    }
}

public class ScanRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Barcode { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Station { get; set; } = string.Empty;

    [StringLength(100)]
    public string? SessionId { get; set; }
}

public class ValidationRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Barcode { get; set; } = string.Empty;
}