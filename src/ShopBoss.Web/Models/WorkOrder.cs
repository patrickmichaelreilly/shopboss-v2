using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class WorkOrder
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public DateTime ImportedDate { get; set; }
    
    public List<Product> Products { get; set; } = new();
    
    public List<Hardware> Hardware { get; set; } = new();
    
    public List<DetachedProduct> DetachedProducts { get; set; } = new();
    
    public List<NestSheet> NestSheets { get; set; } = new();
}