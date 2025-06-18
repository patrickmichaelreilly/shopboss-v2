using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class DetachedProduct
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    public string ProductNumber { get; set; } = string.Empty;
    
    [ForeignKey("WorkOrder")]
    public string WorkOrderId { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public int Qty { get; set; }
    
    public decimal? Length { get; set; }
    
    public decimal? Width { get; set; }
    
    public decimal? Thickness { get; set; }
    
    public string Material { get; set; } = string.Empty;
    
    public string EdgebandingTop { get; set; } = string.Empty;
    
    public string EdgebandingBottom { get; set; } = string.Empty;
    
    public string EdgebandingLeft { get; set; } = string.Empty;
    
    public string EdgebandingRight { get; set; } = string.Empty;
    
    public WorkOrder WorkOrder { get; set; } = null!;
}