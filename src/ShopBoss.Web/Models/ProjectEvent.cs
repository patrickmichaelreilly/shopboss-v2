using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class ProjectEvent
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string ProjectId { get; set; } = string.Empty;
    
    [Required]
    public DateTime EventDate { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty; // "comment", "attachment", "status_change"
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    
    // Optional row number for events imported from SmartSheet
    public int? RowNumber { get; set; }
    
    // TaskBlock relationship (optional - events can exist without being in a block)
    public string? TaskBlockId { get; set; }
    public int? BlockDisplayOrder { get; set; }
    
    // Navigation properties
    public Project Project { get; set; } = null!;
    public TaskBlock? TaskBlock { get; set; }
}