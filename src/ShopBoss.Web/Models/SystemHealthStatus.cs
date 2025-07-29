using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class SystemHealthStatus
{
    [Key]
    public int Id { get; set; } = 1; // Single health status record
    
    /// <summary>
    /// Overall system health status
    /// </summary>
    public HealthStatusLevel OverallStatus { get; set; } = HealthStatusLevel.Healthy;
    
    /// <summary>
    /// Database connection health
    /// </summary>
    public HealthStatusLevel DatabaseStatus { get; set; } = HealthStatusLevel.Healthy;
    
    /// <summary>
    /// Disk space health status
    /// </summary>
    public HealthStatusLevel DiskSpaceStatus { get; set; } = HealthStatusLevel.Healthy;
    
    /// <summary>
    /// Memory usage health status
    /// </summary>
    public HealthStatusLevel MemoryStatus { get; set; } = HealthStatusLevel.Healthy;
    
    /// <summary>
    /// Response time health status
    /// </summary>
    public HealthStatusLevel ResponseTimeStatus { get; set; } = HealthStatusLevel.Healthy;
    
    /// <summary>
    /// Available disk space in GB
    /// </summary>
    public double AvailableDiskSpaceGB { get; set; }
    
    /// <summary>
    /// Total disk space in GB
    /// </summary>
    public double TotalDiskSpaceGB { get; set; }
    
    /// <summary>
    /// Memory usage percentage
    /// </summary>
    public double MemoryUsagePercentage { get; set; }
    
    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTimeMs { get; set; }
    
    /// <summary>
    /// Last health check timestamp
    /// </summary>
    public DateTime LastHealthCheck { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Health check error message (if any)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Database connection time in milliseconds
    /// </summary>
    public double DatabaseConnectionTimeMs { get; set; }
    
    /// <summary>
    /// Number of active work orders
    /// </summary>
    public int ActiveWorkOrderCount { get; set; }
    
    /// <summary>
    /// Total parts in system
    /// </summary>
    public int TotalPartsCount { get; set; }
}

public enum HealthStatusLevel
{
    Healthy = 0,
    Warning = 1,
    Critical = 2,
    Error = 3
}

public class SystemHealthMetrics
{
    public HealthStatusLevel OverallStatus { get; set; }
    public HealthStatusLevel DatabaseStatus { get; set; }
    public HealthStatusLevel DiskSpaceStatus { get; set; }
    public HealthStatusLevel MemoryStatus { get; set; }
    public HealthStatusLevel ResponseTimeStatus { get; set; }
    public double AvailableDiskSpaceGB { get; set; }
    public double TotalDiskSpaceGB { get; set; }
    public double MemoryUsagePercentage { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double DatabaseConnectionTimeMs { get; set; }
    public int ActiveWorkOrderCount { get; set; }
    public int TotalPartsCount { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public string? ErrorMessage { get; set; }
    public double DiskUsagePercentage => TotalDiskSpaceGB > 0 ? 
        ((TotalDiskSpaceGB - AvailableDiskSpaceGB) / TotalDiskSpaceGB) * 100 : 0;
}