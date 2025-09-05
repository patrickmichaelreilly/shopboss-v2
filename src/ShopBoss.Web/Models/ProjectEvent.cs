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
    
    [MaxLength(200)]
    public string? Label { get; set; }
    
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    
    // SmartSheet row ID for synced events
    public long? SmartsheetRowId { get; set; }
    
    // Optional row number for events imported from SmartSheet (display only)
    public int? RowNumber { get; set; }
    
    // Optional attachment reference for file/attachment events
    public string? AttachmentId { get; set; }
    
    // Optional purchase order reference for purchase order events
    public string? PurchaseOrderId { get; set; }
    
    // Optional work order reference for work order events
    public string? WorkOrderId { get; set; }
    
    // Optional custom work order reference for custom work order events
    public string? CustomWorkOrderId { get; set; }
    
    // Parent container relationship (null = root level, otherwise references TaskBlock)
    public string? ParentBlockId { get; set; }
    
    // Display order within parent container (works for both root and TaskBlocks)
    public int DisplayOrder { get; set; }
    
    // Navigation properties
    public Project Project { get; set; } = null!;
    public ProjectAttachment? Attachment { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public CustomWorkOrder? CustomWorkOrder { get; set; }
    public TaskBlock? ParentBlock { get; set; }
}