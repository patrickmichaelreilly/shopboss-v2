namespace ShopBoss.Web.Models.Import;

public class ImportWorkOrder
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime ImportedDate { get; set; } = DateTime.Now;
    
    public List<ImportProduct> Products { get; set; } = new();
    public List<ImportHardware> Hardware { get; set; } = new();
    public List<ImportDetachedProduct> DetachedProducts { get; set; } = new();
    
    // Import statistics
    public ImportStatistics Statistics { get; set; } = new();
}

public class ImportStatistics
{
    public int TotalProducts { get; set; }
    public int TotalParts { get; set; }
    public int TotalSubassemblies { get; set; }
    public int TotalHardware { get; set; }
    public int TotalDetachedProducts { get; set; }
    public List<string> Warnings { get; set; } = new();
}