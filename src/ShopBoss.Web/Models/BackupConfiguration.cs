using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class BackupConfiguration
{
    [Key]
    public int Id { get; set; } = 1; // Single configuration record
    
    /// <summary>
    /// Backup interval in minutes (default: 60 minutes)
    /// </summary>
    public int BackupIntervalMinutes { get; set; } = 60;
    
    /// <summary>
    /// Maximum number of backup files to retain (default: 24 for 1 day at hourly backups)
    /// </summary>
    public int MaxBackupRetention { get; set; } = 24;
    
    /// <summary>
    /// Enable compression for backup files (default: true)
    /// </summary>
    public bool EnableCompression { get; set; } = true;
    
    /// <summary>
    /// Backup directory path (default: External backup directory for beta safety)
    /// </summary>
    public string BackupDirectoryPath { get; set; } = @"C:\ShopBoss-Backups";
    
    /// <summary>
    /// Enable automatic backups (default: true)
    /// </summary>
    public bool EnableAutomaticBackups { get; set; } = true;
    
    /// <summary>
    /// When this configuration was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}