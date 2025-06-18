namespace ShopBoss.Web.Models.Import;

public class ImportHardware
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string Category { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string WorkOrderId { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string? SubassemblyId { get; set; }
    
    // Additional properties
    public string Size { get; set; } = string.Empty;
    public string Finish { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    
    // Selection state for UI
    public bool IsSelected { get; set; } = true;
}