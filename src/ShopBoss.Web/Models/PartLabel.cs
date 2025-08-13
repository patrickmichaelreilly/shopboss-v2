using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopBoss.Web.Models;

/// <summary>
/// Stores individual part labels parsed from Microvellum HTML label files
/// </summary>
public class PartLabel
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to Part.Id (which is the barcode)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string PartId { get; set; } = string.Empty;
    
    /// <summary>
    /// Foreign key to WorkOrder.Id
    /// </summary>
    [Required]
    [StringLength(100)]
    public string WorkOrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional foreign key to NestSheet.Id for additional context and disambiguation
    /// </summary>
    [StringLength(100)]
    public string? NestSheetId { get; set; }
    
    /// <summary>
    /// Complete HTML content for this individual label
    /// </summary>
    public string LabelHtml { get; set; } = string.Empty;
    
    /// <summary>
    /// When this label was imported
    /// </summary>
    public DateTime ImportedDate { get; set; } = DateTime.Now;
    
    // Navigation properties
    [ForeignKey("PartId")]
    public virtual Part? Part { get; set; }
    
    [ForeignKey("WorkOrderId")]
    public virtual WorkOrder? WorkOrder { get; set; }
    
    [ForeignKey("NestSheetId")]
    public virtual NestSheet? NestSheet { get; set; }
}