namespace ShopBoss.Web.Models;

public class MonitoredService
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty; // "Database", "HttpApi", "WindowsService"
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    
    // Current status (in-memory only)
    public ServiceHealthLevel? CurrentStatus { get; set; }
    public DateTime? LastChecked { get; set; }
    public long? ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StatusDetails { get; set; }
    public bool IsReachable { get; set; }
}