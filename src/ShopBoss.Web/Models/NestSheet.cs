using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class NestSheet
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [ForeignKey("WorkOrder")]
    public string WorkOrderId { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public string Material { get; set; } = string.Empty;
    
    public decimal? Length { get; set; }
    
    public decimal? Width { get; set; }
    
    public decimal? Thickness { get; set; }
    
    public string Barcode { get; set; } = string.Empty;
    
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    public bool IsProcessed { get; set; } = false;
    
    public DateTime? ProcessedDate { get; set; }
    
    public WorkOrder WorkOrder { get; set; } = null!;
    
    public List<Part> Parts { get; set; } = new();
}