using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using System.Diagnostics;

namespace ShopBoss.Web.Services;

public class SystemMonitoringService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SystemMonitoringService> _logger;
    private readonly IConfiguration _configuration;

    public SystemMonitoringService(
        ShopBossDbContext context,
        ILogger<SystemMonitoringService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Get all monitored services
    /// </summary>
    public async Task<List<MonitoredService>> GetAllMonitoredServicesAsync()
    {
        return await _context.MonitoredServices
            .Include(s => s.HealthStatuses.OrderByDescending(h => h.LastChecked).Take(1))
            .OrderBy(s => s.ServiceName)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific monitored service by ID
    /// </summary>
    public async Task<MonitoredService?> GetMonitoredServiceAsync(string serviceId)
    {
        return await _context.MonitoredServices
            .Include(s => s.HealthStatuses.OrderByDescending(h => h.LastChecked))
            .FirstOrDefaultAsync(s => s.Id == serviceId);
    }

    /// <summary>
    /// Create or update a monitored service
    /// </summary>
    public async Task<MonitoredService> CreateOrUpdateServiceAsync(MonitoredService service)
    {
        var existingService = await _context.MonitoredServices.FindAsync(service.Id);
        
        if (existingService == null)
        {
            service.CreatedDate = DateTime.Now;
            service.LastModifiedDate = DateTime.Now;
            _context.MonitoredServices.Add(service);
            _logger.LogInformation("Created new monitored service: {ServiceName}", service.ServiceName);
        }
        else
        {
            existingService.ServiceName = service.ServiceName;
            existingService.ServiceType = service.ServiceType;
            existingService.ConnectionString = service.ConnectionString;
            existingService.CheckIntervalMinutes = service.CheckIntervalMinutes;
            existingService.IsEnabled = service.IsEnabled;
            existingService.Description = service.Description;
            existingService.LastModifiedDate = DateTime.Now;
            _logger.LogInformation("Updated monitored service: {ServiceName}", service.ServiceName);
        }

        await _context.SaveChangesAsync();
        return existingService ?? service;
    }

    /// <summary>
    /// Delete a monitored service and its health history
    /// </summary>
    public async Task<bool> DeleteServiceAsync(string serviceId)
    {
        var service = await _context.MonitoredServices.FindAsync(serviceId);
        if (service == null)
        {
            return false;
        }

        _context.MonitoredServices.Remove(service);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Deleted monitored service: {ServiceName}", service.ServiceName);
        return true;
    }

    /// <summary>
    /// Check health of a specific service
    /// </summary>
    public async Task<ServiceHealthStatus> CheckServiceHealthAsync(string serviceId)
    {
        var service = await _context.MonitoredServices.FindAsync(serviceId);
        if (service == null)
        {
            throw new ArgumentException($"Service with ID {serviceId} not found");
        }

        var healthStatus = new ServiceHealthStatus
        {
            ServiceId = serviceId,
            LastChecked = DateTime.Now
        };

        try
        {
            switch (service.ServiceType)
            {
                case ServiceType.SqlServer:
                    await CheckSqlServerHealthAsync(service, healthStatus);
                    break;
                    
                case ServiceType.HttpEndpoint:
                    await CheckHttpEndpointHealthAsync(service, healthStatus);
                    break;
                    
                case ServiceType.WindowsService:
                    await CheckWindowsServiceHealthAsync(service, healthStatus);
                    break;
                    
                case ServiceType.CustomCheck:
                    await CheckCustomServiceHealthAsync(service, healthStatus);
                    break;
                    
                default:
                    healthStatus.Status = ServiceHealthLevel.Unknown;
                    healthStatus.ErrorMessage = $"Unknown service type: {service.ServiceType}";
                    healthStatus.IsReachable = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health for service {ServiceName}", service.ServiceName);
            healthStatus.Status = ServiceHealthLevel.Critical;
            healthStatus.ErrorMessage = ex.Message;
            healthStatus.IsReachable = false;
        }

        // Save health status to database
        _context.ServiceHealthStatuses.Add(healthStatus);
        await _context.SaveChangesAsync();

        return healthStatus;
    }

    /// <summary>
    /// Check health of all enabled services
    /// </summary>
    public async Task<List<ServiceHealthStatus>> CheckAllServicesHealthAsync()
    {
        var services = await _context.MonitoredServices
            .Where(s => s.IsEnabled)
            .ToListAsync();

        var healthResults = new List<ServiceHealthStatus>();

        foreach (var service in services)
        {
            try
            {
                var health = await CheckServiceHealthAsync(service.Id);
                healthResults.Add(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check health for service {ServiceName}", service.ServiceName);
                
                // Create a failed health status
                var failedHealth = new ServiceHealthStatus
                {
                    ServiceId = service.Id,
                    Status = ServiceHealthLevel.Critical,
                    LastChecked = DateTime.Now,
                    ErrorMessage = ex.Message,
                    IsReachable = false
                };
                
                _context.ServiceHealthStatuses.Add(failedHealth);
                healthResults.Add(failedHealth);
            }
        }

        await _context.SaveChangesAsync();
        return healthResults;
    }

    /// <summary>
    /// Get recent health history for a service
    /// </summary>
    public async Task<List<ServiceHealthStatus>> GetServiceHealthHistoryAsync(string serviceId, int count = 20)
    {
        return await _context.ServiceHealthStatuses
            .Where(h => h.ServiceId == serviceId)
            .OrderByDescending(h => h.LastChecked)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Get latest health status for all services
    /// </summary>
    public async Task<List<ServiceHealthStatus>> GetLatestHealthStatusesAsync()
    {
        var services = await _context.MonitoredServices
            .Where(s => s.IsEnabled)
            .ToListAsync();

        var latestStatuses = new List<ServiceHealthStatus>();

        foreach (var service in services)
        {
            var latestStatus = await _context.ServiceHealthStatuses
                .Where(h => h.ServiceId == service.Id)
                .OrderByDescending(h => h.LastChecked)
                .FirstOrDefaultAsync();

            if (latestStatus != null)
            {
                latestStatuses.Add(latestStatus);
            }
        }

        return latestStatuses;
    }

    /// <summary>
    /// Initialize default monitored services
    /// </summary>
    public async Task InitializeDefaultServicesAsync()
    {
        // Check if we already have services configured
        var existingServices = await _context.MonitoredServices.CountAsync();
        if (existingServices > 0)
        {
            return; // Already initialized
        }

        _logger.LogInformation("Initializing default monitored services");

        // Add SQL Server monitoring (using the main database connection)
        var sqlServerService = new MonitoredService
        {
            ServiceName = "SQL Server Database",
            ServiceType = ServiceType.SqlServer,
            ConnectionString = _configuration.GetConnectionString("DefaultConnection"),
            CheckIntervalMinutes = 5,
            IsEnabled = true,
            Description = "Main ShopBoss SQLite database connectivity"
        };

        await CreateOrUpdateServiceAsync(sqlServerService);
        
        _logger.LogInformation("Default monitored services initialized");
    }

    #region Private Health Check Methods

    private async Task CheckSqlServerHealthAsync(MonitoredService service, ServiceHealthStatus healthStatus)
    {
        if (string.IsNullOrEmpty(service.ConnectionString))
        {
            healthStatus.Status = ServiceHealthLevel.Critical;
            healthStatus.ErrorMessage = "No connection string configured";
            healthStatus.IsReachable = false;
            return;
        }

        var dbStopwatch = Stopwatch.StartNew();
        
        try
        {
            using var connection = new SqliteConnection(service.ConnectionString);
            await connection.OpenAsync();
            
            // Simple connectivity test
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync();
            
            dbStopwatch.Stop();

            healthStatus.Status = ServiceHealthLevel.Healthy;
            healthStatus.IsReachable = true;
            healthStatus.Details = "Database connection successful";
            healthStatus.ResponseTimeMs = dbStopwatch.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            dbStopwatch.Stop();
            healthStatus.Status = ServiceHealthLevel.Critical;
            healthStatus.ErrorMessage = ex.Message;
            healthStatus.IsReachable = false;
            healthStatus.ResponseTimeMs = dbStopwatch.ElapsedMilliseconds;
        }
    }

    private async Task CheckHttpEndpointHealthAsync(MonitoredService service, ServiceHealthStatus healthStatus)
    {
        if (string.IsNullOrEmpty(service.ConnectionString))
        {
            healthStatus.Status = ServiceHealthLevel.Critical;
            healthStatus.ErrorMessage = "No URL configured";
            healthStatus.IsReachable = false;
            return;
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.GetAsync(service.ConnectionString);
            
            if (response.IsSuccessStatusCode)
            {
                healthStatus.Status = ServiceHealthLevel.Healthy;
                healthStatus.IsReachable = true;
                healthStatus.Details = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
            }
            else
            {
                healthStatus.Status = ServiceHealthLevel.Warning;
                healthStatus.IsReachable = true;
                healthStatus.ErrorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
            }
        }
        catch (HttpRequestException ex)
        {
            healthStatus.Status = ServiceHealthLevel.Critical;
            healthStatus.ErrorMessage = ex.Message;
            healthStatus.IsReachable = false;
        }
        catch (TaskCanceledException)
        {
            healthStatus.Status = ServiceHealthLevel.Critical;
            healthStatus.ErrorMessage = "Request timeout";
            healthStatus.IsReachable = false;
        }
    }

    private async Task CheckWindowsServiceHealthAsync(MonitoredService service, ServiceHealthStatus healthStatus)
    {
        // This will be implemented in Phase 2 when we add Windows service monitoring
        await Task.CompletedTask;
        
        healthStatus.Status = ServiceHealthLevel.Unknown;
        healthStatus.ErrorMessage = "Windows service monitoring not yet implemented";
        healthStatus.IsReachable = false;
    }

    private async Task CheckCustomServiceHealthAsync(MonitoredService service, ServiceHealthStatus healthStatus)
    {
        // This will be implemented later for custom health checks
        await Task.CompletedTask;
        
        healthStatus.Status = ServiceHealthLevel.Unknown;
        healthStatus.ErrorMessage = "Custom health checks not yet implemented";
        healthStatus.IsReachable = false;
    }

    #endregion
}