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
    private readonly AuditTrailService _auditTrailService;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly SystemMonitoringService _systemMonitoringService;

    public ServerManagementController(
        ShopBossDbContext context, 
        ILogger<ServerManagementController> logger,
        BackupService backupService,
        AuditTrailService auditTrailService,
        IHubContext<StatusHub> hubContext,
        SystemMonitoringService systemMonitoringService)
    {
        _context = context;
        _logger = logger;
        _backupService = backupService;
        _auditTrailService = auditTrailService;
        _hubContext = hubContext;
        _systemMonitoringService = systemMonitoringService;
    }

    // Main Dashboard - consolidates health and backup overview
    public async Task<IActionResult> Index()
    {
        try
        {
            // Get backup configuration and recent backups
            var backupConfig = await _backupService.GetBackupConfigurationAsync();
            var recentBackups = await _backupService.GetRecentBackupsAsync(5); // Just top 5 for dashboard
            
            // Auto-initialize services if none exist
            await _systemMonitoringService.InitializeDefaultServicesAsync();
            
            // Run health checks for all services on page load
            await _systemMonitoringService.CheckAllServicesHealthAsync();
            
            // Get service monitoring data
            var monitoredServices = await _systemMonitoringService.GetAllMonitoredServicesAsync();
            var latestServiceStatuses = await _systemMonitoringService.GetLatestHealthStatusesAsync();
            
            // Get recent health-related audit logs
            var recentHealthLogs = await _context.AuditLogs
                .Where(log => log.EntityType == "SystemHealth" || log.EntityType == "System" || log.EntityType == "BackupConfiguration")
                .OrderByDescending(log => log.Timestamp)
                .Take(10)
                .ToListAsync();

            var viewModel = new ServerManagementDashboardViewModel
            {
                BackupConfiguration = backupConfig,
                RecentBackups = recentBackups,
                RecentActivityLogs = recentHealthLogs,
                MonitoredServices = monitoredServices,
                LatestServiceStatuses = latestServiceStatuses,
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

    // Service Monitoring Actions
    [HttpPost]
    public async Task<IActionResult> CheckServiceHealth(string serviceId)
    {
        try
        {
            var healthStatus = await _systemMonitoringService.CheckServiceHealthAsync(serviceId);
            
            // Notify via SignalR
            await _hubContext.Clients.Group("server-monitoring")
                .SendAsync("ServiceHealthUpdated", new
                {
                    ServiceId = serviceId,
                    Status = healthStatus.Status.ToString(),
                    LastChecked = healthStatus.LastChecked,
                    ResponseTime = healthStatus.ResponseTimeMs,
                    IsReachable = healthStatus.IsReachable,
                    ErrorMessage = healthStatus.ErrorMessage
                });

            return Json(new { success = true, healthStatus });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service health for service {ServiceId}", serviceId);
            return Json(new { success = false, message = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetServiceHealthData(string serviceId)
    {
        try
        {
            var healthHistory = await _systemMonitoringService.GetServiceHealthHistoryAsync(serviceId, 10);
            return Json(new { success = true, healthHistory });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service health data for {ServiceId}", serviceId);
            return Json(new { success = false, message = ex.Message });
        }
    }

}