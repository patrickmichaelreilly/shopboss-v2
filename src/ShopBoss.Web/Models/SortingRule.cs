using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class SortingRule
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public int Priority { get; set; } = 0; // Lower number = higher priority
    
    [Required]
    [StringLength(500)]
    public string Keywords { get; set; } = string.Empty; // Comma-separated keywords
    
    [Required]
    public RackType TargetRackType { get; set; } = RackType.Standard;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? LastModifiedDate { get; set; }
    
    // Helper method to get keywords as a list
    public List<string> GetKeywordsList()
    {
        return Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(k => k.Trim().ToUpperInvariant())
                      .Where(k => !string.IsNullOrEmpty(k))
                      .ToList();
    }
    
    // Helper method to check if a part name matches any keywords
    public bool MatchesPartName(string partName)
    {
        if (string.IsNullOrEmpty(partName))
            return false;
            
        var upperPartName = partName.ToUpperInvariant();
        return GetKeywordsList().Any(keyword => upperPartName.Contains(keyword));
    }
}