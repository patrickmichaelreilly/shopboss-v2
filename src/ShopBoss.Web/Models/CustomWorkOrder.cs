using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class CustomWorkOrder
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string ProjectId { get; set; } = string.Empty;
    
    public Project? Project { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public CustomWorkOrderType WorkOrderType { get; set; } = CustomWorkOrderType.Other;
    
    public string Description { get; set; } = string.Empty;
    
    public string? AssignedTo { get; set; }
    
    public decimal? EstimatedHours { get; set; }
    
    public decimal? ActualHours { get; set; }
    
    public CustomWorkOrderStatus Status { get; set; } = CustomWorkOrderStatus.Pending;
    
    public DateTime? StartDate { get; set; }
    
    public DateTime? CompletedDate { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}