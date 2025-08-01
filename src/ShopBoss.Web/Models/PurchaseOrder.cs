using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class PurchaseOrder
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string ProjectId { get; set; } = string.Empty;
    
    public Project? Project { get; set; }
    
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    
    public string VendorName { get; set; } = string.Empty;
    
    public string? VendorContact { get; set; }
    
    public string? VendorPhone { get; set; }
    
    public string? VendorEmail { get; set; }
    
    public string Description { get; set; } = string.Empty;
    
    public DateTime OrderDate { get; set; }
    
    public DateTime? ExpectedDeliveryDate { get; set; }
    
    public DateTime? ActualDeliveryDate { get; set; }
    
    public decimal? TotalAmount { get; set; }
    
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Pending;
    
    public string? Notes { get; set; }
    
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}