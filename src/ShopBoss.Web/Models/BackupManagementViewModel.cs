namespace ShopBoss.Web.Models;

public class BackupManagementViewModel
{
    public BackupConfiguration Configuration { get; set; } = new();
    public List<BackupStatus> RecentBackups { get; set; } = new();
    public BackupSystemStatus SystemStatus { get; set; } = new();
}