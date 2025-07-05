namespace ShopBoss.Web.Models.Api;

// Response wrapper classes
public class TreeDataResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public TreeData? Data { get; set; }
    public PaginationInfo? Pagination { get; set; }
}

public class ProductDetailsResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ProductTreeNode? Product { get; set; }
}

public class WorkOrderSummaryResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public WorkOrderSummary? WorkOrder { get; set; }
}

// Data transfer objects
public class TreeData
{
    public WorkOrderTreeNode WorkOrder { get; set; } = null!;
    public List<ProductTreeNode> ProductNodes { get; set; } = new();
    public NestSheetSummaryInfo NestSheetSummary { get; set; } = new();
}

public class WorkOrderTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime ImportedDate { get; set; }
    public List<HardwareTreeNode> Hardware { get; set; } = new();
    public List<DetachedProductTreeNode> DetachedProducts { get; set; } = new();
}

public class ProductTreeNode
{
    public ProductInfo Product { get; set; } = null!;
    public List<PartTreeNode> Parts { get; set; } = new();
    public List<SubassemblyTreeNode> Subassemblies { get; set; } = new();
    public string EffectiveStatus { get; set; } = "Pending";
}

public class ProductInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public int Qty { get; set; }
}

public class PartTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Qty { get; set; }
    public double Length { get; set; }
    public double Width { get; set; }
    public double Thickness { get; set; }
    public string Material { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}

public class SubassemblyTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Qty { get; set; }
    public List<PartTreeNode> Parts { get; set; } = new();
}

public class HardwareTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Qty { get; set; }
    public bool IsShipped { get; set; }
}

public class DetachedProductTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public int Qty { get; set; }
    public double Length { get; set; }
    public double Width { get; set; }
    public double Thickness { get; set; }
    public bool IsShipped { get; set; }
}

public class NestSheetSummaryInfo
{
    public int TotalNestSheets { get; set; }
    public int ProcessedNestSheets { get; set; }
    public int PendingNestSheets { get; set; }
    public int TotalPartsOnNestSheets { get; set; }
}

public class WorkOrderSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime ImportedDate { get; set; }
}

public class PaginationInfo
{
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}