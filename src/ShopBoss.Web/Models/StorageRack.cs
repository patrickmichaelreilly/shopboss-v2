using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public enum RackType
{
    Standard = 0,
    DoorsAndDrawerFronts = 1,
    AdjustableShelves = 2,
    Hardware = 3,
    Cart = 4
}

public class StorageRack
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public RackType Type { get; set; } = RackType.Standard;
    
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [Range(1, 50)]
    public int Rows { get; set; } = 4;
    
    [Required]
    [Range(1, 50)]
    public int Columns { get; set; } = 8;
    
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    
    [StringLength(100)]
    public string Location { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    public bool IsPortable { get; set; } = false;
    
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    
    // Navigation properties
    public virtual ICollection<Bin> Bins { get; set; } = new List<Bin>();
    
    // Computed properties
    public int TotalBins => Rows * Columns;
    public int OccupiedBins => Bins.Count(b => b.IsOccupied);
    public int AvailableBins => TotalBins - OccupiedBins;
    public double OccupancyPercentage => TotalBins > 0 ? (double)OccupiedBins / TotalBins * 100 : 0;
}