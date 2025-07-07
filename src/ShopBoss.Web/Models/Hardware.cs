using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class Hardware
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string MicrovellumId { get; set; } = string.Empty;
    
    [ForeignKey("WorkOrder")]
    public string WorkOrderId { get; set; } = string.Empty;
    
    [ForeignKey("Product")]
    public string? ProductId { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public int Qty { get; set; }
    
    public PartStatus Status { get; set; } = PartStatus.Pending;
    
    public bool IsShipped { get; set; } = false;
    
    public DateTime? ShippedDate { get; set; }
    
    public WorkOrder WorkOrder { get; set; } = null!;
    
    public Product? Product { get; set; }
}