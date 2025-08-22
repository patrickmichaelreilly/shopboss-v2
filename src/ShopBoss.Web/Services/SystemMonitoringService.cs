using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using ShopBoss.Web.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.ServiceProcess;

namespace ShopBoss.Web.Services;

public class SystemMonitoringService
{
    private readonly ILogger<SystemMonitoringService> _logger;
    private readonly IConfiguration _configuration;
    
    // In-memory storage for current health status
    private readonly ConcurrentDictionary<string, MonitoredService> _services;

    public SystemMonitoringService(
        ILogger<SystemMonitoringService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _services = new ConcurrentDictionary<string, MonitoredService>();
        
        InitializeMonitoredServices();
    }

    /// <summary>
    /// Get all monitored services with their current status
    /// </summary>
    public List<MonitoredService> GetAllServices()
    {
        return _services.Values.OrderBy(s => s.Name).ToList();
    }

    /// <summary>
    /// Get a specific service by ID
    /// </summary>
    public MonitoredService? GetService(string serviceId)
    {
        _services.TryGetValue(serviceId, out var service);
        return service;
    }

    /// <summary>
    /// Check health of a specific service
    /// </summary>
    public async Task<MonitoredService> CheckServiceHealthAsync(string serviceId)
    {
        if (!_services.TryGetValue(serviceId, out var service))
        {
            throw new ArgumentException($"Service with ID {serviceId} not found");
        }

        try
        {
            switch (service.ServiceType)
            {
                case "Database":
                    await CheckDatabaseHealthAsync(service);
                    break;
                    
                case "HttpApi":
                    await CheckHttpApiHealthAsync(service);
                    break;
                    
                case "WindowsService":
                    await CheckWindowsServiceHealthAsync(service);
                    break;
                    
                default:
                    service.CurrentStatus = ServiceHealthLevel.Unknown;
                    service.ErrorMessage = $"Unknown service type: {service.ServiceType}";
                    service.IsReachable = false;
                    break;
            }
            
            service.LastChecked = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health for service {ServiceName}", service.Name);
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = ex.Message;
            service.IsReachable = false;
            service.LastChecked = DateTime.Now;
        }

        return service;
    }

    /// <summary>
    /// Check health of all enabled services
    /// </summary>
    public async Task<List<MonitoredService>> CheckAllServicesHealthAsync()
    {
        var enabledServices = _services.Values.Where(s => s.IsEnabled).ToList();
        var healthResults = new List<MonitoredService>();

        foreach (var service in enabledServices)
        {
            try
            {
                var health = await CheckServiceHealthAsync(service.Id);
                healthResults.Add(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check health for service {ServiceName}", service.Name);
                
                service.CurrentStatus = ServiceHealthLevel.Critical;
                service.ErrorMessage = ex.Message;
                service.IsReachable = false;
                service.LastChecked = DateTime.Now;
                
                healthResults.Add(service);
            }
        }

        return healthResults;
    }

    /// <summary>
    /// Initialize the monitored services
    /// </summary>
    private void InitializeMonitoredServices()
    {
        var services = new List<MonitoredService>
        {
            // ShopBoss Application (SQLite database monitoring)
            new MonitoredService
            {
                Id = "shopboss-app",
                Name = "ShopBoss Application",
                ServiceType = "Database",
                ConnectionString = _configuration.GetConnectionString("DefaultConnection") ?? "",
                IsEnabled = true,
                Description = "ShopBoss application and SQLite database connectivity"
            },
            
            // External SQL Server (Microvellum data sources)
            new MonitoredService
            {
                Id = "microvellum-sql",
                Name = "Microvellum SQL Server",
                ServiceType = "Database",
                ConnectionString = "Server=YOUR_SQL_SERVER;Database=MicrovellumData;Trusted_Connection=true;TrustServerCertificate=true;",
                IsEnabled = false, // Disabled until configured
                Description = "External SQL Server for Microvellum data import"
            },
            
            // SpeedDial Service
            new MonitoredService
            {
                Id = "speeddial-api",
                Name = "SpeedDial Service",
                ServiceType = "HttpApi",
                ConnectionString = "http://localhost:8080/api/health",
                IsEnabled = false, // Disabled until configured
                Description = "SpeedDial web service API endpoint"
            },
            
            // Time & Attendance Service
            new MonitoredService
            {
                Id = "timeattendance-api",
                Name = "Time & Attendance Service",
                ServiceType = "HttpApi",
                ConnectionString = "http://localhost:8081/api/status",
                IsEnabled = false, // Disabled until configured
                Description = "Time & Attendance web service API endpoint"
            },
            
            // Polling Service
            new MonitoredService
            {
                Id = "polling-service",
                Name = "Polling Service",
                ServiceType = "WindowsService",
                ConnectionString = "PollingService", // Windows service name
                IsEnabled = false, // Disabled until configured
                Description = "Windows service for data polling operations"
            }
        };

        foreach (var service in services)
        {
            _services.TryAdd(service.Id, service);
        }
        
        _logger.LogInformation("Initialized {Count} monitored services", services.Count);
    }

    #region Private Health Check Methods

    private async Task CheckDatabaseHealthAsync(MonitoredService service)
    {
        if (string.IsNullOrEmpty(service.ConnectionString))
        {
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = "No connection string configured";
            service.IsReachable = false;
            return;
        }

        var dbStopwatch = Stopwatch.StartNew();
        
        try
        {
            // Determine if this is SQLite or SQL Server based on connection string
            bool isSqlite = service.ConnectionString.Contains("Data Source") || 
                           service.Id == "shopboss-app";
            
            if (isSqlite)
            {
                // SQLite connection for ShopBoss Application monitoring
                using var connection = new SqliteConnection(service.ConnectionString);
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
                var tableCount = await command.ExecuteScalarAsync();
                
                dbStopwatch.Stop();

                service.CurrentStatus = ServiceHealthLevel.Healthy;
                service.IsReachable = true;
                service.StatusDetails = $"SQLite database connection successful ({tableCount} tables)";
                service.ResponseTimeMs = dbStopwatch.ElapsedMilliseconds;
                service.ErrorMessage = null;
            }
            else
            {
                // External SQL Server connection
                using var connection = new SqlConnection(service.ConnectionString);
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT @@VERSION";
                var version = await command.ExecuteScalarAsync();
                
                dbStopwatch.Stop();

                service.CurrentStatus = ServiceHealthLevel.Healthy;
                service.IsReachable = true;
                service.StatusDetails = "SQL Server connection successful";
                service.ResponseTimeMs = dbStopwatch.ElapsedMilliseconds;
                service.ErrorMessage = null;
            }
        }
        catch (Exception ex)
        {
            dbStopwatch.Stop();
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = ex.Message;
            service.IsReachable = false;
            service.ResponseTimeMs = dbStopwatch.ElapsedMilliseconds;
            service.StatusDetails = null;
        }
    }

    private async Task CheckHttpApiHealthAsync(MonitoredService service)
    {
        if (string.IsNullOrEmpty(service.ConnectionString))
        {
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = "No URL configured";
            service.IsReachable = false;
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.GetAsync(service.ConnectionString);
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                service.CurrentStatus = ServiceHealthLevel.Healthy;
                service.IsReachable = true;
                service.StatusDetails = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                service.ErrorMessage = null;
            }
            else
            {
                service.CurrentStatus = ServiceHealthLevel.Warning;
                service.IsReachable = true;
                service.ErrorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                service.StatusDetails = null;
            }
            
            service.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = ex.Message;
            service.IsReachable = false;
            service.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            service.StatusDetails = null;
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = "Request timeout";
            service.IsReachable = false;
            service.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            service.StatusDetails = null;
        }
    }

