using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class Subassembly
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [ForeignKey("Product")]
    public string? ProductId { get; set; }
    
    [ForeignKey("ParentSubassembly")]
    public string? ParentSubassemblyId { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public int Qty { get; set; }
    
    public decimal? Length { get; set; }
    
    public decimal? Width { get; set; }
    
    public Product? Product { get; set; }
    
    public Subassembly? ParentSubassembly { get; set; }
    
    public List<Subassembly> ChildSubassemblies { get; set; } = new();
    
    public List<Part> Parts { get; set; } = new();
}