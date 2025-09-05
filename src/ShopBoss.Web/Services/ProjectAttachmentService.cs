using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class ProjectAttachmentService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<ProjectAttachmentService> _logger;
    private readonly string _fileStorageRoot;

    public ProjectAttachmentService(ShopBossDbContext context, ILogger<ProjectAttachmentService> logger, IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _fileStorageRoot = Path.Combine(environment.ContentRootPath, "ProjectFiles");
        
        // Ensure root directory exists
        Directory.CreateDirectory(_fileStorageRoot);
    }

    // Shared method for saving attachments from any source
    public async Task<ProjectAttachment> SaveAttachmentAsync(
        string projectId,
        string originalFileName,
        byte[] fileContent,
        string contentType,
        string label,
        string? uploadedBy = null,
        DateTime? uploadDate = null,
        string? comment = null,
        int? rowNumber = null,
        string? taskBlockId = null)
    {
        try
        {
            // Get human-readable folder name using ProjectId and ProjectName
            var projectFolderName = await GetProjectFolderNameAsync(projectId);
            var projectDir = Path.Combine(_fileStorageRoot, projectFolderName);
            Directory.CreateDirectory(projectDir);

            // Handle duplicate filenames by appending counter if needed
            var fileName = GetUniqueFileName(projectDir, originalFileName);
            var filePath = Path.Combine(projectDir, fileName);

            // Save file to disk
            await File.WriteAllBytesAsync(filePath, fileContent);

            // Create database record
            var attachment = new ProjectAttachment
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                FileName = fileName, // Store the final filename (may have counter appended)
                OriginalFileName = originalFileName, // Store the original filename
                FileSize = fileContent.Length,
                ContentType = contentType,
                Label = label,
                UploadedDate = uploadDate ?? DateTime.UtcNow,
                UploadedBy = uploadedBy
            };

            _context.ProjectAttachments.Add(attachment);
            
            // Get the next display order for the target container
            var maxOrder = await _context.ProjectEvents
                .Where(pe => pe.ProjectId == projectId && pe.ParentBlockId == taskBlockId)
                .MaxAsync(pe => (int?)pe.DisplayOrder) ?? 0;

            // Create timeline event for file upload
            // Store only the user comment in Description; file info comes from Attachment object
            var projectEvent = new ProjectEvent
            {
                ProjectId = projectId,
                EventDate = uploadDate ?? DateTime.UtcNow,
                EventType = "attachment",
                Description = comment ?? "",  // Only the user's comment
                CreatedBy = uploadedBy,
                AttachmentId = attachment.Id,
                RowNumber = rowNumber,
                ParentBlockId = taskBlockId,
                DisplayOrder = maxOrder + 1
            };
            _context.ProjectEvents.Add(projectEvent);
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Saved attachment {FileName} for project {ProjectId}", fileName, projectId);
            return attachment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving attachment {FileName} for project {ProjectId}", originalFileName, projectId);
            throw;
        }
    }

    // Helper method to handle duplicate filenames
    private string GetUniqueFileName(string directory, string originalFileName)
    {
        var fileName = originalFileName;
        var counter = 1;
        
        while (File.Exists(Path.Combine(directory, fileName)))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            var extension = Path.GetExtension(originalFileName);
            fileName = $"{nameWithoutExt}({counter}){extension}";
            counter++;
        }
        
        return fileName;
    }

    public async Task<ProjectAttachment> UploadAttachmentAsync(string projectId, IFormFile file, string label, string? uploadedBy = null, string? comment = null, string? taskBlockId = null)
    {
        try
        {
            // Convert IFormFile to byte array
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileContent = memoryStream.ToArray();

            // Use shared save method
            return await SaveAttachmentAsync(
                projectId,
                file.FileName,
                fileContent,
                file.ContentType,
                label,
                uploadedBy,
                comment: comment,
                taskBlockId: taskBlockId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} for project {ProjectId}", file.FileName, projectId);
            throw;
        }
    }

    public async Task<List<ProjectAttachment>> GetAttachmentsAsync(string projectId)
    {
        try
        {
            return await _context.ProjectAttachments
                .Where(a => a.ProjectId == projectId)
                .OrderByDescending(a => a.UploadedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<(Stream? stream, string? contentType, string? fileName)> DownloadAttachmentAsync(string attachmentId)
    {
        try
        {
            var attachment = await _context.ProjectAttachments
                .Include(a => a.Project)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment == null)
            {
                return (null, null, null);
            }

            // Use human-readable folder name: ProjectFiles/{ProjectId ProjectName}/{fileName}
            var projectFolderName = await GetProjectFolderNameAsync(attachment.ProjectId);
            var filePath = Path.Combine(_fileStorageRoot, projectFolderName, attachment.FileName);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return (null, null, null);
            }

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return (stream, attachment.ContentType, attachment.OriginalFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading attachment {AttachmentId}", attachmentId);
            throw;
        }
    }

    public async Task<bool> DeleteAttachmentAsync(string attachmentId)
    {
        try
        {
            var attachment = await _context.ProjectAttachments.FindAsync(attachmentId);
            if (attachment == null) return false;

            // Delete file from disk using human-readable folder structure
            var projectFolderName = await GetProjectFolderNameAsync(attachment.ProjectId);
            var filePath = Path.Combine(_fileStorageRoot, projectFolderName, attachment.FileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Remove any timeline events linked to this attachment
            var linkedEvents = await _context.ProjectEvents
                .Where(e => e.AttachmentId == attachment.Id)
                .ToListAsync();
            if (linkedEvents.Count > 0)
            {
                _context.ProjectEvents.RemoveRange(linkedEvents);
            }

            // Remove attachment from database
            _context.ProjectAttachments.Remove(attachment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted attachment {AttachmentId}", attachmentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId}", attachmentId);
            throw;
        }
    }

    public async Task<bool> UpdateAttachmentLabelAsync(string attachmentId, string newLabel)
    {
        try
        {
            var attachment = await _context.ProjectAttachments.FindAsync(attachmentId);
            if (attachment == null) return false;

            // Update the label
            if (attachment.Label != newLabel)
            {
                attachment.Label = newLabel;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated attachment {AttachmentId} label to {Label}", attachmentId, newLabel);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attachment {AttachmentId} label", attachmentId);
            throw;
        }
    }

    public async Task CleanupProjectFilesAsync(string projectId)
    {
        try
        {
            var projectFolderName = await GetProjectFolderNameAsync(projectId);
            var projectDir = Path.Combine(_fileStorageRoot, projectFolderName);
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, true);
                _logger.LogInformation("Cleaned up files for project {ProjectId}", projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up files for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<string> GetProjectFileDirectoryAsync(string projectId)
    {
        var projectFolderName = await GetProjectFolderNameAsync(projectId);
        return Path.Combine(_fileStorageRoot, projectFolderName);
    }

    private async Task<string> GetProjectFolderNameAsync(string projectId)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId);
        
        if (project == null)
        {
            _logger.LogWarning("Project not found with ID {ProjectId}, using fallback folder name", projectId);
            return SanitizeFolderName($"Unknown {projectId}");
        }

        var folderName = $"{project.ProjectId} {project.ProjectName}";
        return SanitizeFolderName(folderName);
    }

    private static string SanitizeFolderName(string folderName)
    {
        // Remove or replace invalid characters for file system paths
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = folderName;
        
        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // Also replace other problematic characters
        sanitized = sanitized.Replace('<', '_')
                            .Replace('>', '_')
                            .Replace(':', '_')
                            .Replace('"', '_')
                            .Replace('|', '_')
                            .Replace('?', '_')
                            .Replace('*', '_');

        // Trim whitespace and limit length to avoid path issues
        sanitized = sanitized.Trim();
        if (sanitized.Length > 200)
        {
            sanitized = sanitized.Substring(0, 200).Trim();
        }

        return sanitized;
    }
}