    private async Task CheckWindowsServiceHealthAsync(MonitoredService service)
    {
        await Task.CompletedTask; // Make method async compliant
        
        if (string.IsNullOrEmpty(service.ConnectionString))
        {
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = "No service name configured";
            service.IsReachable = false;
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Check if running on Windows (this check will only work in Windows deployment)
            if (!OperatingSystem.IsWindows())
            {
                service.CurrentStatus = ServiceHealthLevel.Warning;
                service.ErrorMessage = "Windows service checks only available on Windows";
                service.IsReachable = false;
                service.StatusDetails = "Development environment - Windows service monitoring unavailable";
                return;
            }

            using var serviceController = new ServiceController(service.ConnectionString);
            
            // This will throw if service doesn't exist
            var status = serviceController.Status;
            var displayName = serviceController.DisplayName;
            
            stopwatch.Stop();

            switch (status)
            {
                case ServiceControllerStatus.Running:
                    service.CurrentStatus = ServiceHealthLevel.Healthy;
                    service.IsReachable = true;
                    service.StatusDetails = $"Service '{displayName}' is running";
                    service.ErrorMessage = null;
                    break;
                    
                case ServiceControllerStatus.Stopped:
                    service.CurrentStatus = ServiceHealthLevel.Critical;
                    service.ErrorMessage = $"Service '{displayName}' is stopped";
                    service.IsReachable = false;
                    service.StatusDetails = null;
                    break;
                    
                case ServiceControllerStatus.Paused:
                    service.CurrentStatus = ServiceHealthLevel.Warning;
                    service.ErrorMessage = $"Service '{displayName}' is paused";
                    service.IsReachable = false;
                    service.StatusDetails = null;
                    break;
                    
                default:
                    service.CurrentStatus = ServiceHealthLevel.Warning;
                    service.ErrorMessage = $"Service '{displayName}' status: {status}";
                    service.IsReachable = false;
                    service.StatusDetails = null;
                    break;
            }
            
            service.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = $"Service '{service.ConnectionString}' not found: {ex.Message}";
            service.IsReachable = false;
            service.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            service.StatusDetails = null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = ex.Message;
            service.IsReachable = false;
            service.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            service.StatusDetails = null;
        }
    }

    #endregion
}