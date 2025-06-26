using System.ComponentModel.DataAnnotations;

namespace ShopBoss.Web.Models;

public class AuditLog
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string Action { get; set; } = string.Empty;
    
    public string EntityType { get; set; } = string.Empty;
    
    public string EntityId { get; set; } = string.Empty;
    
    public string? OldValue { get; set; }
    
    public string? NewValue { get; set; }
    
    public string? UserId { get; set; }
    
    public string Station { get; set; } = string.Empty;
    
    public string? WorkOrderId { get; set; }
    
    public string Details { get; set; } = string.Empty;
    
    public string? SessionId { get; set; }
    
    public string? IPAddress { get; set; }
}