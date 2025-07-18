namespace ShopBoss.Web.Models.Api;

public class TreeDataResponse
{
    public string WorkOrderId { get; set; } = string.Empty;
    public string WorkOrderName { get; set; } = string.Empty;
    public List<TreeItem> Items { get; set; } = new();
    public WorkOrderStatistics? Statistics { get; set; }
}

public class TreeItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "product", "subassembly", "part", "hardware", "category"
    public int Quantity { get; set; }
    public string? Status { get; set; } // Only included when includeStatus=true
    public string? Category { get; set; } // Only included when includeStatus=true and Type=part
    public List<TreeItem> Children { get; set; } = new();
}

public class WorkOrderStatistics
{
    public ProductStatistics Products { get; set; } = new();
    public PartStatistics Parts { get; set; } = new();
    public SubassemblyStatistics Subassemblies { get; set; } = new();
    public HardwareStatistics Hardware { get; set; } = new();
    public NestSheetStatistics NestSheets { get; set; } = new();
}

public class ProductStatistics
{
    public int Total { get; set; }
    public StatusBreakdown StatusBreakdown { get; set; } = new();
}

public class PartStatistics
{
    public int Total { get; set; }
    public StatusBreakdown StatusBreakdown { get; set; } = new();
}

public class SubassemblyStatistics
{
    public int Total { get; set; }
    public StatusBreakdown StatusBreakdown { get; set; } = new();
}

public class HardwareStatistics
{
    public int Total { get; set; }
    public int TotalQuantity { get; set; }
    public StatusBreakdown StatusBreakdown { get; set; } = new();
}

public class NestSheetStatistics
{
    public int Total { get; set; }
    public int Processed { get; set; }
    public int Pending { get; set; }
    public int TotalParts { get; set; }
}

public class StatusBreakdown
{
    public int Pending { get; set; }
    public int Cut { get; set; }
    public int Sorted { get; set; }
    public int Assembled { get; set; }
    public int Shipped { get; set; }
}