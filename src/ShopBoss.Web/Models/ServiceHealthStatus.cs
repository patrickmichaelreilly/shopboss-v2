using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class ServiceHealthStatus
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Foreign key to MonitoredService
    /// </summary>
    [Required]
    public string ServiceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Navigation property to the monitored service
    /// </summary>
    [ForeignKey("ServiceId")]
    public virtual MonitoredService Service { get; set; } = null!;
    
    /// <summary>
    /// Current health status of the service
    /// </summary>
    public ServiceHealthLevel Status { get; set; }
    
    /// <summary>
    /// When this status check was performed
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Response time in milliseconds (for applicable service types)
    /// </summary>
    public long? ResponseTimeMs { get; set; }
    
    /// <summary>
    /// Error message if the service is unhealthy
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Additional details about the health check result
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// Whether the service was reachable/responding
    /// </summary>
    public bool IsReachable { get; set; } = true;
}

public enum ServiceHealthLevel
{
    Healthy,
    Warning,
    Critical,
    Unknown,
    Offline
}