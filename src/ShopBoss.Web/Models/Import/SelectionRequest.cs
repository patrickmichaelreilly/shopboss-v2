namespace ShopBoss.Web.Models.Import;

public class SelectionRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string WorkOrderName { get; set; } = string.Empty;
    public List<string> SelectedItemIds { get; set; } = new();
    public Dictionary<string, SelectionItemInfo> SelectionDetails { get; set; } = new();
}

public class SelectionItemInfo
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty; // product, part, subassembly, hardware, detached
    public bool Selected { get; set; }
    public string? ParentId { get; set; }
    public List<string> ChildIds { get; set; } = new();
}

public class ImportConversionResult
{
    public bool Success { get; set; }
    public string? WorkOrderId { get; set; }
    public ConversionStatistics Statistics { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ConversionStatistics
{
    public int ConvertedProducts { get; set; }
    public int ConvertedParts { get; set; }
    public int ConvertedSubassemblies { get; set; }
    public int ConvertedHardware { get; set; }
    public int ConvertedDetachedProducts { get; set; }
    public int ConvertedNestSheets { get; set; }
    public int SkippedItems { get; set; }
}