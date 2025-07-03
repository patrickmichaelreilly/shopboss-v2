using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class Hardware
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [ForeignKey("WorkOrder")]
    public string WorkOrderId { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public int Qty { get; set; }
    
    public bool IsShipped { get; set; } = false;
    
    public DateTime? ShippedDate { get; set; }
    
    public WorkOrder WorkOrder { get; set; } = null!;
}