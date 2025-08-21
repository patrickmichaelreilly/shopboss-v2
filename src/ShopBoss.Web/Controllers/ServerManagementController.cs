using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Services;
using ShopBoss.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ShopBoss.Web.Controllers;

public class ServerManagementController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<ServerManagementController> _logger;
    private readonly BackupService _backupService;
    private readonly SystemHealthMonitor _healthMonitor;
    private readonly AuditTrailService _auditTrailService;
    private readonly IHubContext<StatusHub> _hubContext;

    public ServerManagementController(
        ShopBossDbContext context, 
        ILogger<ServerManagementController> logger,
        BackupService backupService,
        SystemHealthMonitor healthMonitor,
        AuditTrailService auditTrailService,
        IHubContext<StatusHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _backupService = backupService;
        _healthMonitor = healthMonitor;
        _auditTrailService = auditTrailService;
        _hubContext = hubContext;
    }

    // Main Dashboard - consolidates health and backup overview
    public async Task<IActionResult> Index()
    {
        try
        {
            // Get current health status
            var healthStatus = await _healthMonitor.GetOrCreateHealthStatusAsync();
            var currentMetrics = await _healthMonitor.CheckSystemHealthAsync();
            
            // Get backup configuration and recent backups
            var backupConfig = await _backupService.GetBackupConfigurationAsync();
            var recentBackups = await _backupService.GetRecentBackupsAsync(5); // Just top 5 for dashboard
            
            // Get recent health-related audit logs
            var recentHealthLogs = await _context.AuditLogs
                .Where(log => log.EntityType == "SystemHealth" || log.EntityType == "System" || log.EntityType == "BackupConfiguration")
                .OrderByDescending(log => log.Timestamp)
                .Take(10)
                .ToListAsync();

            var viewModel = new ServerManagementDashboardViewModel
            {
                CurrentHealthStatus = healthStatus,
                CurrentMetrics = currentMetrics,
                BackupConfiguration = backupConfig,
                RecentBackups = recentBackups,
                RecentActivityLogs = recentHealthLogs,
                PageTitle = "Server Management Dashboard"
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading server management dashboard");
            TempData["ErrorMessage"] = "An error occurred while loading the server management dashboard.";
            return View(new ServerManagementDashboardViewModel { PageTitle = "Server Management Dashboard" });
        }
    }

    // Health Monitoring Section
    public async Task<IActionResult> HealthDashboard()
    {
        try
        {
            // Get current health status from database
            var healthStatus = await _healthMonitor.GetOrCreateHealthStatusAsync();
            
            // Get recent health metrics
            var currentMetrics = await _healthMonitor.CheckSystemHealthAsync();
            
            // Get recent audit logs for health-related activities
            var recentHealthLogs = await _context.AuditLogs
                .Where(log => log.EntityType == "SystemHealth" || log.EntityType == "System")
                .OrderByDescending(log => log.Timestamp)
                .Take(20)
                .ToListAsync();

            var viewModel = new HealthDashboardViewModel
            {
                CurrentHealthStatus = healthStatus,
                CurrentMetrics = currentMetrics,
                RecentHealthLogs = recentHealthLogs,
                PageTitle = "System Health Dashboard"
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading health dashboard");
            TempData["ErrorMessage"] = "An error occurred while loading the health dashboard.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> RunHealthCheck()
    {
        try
        {
            // Force an immediate health check
            var metrics = await _healthMonitor.CheckSystemHealthAsync();
            await _healthMonitor.UpdateHealthStatusAsync(metrics);
            
            await _auditTrailService.LogAsync(
                "SystemHealth",
                "ManualHealthCheck",
                "System",
                "1",
                "ServerManagement",
                "Manual health check initiated from server management dashboard");

            // Notify via SignalR
            await _hubContext.Clients.Group("server-monitoring")
                .SendAsync("HealthCheckCompleted", new
                {
                    OverallStatus = metrics.OverallStatus.ToString(),
                    Timestamp = DateTime.Now,
                    Station = "ServerManagement"
                });

            return Json(new { success = true, message = "Health check completed successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running manual health check");
            return Json(new { success = false, message = "An error occurred while running the health check." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetHealthMetrics()
    {
        try
        {
            // Get fresh health metrics instead of relying solely on database
            var healthMetrics = await _healthMonitor.CheckSystemHealthAsync();
            
            // Also try to get stored status for additional data
            var healthStatus = await _healthMonitor.GetOrCreateHealthStatusAsync();
            
            var response = new
            {
                overallStatus = healthMetrics.OverallStatus.ToString(),
                databaseStatus = healthMetrics.DatabaseStatus.ToString(),
                diskSpaceStatus = healthMetrics.DiskSpaceStatus.ToString(),
                memoryStatus = healthMetrics.MemoryStatus.ToString(),
                responseTimeStatus = healthMetrics.ResponseTimeStatus.ToString(),
                availableDiskSpaceGB = healthMetrics.AvailableDiskSpaceGB,
                totalDiskSpaceGB = healthMetrics.TotalDiskSpaceGB,
                diskUsagePercentage = healthMetrics.TotalDiskSpaceGB > 0 ? 
                    ((healthMetrics.TotalDiskSpaceGB - healthMetrics.AvailableDiskSpaceGB) / healthMetrics.TotalDiskSpaceGB) * 100 : 0,
                memoryUsagePercentage = healthMetrics.MemoryUsagePercentage,
                averageResponseTimeMs = healthMetrics.AverageResponseTimeMs,
                databaseConnectionTimeMs = healthMetrics.DatabaseConnectionTimeMs,
                activeWorkOrderCount = healthMetrics.ActiveWorkOrderCount,
                totalPartsCount = healthMetrics.TotalPartsCount,
                lastHealthCheck = healthMetrics.LastHealthCheck,
                errorMessage = healthMetrics.ErrorMessage
            };

            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health metrics");
            return Json(new { 
                overallStatus = "Error",
                error = "Failed to retrieve health metrics",
                errorMessage = ex.Message,
                lastHealthCheck = DateTime.Now
            });
        }
    }

    // Backup Management Section  
    [HttpGet]
    public async Task<IActionResult> BackupManagement()
    {
        try
        {
            var config = await _backupService.GetBackupConfigurationAsync();
            var recentBackups = await _backupService.GetRecentBackupsAsync(20);
            
            var viewModel = new BackupManagementViewModel
            {
                Configuration = config,
                RecentBackups = recentBackups
            };
            
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading backup management page");
            TempData["ErrorMessage"] = "An error occurred while loading the backup management page.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateBackupConfiguration(BackupConfiguration configuration)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var success = await _backupService.UpdateBackupConfigurationAsync(configuration);
                if (success)
                {
                    TempData["SuccessMessage"] = "Backup configuration updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update backup configuration.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid backup configuration settings.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating backup configuration");
            TempData["ErrorMessage"] = "An error occurred while updating the backup configuration.";
        }
        
        return RedirectToAction(nameof(BackupManagement));
    }

    [HttpPost]
    public async Task<IActionResult> CreateManualBackup()
    {
        try
        {
            var backupResult = await _backupService.CreateBackupAsync(BackupType.Manual);
            
            if (backupResult.IsSuccessful)
            {
                TempData["SuccessMessage"] = $"Manual backup created successfully. File: {Path.GetFileName(backupResult.FilePath)}";
                
                // Notify via SignalR
                await _hubContext.Clients.Group("server-monitoring")
                    .SendAsync("BackupCompleted", new
                    {
                        BackupType = "Manual",
                        FileName = Path.GetFileName(backupResult.FilePath),
                        Timestamp = DateTime.Now,
                        Station = "ServerManagement"
                    });
            }
            else
            {
                TempData["ErrorMessage"] = $"Backup failed: {backupResult.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manual backup");
            TempData["ErrorMessage"] = "An error occurred while creating the backup.";
        }
        
        return RedirectToAction(nameof(BackupManagement));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteBackup(int id)
    {
        try
        {
            var success = await _backupService.DeleteBackupAsync(id);
            
            if (success)
            {
                TempData["SuccessMessage"] = "Backup deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete backup.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup {BackupId}", id);
            TempData["ErrorMessage"] = "An error occurred while deleting the backup.";
        }
        
        return RedirectToAction(nameof(BackupManagement));
    }

    [HttpPost]
    public async Task<IActionResult> RestoreBackup(int id)
    {
        try
        {
            var success = await _backupService.RestoreBackupAsync(id);
            
            if (success)
            {
                TempData["SuccessMessage"] = "Database restored successfully. Please restart the application.";
                
                // Notify via SignalR
                await _hubContext.Clients.Group("server-monitoring")
                    .SendAsync("BackupRestored", new
                    {
                        BackupId = id,
                        Timestamp = DateTime.Now,
                        Station = "ServerManagement"
                    });
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to restore backup.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup {BackupId}", id);
            TempData["ErrorMessage"] = "An error occurred while restoring the backup.";
        }
        
        return RedirectToAction(nameof(BackupManagement));
    }
}