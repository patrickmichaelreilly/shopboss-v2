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
    
    // Optional attachment reference for file/attachment events
    public string? AttachmentId { get; set; }
    
    // Optional purchase order reference for purchase order events
    public string? PurchaseOrderId { get; set; }
    
    // Optional work order reference for work order events
    public string? WorkOrderId { get; set; }
    
    // Optional custom work order reference for custom work order events
    public string? CustomWorkOrderId { get; set; }
    
    // TaskBlock relationship (optional - events can exist without being in a block)
    public string? TaskBlockId { get; set; }
    public int? BlockDisplayOrder { get; set; }
    
    // Global timeline ordering (for mixed TaskBlock/Event ordering)
    public int? GlobalDisplayOrder { get; set; }
    
    // Navigation properties
    public Project Project { get; set; } = null!;
    public ProjectAttachment? Attachment { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public CustomWorkOrder? CustomWorkOrder { get; set; }
    public TaskBlock? TaskBlock { get; set; }
}