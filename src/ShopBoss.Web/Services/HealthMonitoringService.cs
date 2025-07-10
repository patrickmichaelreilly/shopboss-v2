using Microsoft.AspNetCore.SignalR;
using ShopBoss.Web.Models;
using ShopBoss.Web.Hubs;

namespace ShopBoss.Web.Services;

public class HealthMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthMonitoringService> _logger;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    // Health check intervals
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromMinutes(1); // Check every minute
    private static readonly TimeSpan FastHealthCheckInterval = TimeSpan.FromSeconds(30); // Faster checks when issues detected
    private static readonly TimeSpan SlowHealthCheckInterval = TimeSpan.FromMinutes(5); // Slower checks when everything is healthy
    
    private HealthStatusLevel _lastOverallStatus = HealthStatusLevel.Healthy;
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private int _consecutiveHealthyChecks = 0;
    private int _consecutiveUnhealthyChecks = 0;

    public HealthMonitoringService(
        IServiceProvider serviceProvider, 
        ILogger<HealthMonitoringService> logger,
        IHubContext<StatusHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthMonitoringService started");

        // Initial health check
        await PerformHealthCheck();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthCheck();
                
                // Adaptive monitoring frequency based on health status
                var delay = GetNextCheckInterval();
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("HealthMonitoringService stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HealthMonitoringService main loop");
                
                // Wait before retrying after error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        
        _logger.LogInformation("HealthMonitoringService stopped");
    }

    private async Task PerformHealthCheck()
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            _logger.LogWarning("Health check skipped - previous check still running");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var healthMonitor = scope.ServiceProvider.GetRequiredService<SystemHealthMonitor>();
            var auditTrailService = scope.ServiceProvider.GetRequiredService<AuditTrailService>();

            _logger.LogDebug("Starting health check");
            
            // Perform health check
            var healthMetrics = await healthMonitor.CheckSystemHealthAsync();
            
            // Update database with health status
            await healthMonitor.UpdateHealthStatusAsync(healthMetrics);
            
            // Check for status changes
            var statusChanged = healthMetrics.OverallStatus != _lastOverallStatus;
            
            if (statusChanged)
            {
                _logger.LogInformation(
                    "System health status changed from {PreviousStatus} to {CurrentStatus}",
                    _lastOverallStatus, healthMetrics.OverallStatus);
                
                // Log audit trail for health status changes
                await auditTrailService.LogAsync(
                    "SystemHealth",
                    "StatusChange",
                    "System",
                    "1", // Single system health record
                    "System",
                    $"Health status changed from {_lastOverallStatus} to {healthMetrics.OverallStatus}");
            }
            
            // Update tracking variables
            _lastOverallStatus = healthMetrics.OverallStatus;
            _lastHealthCheck = DateTime.UtcNow;
            
            // Track consecutive healthy/unhealthy checks for adaptive monitoring
            if (healthMetrics.OverallStatus == HealthStatusLevel.Healthy)
            {
                _consecutiveHealthyChecks++;
                _consecutiveUnhealthyChecks = 0;
            }
            else
            {
                _consecutiveUnhealthyChecks++;
                _consecutiveHealthyChecks = 0;
            }

            // Send real-time updates via SignalR
            await BroadcastHealthUpdate(healthMetrics);
            
            // Note: Critical issue logging removed as per user request - health monitoring provides real-time metrics only
            
            _logger.LogDebug("Health check completed - Status: {Status}", healthMetrics.OverallStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
            
            // Broadcast error status
            await BroadcastHealthError(ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private TimeSpan GetNextCheckInterval()
    {
        // Adaptive monitoring frequency
        if (_lastOverallStatus == HealthStatusLevel.Critical || _lastOverallStatus == HealthStatusLevel.Error)
        {
            // Fast checks when there are critical issues
            return FastHealthCheckInterval;
        }
        else if (_lastOverallStatus == HealthStatusLevel.Warning)
        {
            // Normal checks when there are warnings
            return HealthCheckInterval;
        }
        else if (_consecutiveHealthyChecks > 10)
        {
            // Slow checks when system has been healthy for a while
            return SlowHealthCheckInterval;
        }
        else
        {
            // Normal interval
            return HealthCheckInterval;
        }
    }

    private async Task BroadcastHealthUpdate(SystemHealthMetrics metrics)
    {
        try
        {
            var healthUpdate = new
            {
                OverallStatus = metrics.OverallStatus.ToString(),
                DatabaseStatus = metrics.DatabaseStatus.ToString(),
                DiskSpaceStatus = metrics.DiskSpaceStatus.ToString(),
                MemoryStatus = metrics.MemoryStatus.ToString(),
                ResponseTimeStatus = metrics.ResponseTimeStatus.ToString(),
                AvailableDiskSpaceGB = metrics.AvailableDiskSpaceGB,
                TotalDiskSpaceGB = metrics.TotalDiskSpaceGB,
                DiskUsagePercentage = metrics.DiskUsagePercentage,
                MemoryUsagePercentage = metrics.MemoryUsagePercentage,
                AverageResponseTimeMs = metrics.AverageResponseTimeMs,
                DatabaseConnectionTimeMs = metrics.DatabaseConnectionTimeMs,
                ActiveWorkOrderCount = metrics.ActiveWorkOrderCount,
                TotalPartsCount = metrics.TotalPartsCount,
                LastHealthCheck = metrics.LastHealthCheck,
                ErrorMessage = metrics.ErrorMessage
            };

            await _hubContext.Clients.All.SendAsync("HealthUpdate", healthUpdate);
            _logger.LogDebug("Health update broadcast to all clients");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting health update via SignalR");
        }
    }

    private async Task BroadcastHealthError(string errorMessage)
    {
        try
        {
            var errorUpdate = new
            {
                OverallStatus = HealthStatusLevel.Error.ToString(),
                LastHealthCheck = DateTime.UtcNow,
                ErrorMessage = errorMessage
            };

            await _hubContext.Clients.All.SendAsync("HealthUpdate", errorUpdate);
            _logger.LogDebug("Health error broadcast to all clients");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting health error via SignalR");
        }
    }

    // LogCriticalIssue method removed - health monitoring provides real-time metrics only, no historical logging

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthMonitoringService stopping...");
        
        await base.StopAsync(stoppingToken);
        
        _semaphore.Dispose();
        _logger.LogInformation("HealthMonitoringService stopped");
    }
}