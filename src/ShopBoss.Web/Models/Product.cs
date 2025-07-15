using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class Product
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
    
    public PartStatus Status { get; set; } = PartStatus.Pending;
    
    public DateTime? StatusUpdatedDate { get; set; }
    
    public WorkOrder WorkOrder { get; set; } = null!;
    
    public List<Part> Parts { get; set; } = new();
    
    public List<Subassembly> Subassemblies { get; set; } = new();
    
    public List<Hardware> Hardware { get; set; } = new();
}