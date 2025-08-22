using ShopBoss.Web.Models;

namespace ShopBoss.Web.Models;

public class ServerManagementDashboardViewModel
{
    public string PageTitle { get; set; } = "Server Management Dashboard";
    public BackupConfiguration? BackupConfiguration { get; set; }
    public List<BackupStatus> RecentBackups { get; set; } = new();
    public List<AuditLog> RecentActivityLogs { get; set; } = new();
    public List<MonitoredService> MonitoredServices { get; set; } = new();
}