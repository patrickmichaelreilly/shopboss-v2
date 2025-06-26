namespace ShopBoss.Web.Models.Import;

public class ImportNestSheet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public string Barcode { get; set; } = string.Empty;
    
    // Additional properties from import data
    public string Description { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    
    // Selection state for UI
    public bool IsSelected { get; set; } = true;
    
    // Parts that belong to this nest sheet
    public List<ImportPart> Parts { get; set; } = new();
}