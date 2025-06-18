namespace ShopBoss.Web.Models.Import;

public class ImportSubassembly
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Depth { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string? ParentSubassemblyId { get; set; }
    
    public List<ImportPart> Parts { get; set; } = new();
    public List<ImportSubassembly> NestedSubassemblies { get; set; } = new();
    public List<ImportHardware> Hardware { get; set; } = new();
    
    // Selection state for UI
    public bool IsSelected { get; set; } = true;
    public bool IsExpanded { get; set; } = false;
}