namespace ShopBoss.Web.Models;

public class BackupSystemStatus
{
    public DateTime? LastBackupDate { get; set; }
    public DateTime? NextBackupDate { get; set; }
    public bool IsBackgroundServiceRunning { get; set; }
    public long TotalBackupsCount { get; set; }
    public long TotalBackupsSize { get; set; }
    public string BackupDirectoryPath { get; set; } = string.Empty;
    public long AvailableDiskSpace { get; set; }
    public long DatabaseSize { get; set; }
}