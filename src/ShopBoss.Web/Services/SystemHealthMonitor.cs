using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShopBoss.Web.Services;

public class SystemHealthMonitor
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SystemHealthMonitor> _logger;
    
    // Health check thresholds
    private const double DISK_SPACE_WARNING_THRESHOLD = 85.0; // 85% usage
    private const double DISK_SPACE_CRITICAL_THRESHOLD = 95.0; // 95% usage
    private const double MEMORY_WARNING_THRESHOLD = 80.0; // 80% usage
    private const double MEMORY_CRITICAL_THRESHOLD = 90.0; // 90% usage
    private const double RESPONSE_TIME_WARNING_THRESHOLD = 1000.0; // 1 second
    private const double RESPONSE_TIME_CRITICAL_THRESHOLD = 3000.0; // 3 seconds
    private const double DATABASE_CONNECTION_WARNING_THRESHOLD = 500.0; // 500ms
    private const double DATABASE_CONNECTION_CRITICAL_THRESHOLD = 2000.0; // 2 seconds

    public SystemHealthMonitor(ShopBossDbContext context, ILogger<SystemHealthMonitor> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SystemHealthMetrics> CheckSystemHealthAsync()
    {
        var metrics = new SystemHealthMetrics
        {
            LastHealthCheck = DateTime.Now
        };

        try
        {
            // Check database connectivity
            await CheckDatabaseHealthAsync(metrics);
            
            // Check disk space
            CheckDiskSpaceHealth(metrics);
            
            // Check memory usage
            CheckMemoryHealth(metrics);
            
            // Check response time (application health)
            await CheckResponseTimeHealthAsync(metrics);
            
            // Get system statistics
            await GetSystemStatisticsAsync(metrics);
            
            // Calculate overall health status
            CalculateOverallHealth(metrics);
            
            _logger.LogDebug("System health check completed - Overall Status: {Status}", metrics.OverallStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system health check");
            metrics.ErrorMessage = ex.Message;
            metrics.OverallStatus = HealthStatusLevel.Error;
        }

        return metrics;
    }

    private async Task CheckDatabaseHealthAsync(SystemHealthMetrics metrics)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Simple database connectivity test
            await _context.Database.ExecuteSqlRawAsync("SELECT 1");
            stopwatch.Stop();
            
            metrics.DatabaseConnectionTimeMs = stopwatch.ElapsedMilliseconds;
            
            if (metrics.DatabaseConnectionTimeMs < DATABASE_CONNECTION_WARNING_THRESHOLD)
            {
                metrics.DatabaseStatus = HealthStatusLevel.Healthy;
            }
            else if (metrics.DatabaseConnectionTimeMs < DATABASE_CONNECTION_CRITICAL_THRESHOLD)
            {
                metrics.DatabaseStatus = HealthStatusLevel.Warning;
            }
            else
            {
                metrics.DatabaseStatus = HealthStatusLevel.Critical;
            }
            
            _logger.LogDebug("Database health check completed in {ElapsedMs}ms - Status: {Status}", 
                metrics.DatabaseConnectionTimeMs, metrics.DatabaseStatus);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metrics.DatabaseConnectionTimeMs = stopwatch.ElapsedMilliseconds;
            metrics.DatabaseStatus = HealthStatusLevel.Error;
            _logger.LogError(ex, "Database health check failed");
        }
    }

    private void CheckDiskSpaceHealth(SystemHealthMetrics metrics)
    {
        try
        {
            // Get disk space for the application directory
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var driveInfo = new DriveInfo(Path.GetPathRoot(appDirectory) ?? "C:");
            
            metrics.TotalDiskSpaceGB = Math.Round(driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0), 2);
            metrics.AvailableDiskSpaceGB = Math.Round(driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 2);
            
            var usagePercentage = metrics.DiskUsagePercentage;
            
            if (usagePercentage < DISK_SPACE_WARNING_THRESHOLD)
            {
                metrics.DiskSpaceStatus = HealthStatusLevel.Healthy;
            }
            else if (usagePercentage < DISK_SPACE_CRITICAL_THRESHOLD)
            {
                metrics.DiskSpaceStatus = HealthStatusLevel.Warning;
            }
            else
            {
                metrics.DiskSpaceStatus = HealthStatusLevel.Critical;
            }
            
            _logger.LogDebug("Disk space check completed - Usage: {Usage}% - Status: {Status}", 
                usagePercentage, metrics.DiskSpaceStatus);
        }
        catch (Exception ex)
        {
            metrics.DiskSpaceStatus = HealthStatusLevel.Error;
            _logger.LogError(ex, "Disk space health check failed");
        }
    }

    private void CheckMemoryHealth(SystemHealthMetrics metrics)
    {
        try
        {
            // Get memory usage information
            var currentProcess = Process.GetCurrentProcess();
            var workingSet = currentProcess.WorkingSet64;
            
            // Get total system memory (approximate)
            var totalMemory = GC.GetTotalMemory(false);
            var memoryUsage = (double)workingSet / (1024 * 1024); // Convert to MB
            
            // Calculate percentage based on working set vs available memory
            // This is a simplified calculation - in production you might want more sophisticated memory monitoring
            var gcMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0); // MB
            metrics.MemoryUsagePercentage = Math.Round(gcMemory, 2);
            
            // For percentage calculation, we'll use a simplified approach
            // In a real production system, you'd want to use performance counters or WMI
            var memoryPressure = GC.GetTotalMemory(false) / (1024.0 * 1024.0 * 1024.0); // GB
            var estimatedUsagePercent = Math.Min(memoryPressure * 10, 100); // Rough estimate
            
            if (estimatedUsagePercent < MEMORY_WARNING_THRESHOLD)
            {
                metrics.MemoryStatus = HealthStatusLevel.Healthy;
            }
            else if (estimatedUsagePercent < MEMORY_CRITICAL_THRESHOLD)
            {
                metrics.MemoryStatus = HealthStatusLevel.Warning;
            }
            else
            {
                metrics.MemoryStatus = HealthStatusLevel.Critical;
            }
            
            _logger.LogDebug("Memory check completed - Usage: {Usage}MB - Status: {Status}", 
                metrics.MemoryUsagePercentage, metrics.MemoryStatus);
        }
        catch (Exception ex)
        {
            metrics.MemoryStatus = HealthStatusLevel.Error;
            _logger.LogError(ex, "Memory health check failed");
        }
    }

    private async Task CheckResponseTimeHealthAsync(SystemHealthMetrics metrics)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Simple application responsiveness test
            await Task.Delay(1); // Minimal async operation
            stopwatch.Stop();
            
            metrics.AverageResponseTimeMs = stopwatch.ElapsedMilliseconds;
            
            if (metrics.AverageResponseTimeMs < RESPONSE_TIME_WARNING_THRESHOLD)
            {
                metrics.ResponseTimeStatus = HealthStatusLevel.Healthy;
            }
            else if (metrics.AverageResponseTimeMs < RESPONSE_TIME_CRITICAL_THRESHOLD)
            {
                metrics.ResponseTimeStatus = HealthStatusLevel.Warning;
            }
            else
            {
                metrics.ResponseTimeStatus = HealthStatusLevel.Critical;
            }
            
            _logger.LogDebug("Response time check completed - Time: {Time}ms - Status: {Status}", 
                metrics.AverageResponseTimeMs, metrics.ResponseTimeStatus);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metrics.AverageResponseTimeMs = stopwatch.ElapsedMilliseconds;
            metrics.ResponseTimeStatus = HealthStatusLevel.Error;
            _logger.LogError(ex, "Response time health check failed");
        }
    }

    private async Task GetSystemStatisticsAsync(SystemHealthMetrics metrics)
    {
        try
        {
            // Get active work order count
            metrics.ActiveWorkOrderCount = await _context.WorkOrders
                .CountAsync(wo => !wo.IsArchived);
            
            // Get total parts count
            metrics.TotalPartsCount = await _context.Parts.CountAsync();
            
            _logger.LogDebug("System statistics retrieved - Active Work Orders: {WorkOrders}, Total Parts: {Parts}", 
                metrics.ActiveWorkOrderCount, metrics.TotalPartsCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system statistics");
        }
    }

    private void CalculateOverallHealth(SystemHealthMetrics metrics)
    {
        // Calculate overall health based on individual component health
        var healthLevels = new[]
        {
            metrics.DatabaseStatus,
            metrics.DiskSpaceStatus,
            metrics.MemoryStatus,
            metrics.ResponseTimeStatus
        };

        // Overall health is the worst of all component health levels
        metrics.OverallStatus = healthLevels.Max();
        
        _logger.LogDebug("Overall health calculated - Status: {Status}", metrics.OverallStatus);
    }

    public async Task<SystemHealthStatus> GetOrCreateHealthStatusAsync()
    {
        try
        {
            var healthStatus = await _context.SystemHealthStatus.FirstOrDefaultAsync();
            
            if (healthStatus == null)
            {
                healthStatus = new SystemHealthStatus();
                _context.SystemHealthStatus.Add(healthStatus);
                await _context.SaveChangesAsync();
            }
            
            return healthStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accessing SystemHealthStatus table - table may not exist yet");
            // Return a default health status without saving to database
            return new SystemHealthStatus
            {
                OverallStatus = HealthStatusLevel.Warning,
                DatabaseStatus = HealthStatusLevel.Warning,
                DiskSpaceStatus = HealthStatusLevel.Healthy,
                MemoryStatus = HealthStatusLevel.Healthy,
                ResponseTimeStatus = HealthStatusLevel.Healthy,
                LastHealthCheck = DateTime.Now,
                ErrorMessage = "Health monitoring table not available - database migration may be needed"
            };
        }
    }

    public async Task UpdateHealthStatusAsync(SystemHealthMetrics metrics)
    {
        try
        {
            var healthStatus = await GetOrCreateHealthStatusAsync();
            
            // If we got a default health status (table doesn't exist), don't try to save
            if (healthStatus.ErrorMessage?.Contains("table not available") == true)
            {
                _logger.LogDebug("Skipping health status database update - table not available");
                return;
            }
            
            // Update health status with current metrics
            healthStatus.OverallStatus = metrics.OverallStatus;
            healthStatus.DatabaseStatus = metrics.DatabaseStatus;
            healthStatus.DiskSpaceStatus = metrics.DiskSpaceStatus;
            healthStatus.MemoryStatus = metrics.MemoryStatus;
            healthStatus.ResponseTimeStatus = metrics.ResponseTimeStatus;
            healthStatus.AvailableDiskSpaceGB = metrics.AvailableDiskSpaceGB;
            healthStatus.TotalDiskSpaceGB = metrics.TotalDiskSpaceGB;
            healthStatus.MemoryUsagePercentage = metrics.MemoryUsagePercentage;
            healthStatus.AverageResponseTimeMs = metrics.AverageResponseTimeMs;
            healthStatus.DatabaseConnectionTimeMs = metrics.DatabaseConnectionTimeMs;
            healthStatus.ActiveWorkOrderCount = metrics.ActiveWorkOrderCount;
            healthStatus.TotalPartsCount = metrics.TotalPartsCount;
            healthStatus.LastHealthCheck = metrics.LastHealthCheck;
            healthStatus.ErrorMessage = metrics.ErrorMessage;
            
            await _context.SaveChangesAsync();
            
            _logger.LogDebug("Health status updated in database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating health status in database");
        }
    }
}