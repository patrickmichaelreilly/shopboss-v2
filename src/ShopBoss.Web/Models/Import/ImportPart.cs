namespace ShopBoss.Web.Models.Import;

public class ImportPart
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Thickness { get; set; }
    public string Material { get; set; } = string.Empty;
    public string EdgeBanding { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string? SubassemblyId { get; set; }
    
    // Additional properties from import data
    public string GrainDirection { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    
    // Selection state for UI
    public bool IsSelected { get; set; } = true;
}