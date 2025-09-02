using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public enum BlockType
{
    Generic = 0
    // Future types: Materials, Checklist, Documentation, Communication, Milestone
}

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
    
    // Nesting support - self-referencing relationship
    public string? ParentTaskBlockId { get; set; }
    
    public bool IsTemplate { get; set; } = false;
    
    // Block template type (for future specialization)
    public BlockType BlockType { get; set; } = BlockType.Generic;
    
    // Navigation properties
    public Project Project { get; set; } = null!;
    public TaskBlock? ParentTaskBlock { get; set; }
    public ICollection<TaskBlock> ChildTaskBlocks { get; set; } = new List<TaskBlock>();
    public ICollection<ProjectEvent> Events { get; set; } = new List<ProjectEvent>();
}