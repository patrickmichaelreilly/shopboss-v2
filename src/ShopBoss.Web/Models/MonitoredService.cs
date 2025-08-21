using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class MonitoredService
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Display name for the service
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of service being monitored
    /// </summary>
    public ServiceType ServiceType { get; set; }
    
    /// <summary>
    /// Connection string or endpoint for health checks
    /// </summary>
    public string? ConnectionString { get; set; }
    
    /// <summary>
    /// How often to check this service (in minutes)
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 5;
    
    /// <summary>
    /// Whether this service is actively monitored
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// When this service configuration was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// When this service configuration was last modified
    /// </summary>
    public DateTime LastModifiedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Optional description of what this service does
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Collection of health status records for this service
    /// </summary>
    public virtual ICollection<ServiceHealthStatus> HealthStatuses { get; set; } = new List<ServiceHealthStatus>();
}

public enum ServiceType
{
    SqlServer,
    HttpEndpoint, 
    WindowsService,
    CustomCheck
}