namespace ShopBoss.Web.Models;

public class HealthDashboardViewModel
{
    public SystemHealthStatus CurrentHealthStatus { get; set; } = new();
    public SystemHealthMetrics CurrentMetrics { get; set; } = new();
    public List<AuditLog> RecentHealthLogs { get; set; } = new();
    public string PageTitle { get; set; } = "System Health Dashboard";
}