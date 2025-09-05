using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class ProjectAttachment
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string ProjectId { get; set; } = string.Empty;
    
    public string FileName { get; set; } = string.Empty;
    
    public string OriginalFileName { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    public string ContentType { get; set; } = string.Empty;
    
    public string Label { get; set; } = string.Empty;
    
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    
    public string? UploadedBy { get; set; }
    
    // Navigation property
    public Project Project { get; set; } = null!;
}