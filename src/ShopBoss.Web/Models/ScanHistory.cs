using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

public class ScanHistory
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    public string Barcode { get; set; } = string.Empty;
    
    public string Station { get; set; } = string.Empty;
    
    public bool IsSuccessful { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    [ForeignKey("NestSheet")]
    public string? NestSheetId { get; set; }
    
    [ForeignKey("WorkOrder")]
    public string? WorkOrderId { get; set; }
    
    public int? PartsProcessed { get; set; }
    
    public string? SessionId { get; set; }
    
    public string? IPAddress { get; set; }
    
    public string Details { get; set; } = string.Empty;
    
    // Navigation properties
    public NestSheet? NestSheet { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}