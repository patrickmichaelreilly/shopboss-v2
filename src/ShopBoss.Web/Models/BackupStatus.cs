using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class BackupStatus
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// When this backup was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Type of backup (Automatic, Manual)
    /// </summary>
    public BackupType BackupType { get; set; }
    
    /// <summary>
    /// Full file path to the backup file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Original database file size in bytes
    /// </summary>
    public long OriginalSize { get; set; }
    
    /// <summary>
    /// Compressed backup file size in bytes
    /// </summary>
    public long BackupSize { get; set; }
    
    /// <summary>
    /// Whether the backup was successful
    /// </summary>
    public bool IsSuccessful { get; set; }
    
    /// <summary>
    /// Error message if backup failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// How long the backup took to complete
    /// </summary>
    public TimeSpan Duration { get; set; }
}

public enum BackupType
{
    Automatic,
    Manual
}