using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class Part
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [ForeignKey("Product")]
    public string? ProductId { get; set; }
    
    [ForeignKey("Subassembly")]
    public string? SubassemblyId { get; set; }
    
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
    
    public Product? Product { get; set; }
    
    public Subassembly? Subassembly { get; set; }
}