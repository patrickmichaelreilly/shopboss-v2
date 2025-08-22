using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using ShopBoss.Web.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

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
                    
                case "Process":
                    await CheckProcessHealthAsync(service);
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
            // ShopBoss Application (SQLite database monitoring) - First in list
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
                ConnectionString = "Server=192.168.0.24\\EUROTEXSQLSERVER;Database=MV_Database;User Id=sa;Password=PC12345!;TrustServerCertificate=true;",
                IsEnabled = true, // Now enabled with real connection
                Description = "External SQL Server for Microvellum data import"
            },
            
            // SpeedDial Service
            new MonitoredService
            {
                Id = "speeddial-api",
                Name = "SpeedDial Service",
                ServiceType = "HttpApi",
                ConnectionString = "http://192.168.0.24:5555/",
                IsEnabled = true, // Now enabled with real endpoint
                Description = "SpeedDial Windows service web interface"
            },
            
            // Time & Attendance Process
            new MonitoredService
            {
                Id = "timeattendance-process",
                Name = "Time & Attendance (PROXTIMEHW)",
                ServiceType = "Process",
                ConnectionString = "proxtimehw.exe",
                IsEnabled = true, // Now enabled for process monitoring
                Description = "PROXTIMEHW background process for time tracking"
            },
            
            // Polling Process
            new MonitoredService
            {
                Id = "polling-process",
                Name = "Polling Service (ALTOAUTO)",
                ServiceType = "Process",
                ConnectionString = "altoauto.exe",
                IsEnabled = true, // Now enabled for process monitoring
                Description = "ALTOAUTO background process for data polling"
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

    private async Task CheckProcessHealthAsync(MonitoredService service)
    {
        await Task.CompletedTask; // Make method async compliant
        
        if (string.IsNullOrEmpty(service.ConnectionString))
        {
            service.CurrentStatus = ServiceHealthLevel.Critical;
            service.ErrorMessage = "No process name configured";
            service.IsReachable = false;
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Check if running on Windows (process monitoring only works on Windows)
            if (!OperatingSystem.IsWindows())
            {
                service.CurrentStatus = ServiceHealthLevel.Warning;
                service.ErrorMessage = "Process monitoring only available on Windows";
                service.IsReachable = false;
                service.StatusDetails = "Development environment - process monitoring unavailable";
                return;
            }

            // Use WMI to check if process is running
            var processName = service.ConnectionString.Replace(".exe", ""); // Remove .exe if present
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            
            stopwatch.Stop();

            if (processes.Length > 0)
            {
                var process = processes[0];
                service.CurrentStatus = ServiceHealthLevel.Healthy;
                service.IsReachable = true;
                service.StatusDetails = $"Process '{processName}' is running (PID: {process.Id})";
                service.ErrorMessage = null;
                
                // Dispose processes to avoid memory leaks
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }
            else
            {
                service.CurrentStatus = ServiceHealthLevel.Critical;
                service.ErrorMessage = $"Process '{processName}' is not running";
                service.IsReachable = false;
                service.StatusDetails = null;
            }
            
            service.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
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