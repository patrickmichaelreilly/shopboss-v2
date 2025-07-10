using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShopBoss.Web.Controllers;
using ShopBoss.Web.Data;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Models;
using ShopBoss.Web.Models.Scanner;

namespace ShopBoss.Web.Services;

public class UniversalScannerService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<UniversalScannerService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly AuditTrailService _auditTrail;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Cache keys
    private const string CACHE_KEY_NESTSHEETS = "scanner_nestsheets";
    private const string CACHE_KEY_PARTS = "scanner_parts";
    private const string CACHE_KEY_PRODUCTS = "scanner_products";
    private const string CACHE_KEY_HARDWARE = "scanner_hardware";
    private const string CACHE_KEY_DETACHED = "scanner_detached";
    
    // Cache duration
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public UniversalScannerService(
        ShopBossDbContext context,
        ILogger<UniversalScannerService> logger,
        IMemoryCache cache,
        IHubContext<StatusHub> hubContext,
        AuditTrailService auditTrail,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _hubContext = hubContext;
        _auditTrail = auditTrail;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ScanResult> ProcessScanAsync(string? barcode, string station, string? sessionId = null, string? ipAddress = null)
    {
        try
        {
            // Validate barcode input
            var validationResult = ValidateBarcode(barcode);
            if (!validationResult.IsValid)
            {
                await LogScanAsync(barcode ?? "[empty]", station, false, validationResult.ErrorMessage, sessionId, ipAddress);
                return new ScanResult
                {
                    Success = false,
                    Message = validationResult.ErrorMessage,
                    BarcodeType = BarcodeType.Unknown,
                    Suggestions = validationResult.Suggestions,
                    ScanType = "validation_error"
                };
            }

            var barcodeInfo = AnalyzeBarcode(barcode!);
            
            // Handle command barcodes
            if (barcodeInfo.IsCommand)
            {
                return await ProcessCommandAsync(barcodeInfo, station, sessionId, ipAddress);
            }

            // Handle entity barcodes
            return await ProcessEntityScanAsync(barcodeInfo, station, sessionId, ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scan for barcode {Barcode} at station {Station}", barcode, station);
            
            await LogScanAsync(barcode ?? "[empty]", station, false, $"System error: {ex.Message}", sessionId, ipAddress);
            
            return new ScanResult
            {
                Success = false,
                Message = "A system error occurred while processing the scan. Please try again.",
                BarcodeType = BarcodeType.Unknown,
                ScanType = "system_error"
            };
        }
    }

    public ValidationResult ValidateBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "❌ No barcode provided. Please scan a barcode.",
                Suggestions = new List<string>
                {
                    "Make sure your scanner is properly connected",
                    "Try scanning a different barcode",
                    "Check that the barcode is clearly visible"
                }
            };
        }

        var trimmed = barcode.Trim();
        
        if (trimmed.Length < 3)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "❌ Barcode too short. Please scan a valid barcode.",
                Suggestions = new List<string>
                {
                    "Ensure the entire barcode is scanned",
                    "Try scanning from a closer distance",
                    "Check for barcode damage or obstruction"
                }
            };
        }

        if (trimmed.Length > 100)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "❌ Barcode too long. Please scan a valid barcode.",
                Suggestions = new List<string>
                {
                    "Ensure you're scanning the correct barcode",
                    "Check for multiple barcodes being scanned together"
                }
            };
        }

        return new ValidationResult { IsValid = true };
    }

    public BarcodeInfo AnalyzeBarcode(string barcode)
    {
        var cleanBarcode = barcode.Trim();
        var info = new BarcodeInfo { CleanBarcode = cleanBarcode };

        // Check for command barcodes first
        if (IsNavigationCommand(cleanBarcode))
        {
            info.Type = BarcodeType.NavigationCommand;
            info.CommandString = cleanBarcode;
            info.ParsedCommand = ParseNavigationCommand(cleanBarcode);
            return info;
        }

        if (IsSystemCommand(cleanBarcode))
        {
            info.Type = BarcodeType.SystemCommand;
            info.CommandString = cleanBarcode;
            info.ParsedCommand = ParseSystemCommand(cleanBarcode);
            return info;
        }

        if (IsAdminCommand(cleanBarcode))
        {
            info.Type = BarcodeType.AdminCommand;
            info.CommandString = cleanBarcode;
            info.ParsedCommand = ParseAdminCommand(cleanBarcode);
            return info;
        }

        if (IsStationCommand(cleanBarcode))
        {
            info.Type = BarcodeType.StationCommand;
            info.CommandString = cleanBarcode;
            info.ParsedCommand = ParseStationCommand(cleanBarcode);
            return info;
        }

        // Station will determine how to process - no entity type needed
        return info;
    }

    private async Task<ScanResult> ProcessCommandAsync(BarcodeInfo barcodeInfo, string station, string? sessionId, string? ipAddress)
    {
        try
        {
            CommandResult commandResult = barcodeInfo.Type switch
            {
                BarcodeType.NavigationCommand => await ExecuteNavigationCommand((NavigationCommand)barcodeInfo.ParsedCommand!, station),
                BarcodeType.SystemCommand => await ExecuteSystemCommand((SystemCommand)barcodeInfo.ParsedCommand!, station),
                BarcodeType.AdminCommand => await ExecuteAdminCommand((AdminCommand)barcodeInfo.ParsedCommand!, station),
                BarcodeType.StationCommand => await ExecuteStationCommand((StationCommand)barcodeInfo.ParsedCommand!, station),
                _ => new CommandResult { Success = false, Message = "Unknown command type" }
            };

            await LogScanAsync(barcodeInfo.CleanBarcode, station, commandResult.Success, commandResult.Message, sessionId, ipAddress, 
                $"Command: {barcodeInfo.CommandString}");

            // Broadcast command execution
            await _hubContext.Clients.All.SendAsync("CommandExecuted", new
            {
                station = station,
                command = barcodeInfo.CommandString,
                success = commandResult.Success,
                message = commandResult.Message,
                timestamp = DateTime.UtcNow
            });

            return new ScanResult
            {
                Success = commandResult.Success,
                Message = commandResult.Message,
                BarcodeType = barcodeInfo.Type,
                RedirectUrl = commandResult.RedirectUrl,
                AdditionalData = commandResult.Data != null ? new Dictionary<string, object> { ["commandData"] = commandResult.Data } : new(),
                ScanType = "command"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {Command} at station {Station}", barcodeInfo.CommandString, station);
            
            return new ScanResult
            {
                Success = false,
                Message = $"Failed to execute command: {ex.Message}",
                BarcodeType = barcodeInfo.Type,
                ScanType = "command_error"
            };
        }
    }

    private async Task<ScanResult> ProcessEntityScanAsync(BarcodeInfo barcodeInfo, string station, string? sessionId, string? ipAddress)
    {
        // Station-based delegation - let each station handle barcodes with their existing logic
        return station.ToUpper() switch
        {
            "CNC" => await DelegateToCncStation(barcodeInfo.CleanBarcode, sessionId, ipAddress),
            "SORTING" => await DelegateToSortingStation(barcodeInfo.CleanBarcode, sessionId, ipAddress),
            "ASSEMBLY" => await DelegateToAssemblyStation(barcodeInfo.CleanBarcode, sessionId, ipAddress),
            "SHIPPING" => await DelegateToShippingStation(barcodeInfo.CleanBarcode, sessionId, ipAddress),
            _ => new ScanResult
            {
                Success = false,
                Message = $"⚠️ Station '{station}' not supported for barcode scanning",
                ScanType = "unsupported_station"
            }
        };
    }


    // Command detection methods
    private bool IsNavigationCommand(string barcode) =>
        barcode.StartsWith("NAV-", StringComparison.OrdinalIgnoreCase) ||
        barcode.StartsWith("GOTO-", StringComparison.OrdinalIgnoreCase);

    private bool IsSystemCommand(string barcode) =>
        barcode.StartsWith("CMD-", StringComparison.OrdinalIgnoreCase) ||
        barcode.StartsWith("SYS-", StringComparison.OrdinalIgnoreCase);

    private bool IsAdminCommand(string barcode) =>
        barcode.StartsWith("ADMIN-", StringComparison.OrdinalIgnoreCase) ||
        barcode.StartsWith("ADM-", StringComparison.OrdinalIgnoreCase);

    private bool IsStationCommand(string barcode) =>
        barcode.StartsWith("STN-", StringComparison.OrdinalIgnoreCase) ||
        barcode.StartsWith("STATION-", StringComparison.OrdinalIgnoreCase);

    // Command parsing methods
    private NavigationCommand ParseNavigationCommand(string barcode)
    {
        var command = barcode.Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToUpperInvariant();
        
        return command switch
        {
            "ADMIN" => NavigationCommand.GoToAdmin,
            "CNC" => NavigationCommand.GoToCnc,
            "SORTING" => NavigationCommand.GoToSorting,
            "ASSEMBLY" => NavigationCommand.GoToAssembly,
            "SHIPPING" => NavigationCommand.GoToShipping,
            "HEALTH" => NavigationCommand.GoToHealthDashboard,
            "BACKUP" => NavigationCommand.GoToBackupManagement,
            "RACKS" => NavigationCommand.GoToRackConfiguration,
            _ => NavigationCommand.Unknown
        };
    }

    private SystemCommand ParseSystemCommand(string barcode)
    {
        var command = barcode.Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToUpperInvariant();
        
        return command switch
        {
            "REFRESH" => SystemCommand.Refresh,
            "HELP" => SystemCommand.Help,
            "CANCEL" => SystemCommand.Cancel,
            "CLEAR" => SystemCommand.ClearSession,
            "LOGOUT" => SystemCommand.Logout,
            "RECENT" => SystemCommand.ShowRecentScans,
            "SUMMARY" => SystemCommand.ShowWorkOrderSummary,
            _ => SystemCommand.Unknown
        };
    }

    private AdminCommand ParseAdminCommand(string barcode)
    {
        var command = barcode.Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToUpperInvariant();
        
        return command switch
        {
            "BACKUP" => AdminCommand.CreateBackup,
            "ARCHIVE" => AdminCommand.ArchiveActiveWorkOrder,
            "CLEARSESSIONS" => AdminCommand.ClearAllSessions,
            "HEALTHCHECK" => AdminCommand.RunHealthCheck,
            "AUDITLOG" => AdminCommand.ViewAuditLog,
            _ => AdminCommand.Unknown
        };
    }

    private StationCommand ParseStationCommand(string barcode)
    {
        var parts = barcode.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return StationCommand.Unknown;

        var station = parts[1].ToUpperInvariant();
        var command = parts[2].ToUpperInvariant();
        
        return (station, command) switch
        {
            ("CNC", "RECENT") => StationCommand.ShowRecentNestSheets,
            ("CNC", "UNPROCESSED") => StationCommand.ShowUnprocessedNestSheets,
            ("SORTING", "RACKS") => StationCommand.ShowRackSummary,
            ("SORTING", "READY") => StationCommand.ShowAssemblyReadiness,
            ("ASSEMBLY", "QUEUE") => StationCommand.ShowAssemblyQueue,
            ("ASSEMBLY", "PROGRESS") => StationCommand.ShowProductProgress,
            ("SHIPPING", "QUEUE") => StationCommand.ShowShippingQueue,
            ("SHIPPING", "PROGRESS") => StationCommand.ShowWorkOrderProgress,
            _ => StationCommand.Unknown
        };
    }

    private async Task LogScanAsync(string barcode, string station, bool success, string result, string? sessionId, string? ipAddress, string details = "")
    {
        try
        {
            await _auditTrail.LogScanAsync(barcode, station, success, result, null, null, null, sessionId, ipAddress, details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log scan audit trail for barcode {Barcode} at station {Station}", barcode, station);
        }
    }


    private async Task<ScanResult> ProcessUnknownScan(string barcode, string station, string? sessionId, string? ipAddress)
    {
        _logger.LogWarning("Unknown barcode scanned: {Barcode} at station {Station}", barcode, station);
        
        // Audit the unknown scan
        await _auditTrail.LogAsync(
            action: "Scan",
            entityType: "Unknown",
            entityId: barcode,
            details: $"Unknown barcode '{barcode}' scanned at {station} station",
            station: station,
            ipAddress: ipAddress,
            sessionId: sessionId
        );

        // Try to provide helpful suggestions based on barcode pattern
        var suggestions = GenerateSuggestions(barcode);
        
        return new ScanResult
        {
            Success = false,
            BarcodeType = BarcodeType.Unknown,
            Message = $"⚠️ Barcode '{barcode}' not recognized",
            AdditionalData = new Dictionary<string, object>
            {
                ["details"] = "This barcode doesn't match any known nest sheets, parts, products, or commands in the system."
            },
            Suggestions = suggestions
        };
    }

    private List<string> GenerateSuggestions(string barcode)
    {
        var suggestions = new List<string>();
        
        // Check if it looks like a command barcode but has wrong format
        if (barcode.Contains(":"))
        {
            suggestions.Add("Command barcodes now use hyphen separators (not colons)");
            suggestions.Add("Try: NAV-ADMIN, CMD-HELP, or ADMIN-BACKUP");
        }
        else if (barcode.Contains("-") && (barcode.StartsWith("NAV") || barcode.StartsWith("CMD") || barcode.StartsWith("ADMIN") || barcode.StartsWith("STN")))
        {
            suggestions.Add("Command barcodes should start with NAV-, CMD-, ADMIN-, or STN-");
            suggestions.Add("Try: NAV-ADMIN, CMD-HELP, or ADMIN-BACKUP");
        }
        // Check if it might be a typo of a nest sheet barcode
        else if (barcode.Length > 5 && (barcode.StartsWith("B") || barcode.StartsWith("N")))
        {
            suggestions.Add("This looks like it might be a nest sheet barcode");
            suggestions.Add("Verify the barcode is from an imported work order");
        }
        // Generic suggestions
        else
        {
            suggestions.Add("Ensure this barcode is from an imported work order");
            suggestions.Add("Try scanning a nest sheet from the active work order");
            suggestions.Add("Use CMD:HELP to see available command barcodes");
        }
        
        return suggestions;
    }

    // Command execution methods
    private async Task<CommandResult> ExecuteNavigationCommand(NavigationCommand command, string station)
    {
        var result = new CommandResult { CommandType = "navigation" };

        try
        {
            result.RedirectUrl = command switch
            {
                NavigationCommand.GoToAdmin => "/Admin/Index",
                NavigationCommand.GoToCnc => "/Cnc/Index",
                NavigationCommand.GoToSorting => "/Sorting/Index",
                NavigationCommand.GoToAssembly => "/Assembly/Index",
                NavigationCommand.GoToShipping => "/Shipping/Index",
                NavigationCommand.GoToHealthDashboard => "/Admin/HealthDashboard",
                NavigationCommand.GoToBackupManagement => "/Admin/BackupManagement",
                NavigationCommand.GoToRackConfiguration => "/Admin/RackConfiguration",
                _ => null
            };

            if (result.RedirectUrl != null)
            {
                result.Success = true;
                result.Message = $"🧭 Navigating to {command.ToString().Replace("GoTo", "")} station...";
                
                _logger.LogInformation("Navigation command executed: {Command} from station {Station}", command, station);
            }
            else
            {
                result.Success = false;
                result.Message = $"❌ Unknown navigation command: {command}";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing navigation command {Command} from station {Station}", command, station);
            result.Success = false;
            result.Message = "❌ Navigation failed due to system error";
            return result;
        }
    }

    private async Task<CommandResult> ExecuteSystemCommand(SystemCommand command, string station)
    {
        var result = new CommandResult { CommandType = "system" };

        try
        {
            switch (command)
            {
                case SystemCommand.Refresh:
                    result.Success = true;
                    result.Message = "🔄 Refreshing page...";
                    result.RequiresRefresh = true;
                    break;

                case SystemCommand.Help:
                    result.Success = true;
                    result.Message = "ℹ️ Scanner Help: Use NAV: commands to navigate, CMD: for system actions, ADMIN: for admin tasks";
                    result.Data = new
                    {
                        navigation = new[] { "NAV:ADMIN", "NAV:CNC", "NAV:SORTING", "NAV:ASSEMBLY", "NAV:SHIPPING" },
                        system = new[] { "CMD:REFRESH", "CMD:HELP", "CMD:CANCEL", "CMD:CLEAR" },
                        admin = new[] { "ADMIN:BACKUP", "ADMIN:ARCHIVE", "ADMIN:HEALTHCHECK" }
                    };
                    break;

                case SystemCommand.Cancel:
                    result.Success = true;
                    result.Message = "❌ Operation cancelled";
                    break;

                case SystemCommand.ClearSession:
                    result.Success = true;
                    result.Message = "🧹 Session cleared";
                    // Note: Actual session clearing would be handled by the controller
                    break;

                case SystemCommand.Logout:
                    result.Success = true;
                    result.Message = "👋 Logging out...";
                    result.RedirectUrl = "/Admin/Index";
                    break;

                case SystemCommand.ShowRecentScans:
                    var recentScans = await GetRecentScansAsync(station);
                    result.Success = true;
                    result.Message = $"📊 Found {recentScans.Count} recent scans";
                    result.Data = recentScans;
                    break;

                case SystemCommand.ShowWorkOrderSummary:
                    var summary = await GetWorkOrderSummaryAsync();
                    result.Success = true;
                    result.Message = "📋 Work Order Summary";
                    result.Data = summary;
                    break;

                default:
                    result.Success = false;
                    result.Message = $"❌ Unknown system command: {command}";
                    break;
            }

            _logger.LogInformation("System command executed: {Command} from station {Station}", command, station);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing system command {Command} from station {Station}", command, station);
            result.Success = false;
            result.Message = "❌ System command failed";
            return result;
        }
    }

    private async Task<CommandResult> ExecuteAdminCommand(AdminCommand command, string station)
    {
        var result = new CommandResult { CommandType = "admin" };

        try
        {
            switch (command)
            {
                case AdminCommand.CreateBackup:
                    result.Success = true;
                    result.Message = "💾 Backup command received - redirecting to backup management";
                    result.RedirectUrl = "/Admin/BackupManagement";
                    break;

                case AdminCommand.ArchiveActiveWorkOrder:
                    result.Success = true;
                    result.Message = "📦 Archive command received - redirecting to admin panel";
                    result.RedirectUrl = "/Admin/Index";
                    break;

                case AdminCommand.RunHealthCheck:
                    result.Success = true;
                    result.Message = "❤️ Health check command received - redirecting to health dashboard";
                    result.RedirectUrl = "/Admin/HealthDashboard";
                    break;

                case AdminCommand.ViewAuditLog:
                    var recentAudits = await GetRecentAuditLogsAsync();
                    result.Success = true;
                    result.Message = $"📝 Found {recentAudits.Count} recent audit entries";
                    result.Data = recentAudits;
                    break;

                case AdminCommand.ClearAllSessions:
                    result.Success = true;
                    result.Message = "🧹 Clear sessions command received - requires admin confirmation";
                    result.RedirectUrl = "/Admin/Index";
                    break;

                default:
                    result.Success = false;
                    result.Message = $"❌ Unknown admin command: {command}";
                    break;
            }

            _logger.LogInformation("Admin command executed: {Command} from station {Station}", command, station);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing admin command {Command} from station {Station}", command, station);
            result.Success = false;
            result.Message = "❌ Admin command failed";
            return result;
        }
    }

    private async Task<CommandResult> ExecuteStationCommand(StationCommand command, string station)
    {
        var result = new CommandResult { CommandType = "station" };

        try
        {
            switch (command)
            {
                case StationCommand.ShowRecentNestSheets:
                    var recentNestSheets = await GetRecentNestSheetsAsync();
                    result.Success = true;
                    result.Message = $"📄 Found {recentNestSheets.Count} recent nest sheets";
                    result.Data = recentNestSheets;
                    break;

                case StationCommand.ShowUnprocessedNestSheets:
                    var unprocessedNestSheets = await GetUnprocessedNestSheetsAsync();
                    result.Success = true;
                    result.Message = $"⏳ Found {unprocessedNestSheets.Count} unprocessed nest sheets";
                    result.Data = unprocessedNestSheets;
                    break;

                case StationCommand.ShowRackSummary:
                    var rackSummary = await GetRackSummaryAsync();
                    result.Success = true;
                    result.Message = "🗂️ Rack Summary";
                    result.Data = rackSummary;
                    break;

                case StationCommand.ShowAssemblyReadiness:
                    var readyForAssembly = await GetAssemblyReadinessAsync();
                    result.Success = true;
                    result.Message = $"🔧 {readyForAssembly.Count} products ready for assembly";
                    result.Data = readyForAssembly;
                    break;

                case StationCommand.ShowAssemblyQueue:
                    var assemblyQueue = await GetAssemblyQueueAsync();
                    result.Success = true;
                    result.Message = $"📋 {assemblyQueue.Count} items in assembly queue";
                    result.Data = assemblyQueue;
                    break;

                case StationCommand.ShowProductProgress:
                    var productProgress = await GetProductProgressAsync();
                    result.Success = true;
                    result.Message = "📊 Product Progress Summary";
                    result.Data = productProgress;
                    break;

                case StationCommand.ShowShippingQueue:
                    var shippingQueue = await GetShippingQueueAsync();
                    result.Success = true;
                    result.Message = $"📦 {shippingQueue.Count} items ready for shipping";
                    result.Data = shippingQueue;
                    break;

                case StationCommand.ShowWorkOrderProgress:
                    var workOrderProgress = await GetWorkOrderProgressAsync();
                    result.Success = true;
                    result.Message = "📈 Work Order Progress";
                    result.Data = workOrderProgress;
                    break;

                default:
                    result.Success = false;
                    result.Message = $"❌ Unknown station command: {command}";
                    break;
            }

            _logger.LogInformation("Station command executed: {Command} from station {Station}", command, station);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing station command {Command} from station {Station}", command, station);
            result.Success = false;
            result.Message = "❌ Station command failed";
            return result;
        }
    }

    // Helper methods for data retrieval
    public async Task<List<object>> GetRecentScansAsync(string station, int limit = 10)
    {
        try
        {
            var recentScans = await _context.ScanHistory
                .Where(s => s.Station == station)
                .OrderByDescending(s => s.Timestamp)
                .Take(limit)
                .Select(s => new
                {
                    s.Barcode,
                    s.Station,
                    Success = s.IsSuccessful,
                    Result = s.ErrorMessage ?? "Success",
                    ScanDate = s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            return recentScans.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent scans for station {Station}", station);
            return new List<object>();
        }
    }

    private async Task<object> GetWorkOrderSummaryAsync()
    {
        try
        {
            var activeWorkOrderId = _httpContextAccessor.HttpContext?.Session?.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return new { message = "No active work order" };
            }

            var workOrder = await _context.WorkOrders
                .Include(w => w.Products)
                .Include(w => w.DetachedProducts)
                .FirstOrDefaultAsync(w => w.Id == activeWorkOrderId);

            if (workOrder == null)
            {
                return new { message = "Active work order not found" };
            }

            return new
            {
                workOrder.Id,
                workOrder.Name,
                ProductCount = workOrder.Products.Count,
                DetachedProductCount = workOrder.DetachedProducts.Count,
                CreatedDate = workOrder.ImportedDate.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order summary");
            return new { error = "Failed to retrieve work order summary" };
        }
    }

    private async Task<List<object>> GetRecentAuditLogsAsync(int limit = 20)
    {
        try
        {
            var recentAudits = await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(limit)
                .Select(a => new
                {
                    a.EntityType,
                    a.Action,
                    a.Station,
                    a.Details,
                    Timestamp = a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            return recentAudits.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent audit logs");
            return new List<object>();
        }
    }

    private async Task<List<object>> GetRecentNestSheetsAsync(int limit = 10)
    {
        try
        {
            var recentNestSheets = await _context.NestSheets
                .OrderByDescending(n => n.CreatedDate)
                .Take(limit)
                .Select(n => new
                {
                    n.Id,
                    n.Name,
                    n.Material,
                    n.IsProcessed,
                    CreatedDate = n.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    ProcessedDate = n.ProcessedDate.HasValue ? n.ProcessedDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : null
                })
                .ToListAsync();

            return recentNestSheets.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent nest sheets");
            return new List<object>();
        }
    }

    private async Task<List<object>> GetUnprocessedNestSheetsAsync()
    {
        try
        {
            var unprocessedNestSheets = await _context.NestSheets
                .Where(n => !n.IsProcessed)
                .OrderBy(n => n.CreatedDate)
                .Select(n => new
                {
                    n.Id,
                    n.Name,
                    n.Material,
                    n.Barcode,
                    CreatedDate = n.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            return unprocessedNestSheets.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unprocessed nest sheets");
            return new List<object>();
        }
    }

    private async Task<object> GetRackSummaryAsync()
    {
        try
        {
            var rackSummary = await _context.StorageRacks
                .Include(r => r.Bins)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Location,
                    TotalBins = r.Bins.Count,
                    OccupiedBins = r.Bins.Count(b => !string.IsNullOrEmpty(b.PartId)),
                    UtilizationPercentage = r.Bins.Count > 0 ? 
                        Math.Round((double)r.Bins.Count(b => !string.IsNullOrEmpty(b.PartId)) / r.Bins.Count * 100, 1) : 0
                })
                .ToListAsync();

            return (object)rackSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rack summary");
            return new { error = "Failed to retrieve rack summary" };
        }
    }

    private async Task<List<object>> GetAssemblyReadinessAsync()
    {
        try
        {
            // This is a simplified version - actual logic would be more complex
            var readyProducts = await _context.Products
                .Where(p => p.Parts.All(part => part.Status == PartStatus.Sorted))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.ProductNumber,
                    PartsCount = p.Parts.Count,
                    ReadyForAssembly = true
                })
                .ToListAsync();

            return readyProducts.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assembly readiness");
            return new List<object>();
        }
    }

    private async Task<List<object>> GetAssemblyQueueAsync()
    {
        try
        {
            var assemblyQueue = await _context.Products
                .Where(p => p.Parts.All(part => part.Status == PartStatus.Sorted))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.ProductNumber,
                    Status = "Ready for Assembly",
                    PartsReady = p.Parts.Count(part => part.Status == PartStatus.Sorted),
                    TotalParts = p.Parts.Count
                })
                .ToListAsync();

            return assemblyQueue.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assembly queue");
            return new List<object>();
        }
    }

    private async Task<object> GetProductProgressAsync()
    {
        try
        {
            var productStats = await _context.Parts
                .GroupBy(p => p.Status)
                .Select(g => new
                {
                    Status = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToListAsync();

            return new
            {
                ProductStats = productStats,
                TotalProducts = productStats.Sum(s => s.Count)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product progress");
            return new { error = "Failed to retrieve product progress" };
        }
    }

    private async Task<List<object>> GetShippingQueueAsync()
    {
        try
        {
            var shippingQueue = await _context.Products
                .Where(p => p.Parts.All(part => part.Status == PartStatus.Assembled))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.ProductNumber,
                    Status = "Ready for Shipping",
                    ReadyForShipping = true
                })
                .ToListAsync();

            return shippingQueue.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shipping queue");
            return new List<object>();
        }
    }

    private async Task<object> GetWorkOrderProgressAsync()
    {
        try
        {
            var activeWorkOrderId = _httpContextAccessor.HttpContext?.Session?.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return new { message = "No active work order" };
            }

            var progress = await _context.Parts
                .Where(p => p.Product != null && p.Product.WorkOrderId == activeWorkOrderId)
                .GroupBy(p => p.Status)
                .Select(g => new
                {
                    Status = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToListAsync();

            return new
            {
                WorkOrderId = activeWorkOrderId,
                PartProgress = progress,
                TotalParts = progress.Sum(p => p.Count)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order progress");
            return new { error = "Failed to retrieve work order progress" };
        }
    }

    // Station delegation methods - delegate to existing controller logic
    private async Task<ScanResult> DelegateToCncStation(string barcode, string? sessionId, string? ipAddress)
    {
        try
        {
            // Create a controller instance to call ProcessNestSheet
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "No HTTP context available",
                    ScanType = "system_error"
                };
            }

            // Create CncController instance manually with required dependencies
            var context = httpContext.RequestServices.GetRequiredService<ShopBossDbContext>();
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<CncController>>();
            var hubContext = httpContext.RequestServices.GetRequiredService<IHubContext<StatusHub>>();
            var auditTrail = httpContext.RequestServices.GetRequiredService<AuditTrailService>();
            
            var cncController = new CncController(context, logger, hubContext, auditTrail, this);
            cncController.ControllerContext = new ControllerContext 
            { 
                HttpContext = httpContext 
            };
            
            var actionResult = await cncController.ProcessNestSheet(barcode);
            
            if (actionResult is JsonResult jsonResult && jsonResult.Value != null)
            {
                var resultData = jsonResult.Value;
                var success = GetPropertyValue(resultData, "success")?.ToString()?.ToLower() == "true";
                var message = GetPropertyValue(resultData, "message")?.ToString() ?? "Processing completed";
                
                return new ScanResult
                {
                    Success = success,
                    Message = message,
                    ScanType = success ? "processed" : "error",
                    AdditionalData = new Dictionary<string, object> { ["originalResult"] = resultData }
                };
            }

            return new ScanResult
            {
                Success = false,
                Message = "Unexpected response from CNC controller",
                ScanType = "system_error"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delegating to CNC station for barcode {Barcode}", barcode);
            return new ScanResult
            {
                Success = false,
                Message = $"Error processing barcode: {ex.Message}",
                ScanType = "system_error"
            };
        }
    }

    private async Task<ScanResult> DelegateToSortingStation(string barcode, string? sessionId, string? ipAddress)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "No HTTP context available",
                    ScanType = "system_error"
                };
            }

            // Create SortingController instance manually with required dependencies
            var context = httpContext.RequestServices.GetRequiredService<ShopBossDbContext>();
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<SortingController>>();
            var hubContext = httpContext.RequestServices.GetRequiredService<IHubContext<StatusHub>>();
            var auditTrail = httpContext.RequestServices.GetRequiredService<AuditTrailService>();
            var sortingRuleService = httpContext.RequestServices.GetRequiredService<SortingRuleService>();
            var partFilteringService = httpContext.RequestServices.GetRequiredService<PartFilteringService>();
            
            var sortingController = new SortingController(context, logger, hubContext, auditTrail, sortingRuleService, partFilteringService);
            sortingController.ControllerContext = new ControllerContext 
            { 
                HttpContext = httpContext 
            };
            
            var actionResult = await sortingController.ScanPart(barcode);
            
            if (actionResult is JsonResult jsonResult && jsonResult.Value != null)
            {
                var resultData = jsonResult.Value;
                var success = GetPropertyValue(resultData, "success")?.ToString()?.ToLower() == "true";
                var message = GetPropertyValue(resultData, "message")?.ToString() ?? "Processing completed";
                
                return new ScanResult
                {
                    Success = success,
                    Message = message,
                    ScanType = success ? "processed" : "error",
                    AdditionalData = new Dictionary<string, object> { ["originalResult"] = resultData }
                };
            }

            return new ScanResult
            {
                Success = false,
                Message = "Unexpected response from Sorting controller",
                ScanType = "system_error"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delegating to Sorting station for barcode {Barcode}", barcode);
            return new ScanResult
            {
                Success = false,
                Message = $"Error processing barcode: {ex.Message}",
                ScanType = "system_error"
            };
        }
    }

    private async Task<ScanResult> DelegateToAssemblyStation(string barcode, string? sessionId, string? ipAddress)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "No HTTP context available",
                    ScanType = "system_error"
                };
            }

            // Create AssemblyController instance manually with required dependencies
            var context = httpContext.RequestServices.GetRequiredService<ShopBossDbContext>();
            var workOrderService = httpContext.RequestServices.GetRequiredService<WorkOrderService>();
            var sortingRuleService = httpContext.RequestServices.GetRequiredService<SortingRuleService>();
            var partFilteringService = httpContext.RequestServices.GetRequiredService<PartFilteringService>();
            var auditTrail = httpContext.RequestServices.GetRequiredService<AuditTrailService>();
            var shippingService = httpContext.RequestServices.GetRequiredService<ShippingService>();
            var hubContext = httpContext.RequestServices.GetRequiredService<IHubContext<StatusHub>>();
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<AssemblyController>>();
            
            var assemblyController = new AssemblyController(context, workOrderService, sortingRuleService, partFilteringService, auditTrail, shippingService, hubContext, logger);
            assemblyController.ControllerContext = new ControllerContext 
            { 
                HttpContext = httpContext 
            };
            
            var actionResult = await assemblyController.ScanPartForAssembly(barcode);
            
            if (actionResult is JsonResult jsonResult && jsonResult.Value != null)
            {
                var resultData = jsonResult.Value;
                var success = GetPropertyValue(resultData, "success")?.ToString()?.ToLower() == "true";
                var message = GetPropertyValue(resultData, "message")?.ToString() ?? "Processing completed";
                
                return new ScanResult
                {
                    Success = success,
                    Message = message,
                    ScanType = success ? "processed" : "error",
                    AdditionalData = new Dictionary<string, object> { ["originalResult"] = resultData }
                };
            }

            return new ScanResult
            {
                Success = false,
                Message = "Unexpected response from Assembly controller",
                ScanType = "system_error"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delegating to Assembly station for barcode {Barcode}", barcode);
            return new ScanResult
            {
                Success = false,
                Message = $"Error processing barcode: {ex.Message}",
                ScanType = "system_error"
            };
        }
    }

    private async Task<ScanResult> DelegateToShippingStation(string barcode, string? sessionId, string? ipAddress)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "No HTTP context available",
                    ScanType = "system_error"
                };
            }

            // Create ShippingController instance manually with required dependencies
            var context = httpContext.RequestServices.GetRequiredService<ShopBossDbContext>();
            var workOrderService = httpContext.RequestServices.GetRequiredService<WorkOrderService>();
            var shippingService = httpContext.RequestServices.GetRequiredService<ShippingService>();
            var auditTrail = httpContext.RequestServices.GetRequiredService<AuditTrailService>();
            var hubContext = httpContext.RequestServices.GetRequiredService<IHubContext<StatusHub>>();
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<ShippingController>>();
            
            var shippingController = new ShippingController(context, workOrderService, shippingService, auditTrail, hubContext, logger);
            shippingController.ControllerContext = new ControllerContext 
            { 
                HttpContext = httpContext 
            };
            
            // Try shipping methods in sequence until one succeeds
            
            // Try ScanProduct first
            try
            {
                var actionResult = await shippingController.ScanProduct(barcode);
                if (actionResult is JsonResult jsonResult && jsonResult.Value != null)
                {
                    var resultData = jsonResult.Value;
                    var success = GetPropertyValue(resultData, "success")?.ToString()?.ToLower() == "true";
                    if (success)
                    {
                        var message = GetPropertyValue(resultData, "message")?.ToString() ?? "Product processed";
                        return new ScanResult
                        {
                            Success = true,
                            Message = message,
                            ScanType = "processed",
                            AdditionalData = new Dictionary<string, object> { ["originalResult"] = resultData, ["method"] = "ScanProduct" }
                        };
                    }
                }
            }
            catch
            {
                // Continue to next method
            }

            // Try ScanPart if ScanProduct failed
            try
            {
                var actionResult = await shippingController.ScanPart(barcode);
                if (actionResult is JsonResult jsonResult && jsonResult.Value != null)
                {
                    var resultData = jsonResult.Value;
                    var success = GetPropertyValue(resultData, "success")?.ToString()?.ToLower() == "true";
                    if (success)
                    {
                        var message = GetPropertyValue(resultData, "message")?.ToString() ?? "Part processed";
                        return new ScanResult
                        {
                            Success = true,
                            Message = message,
                            ScanType = "processed",
                            AdditionalData = new Dictionary<string, object> { ["originalResult"] = resultData, ["method"] = "ScanPart" }
                        };
                    }
                }
            }
            catch
            {
                // Continue to next method
            }

            // Try ScanHardware if others failed
            try
            {
                var actionResult = await shippingController.ScanHardware(barcode);
                if (actionResult is JsonResult jsonResult && jsonResult.Value != null)
                {
                    var resultData = jsonResult.Value;
                    var success = GetPropertyValue(resultData, "success")?.ToString()?.ToLower() == "true";
                    if (success)
                    {
                        var message = GetPropertyValue(resultData, "message")?.ToString() ?? "Hardware processed";
                        return new ScanResult
                        {
                            Success = true,
                            Message = message,
                            ScanType = "processed",
                            AdditionalData = new Dictionary<string, object> { ["originalResult"] = resultData, ["method"] = "ScanHardware" }
                        };
                    }
                }
            }
            catch
            {
                // Continue to next method
            }

            // Try ScanDetachedProduct as last resort
            try
            {
                var actionResult = await shippingController.ScanDetachedProduct(barcode);
                if (actionResult is JsonResult jsonResult && jsonResult.Value != null)
                {
                    var resultData = jsonResult.Value;
                    var success = GetPropertyValue(resultData, "success")?.ToString()?.ToLower() == "true";
                    var message = GetPropertyValue(resultData, "message")?.ToString() ?? "Detached product processed";
                    
                    return new ScanResult
                    {
                        Success = success,
                        Message = message,
                        ScanType = success ? "processed" : "error",
                        AdditionalData = new Dictionary<string, object> { ["originalResult"] = resultData, ["method"] = "ScanDetachedProduct" }
                    };
                }
            }
            catch
            {
                // All methods failed
            }

            return new ScanResult
            {
                Success = false,
                Message = $"Barcode '{barcode}' not recognized by any shipping method",
                ScanType = "not_found"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delegating to Shipping station for barcode {Barcode}", barcode);
            return new ScanResult
            {
                Success = false,
                Message = $"Error processing barcode: {ex.Message}",
                ScanType = "system_error"
            };
        }
    }

    private object? GetPropertyValue(object obj, string propertyName)
    {
        try
        {
            var property = obj.GetType().GetProperty(propertyName);
            return property?.GetValue(obj);
        }
        catch
        {
            return null;
        }
    }
}