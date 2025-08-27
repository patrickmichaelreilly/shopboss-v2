using ShopBoss.Web.Models;

namespace ShopBoss.Web.Models;

public class SmartSheetLinkInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Permalink { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public int RowCount { get; set; }
}

/// <summary>
/// Response model for SmartSheet token API
/// </summary>
public class SmartSheetTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }
}

// Migration Tool Models
public class WorkspaceListResult
{
    public List<SheetInfo> ActiveJobs { get; set; } = new();
    public List<SheetInfo> ArchivedJobs { get; set; } = new();
}

public class SheetInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
}

public class SheetDetailsResult
{
    public long SheetId { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public Dictionary<string, string> Summary { get; set; } = new();
    public List<AttachmentInfo> Attachments { get; set; } = new();
    public List<CommentInfo> Comments { get; set; } = new();
}

public class AttachmentInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? SizeInKb { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public int? RowNumber { get; set; }
    public string? AttachmentType { get; set; }
    public string? MimeType { get; set; }
    public string? Url { get; set; }
}

public class CommentInfo
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public int? RowNumber { get; set; }
}

public class ImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
}

// Simplified import request - now only requires sheetId and importId for progress tracking
public class ImportProjectRequest
{
    public long SheetId { get; set; }
    public string ImportId { get; set; } = string.Empty;
}

// Internal data class for mapping SmartSheet Summary to project fields
public class ProjectData
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectManager { get; set; }
    public string? ProjectContact { get; set; }
    public string? ProjectContactPhone { get; set; }
    public string? ProjectContactEmail { get; set; }
    public string? ProjectAddress { get; set; }
    public string? GeneralContractor { get; set; }
    public string? Installer { get; set; }
    public DateTime? TargetInstallDate { get; set; }
    public ProjectCategory ProjectCategory { get; set; } = ProjectCategory.StandardProducts;
}