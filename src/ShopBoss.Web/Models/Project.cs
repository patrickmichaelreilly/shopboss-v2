using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class Project
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string ProjectId { get; set; } = string.Empty;
    
    public string ProjectName { get; set; } = string.Empty;
    
    public DateTime? BidRequestDate { get; set; }
    
    public string? ProjectAddress { get; set; }
    
    public string? ProjectContact { get; set; }
    
    public string? ProjectContactPhone { get; set; }
    
    public string? ProjectContactEmail { get; set; }
    
    public string? GeneralContractor { get; set; }
    
    public string? ProjectManager { get; set; }
    
    public DateTime? TargetInstallDate { get; set; }
    
    public ProjectCategory ProjectCategory { get; set; }
    
    public string? Installer { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    public bool IsArchived { get; set; } = false;
    
    public DateTime? ArchivedDate { get; set; }
    
    // Navigation properties
    public List<WorkOrder> WorkOrders { get; set; } = new();
    
    public List<ProjectAttachment> Attachments { get; set; } = new();
}