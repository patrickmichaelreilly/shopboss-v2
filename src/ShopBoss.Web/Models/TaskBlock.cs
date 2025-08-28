using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class TaskBlock
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string ProjectId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    public int DisplayOrder { get; set; }
    
    // Global timeline ordering (for mixed TaskBlock/Event ordering)
    public int? GlobalDisplayOrder { get; set; }
    
    public bool IsTemplate { get; set; } = false;
    
    // Navigation properties
    public Project Project { get; set; } = null!;
    public ICollection<ProjectEvent> Events { get; set; } = new List<ProjectEvent>();
}