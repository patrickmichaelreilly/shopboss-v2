using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public enum BinStatus
{
    Empty = 0,
    Partial = 1,
    Full = 2,
    Reserved = 3,
    Blocked = 4
}

public class Bin
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string StorageRackId { get; set; } = string.Empty;
    
    [Required]
    [Range(1, 50)]
    public int Row { get; set; }
    
    [Required]
    [Range(1, 50)]
    public int Column { get; set; }
    
    [Required]
    public BinStatus Status { get; set; } = BinStatus.Empty;
    
    public string? PartId { get; set; }
    public string? ProductId { get; set; }
    public string? WorkOrderId { get; set; }
    
    [StringLength(200)]
    public string Contents { get; set; } = string.Empty;
    
    public int PartsCount { get; set; } = 0;
    public int MaxCapacity { get; set; } = 50;
    
    public DateTime? AssignedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    
    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual StorageRack StorageRack { get; set; } = null!;
    public virtual Part? Part { get; set; }
    public virtual Product? Product { get; set; }
    public virtual WorkOrder? WorkOrder { get; set; }
    
    // Computed properties
    public bool IsOccupied => Status != BinStatus.Empty && Status != BinStatus.Blocked;
    public bool IsAvailable => Status == BinStatus.Empty && !IsBlocked;
    public bool IsBlocked => Status == BinStatus.Blocked;
    public string BinLabel => $"{(char)('A' + Row - 1)}{Column:D2}";
}