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

    public async Task<ProjectAttachment> UploadAttachmentAsync(string projectId, IFormFile file, string category, string? uploadedBy = null)
    {
        try
        {
            // Create project directory structure
            var projectDir = Path.Combine(_fileStorageRoot, projectId, category);
            Directory.CreateDirectory(projectDir);

            // Generate unique filename while preserving original
            var fileExtension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(projectDir, uniqueFileName);

            // Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Create database record
            var attachment = new ProjectAttachment
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                FileName = uniqueFileName,
                OriginalFileName = file.FileName,
                FileSize = file.Length,
                ContentType = file.ContentType,
                Category = category,
                UploadedDate = DateTime.UtcNow,
                UploadedBy = uploadedBy
            };

            _context.ProjectAttachments.Add(attachment);
            
            // Create timeline event for file upload
            var projectEvent = new ProjectEvent
            {
                ProjectId = projectId,
                EventDate = DateTime.UtcNow,
                EventType = "attachment",
                Description = $"File uploaded: {file.FileName} ({file.Length / 1024:F1} KB)",
                CreatedBy = uploadedBy,
                AttachmentId = attachment.Id
            };
            _context.ProjectEvents.Add(projectEvent);
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Uploaded file {FileName} for project {ProjectId}", file.FileName, projectId);
            return attachment;
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

            var filePath = Path.Combine(_fileStorageRoot, attachment.ProjectId, attachment.Category, attachment.FileName);
            
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

            // Delete file from disk
            var filePath = Path.Combine(_fileStorageRoot, attachment.ProjectId, attachment.Category, attachment.FileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Create timeline event for file deletion
            var projectEvent = new ProjectEvent
            {
                ProjectId = attachment.ProjectId,
                EventDate = DateTime.UtcNow,
                EventType = "attachment",
                Description = $"File deleted: {attachment.OriginalFileName}",
                CreatedBy = null // Could be passed as parameter if needed
            };
            _context.ProjectEvents.Add(projectEvent);

            // Remove from database
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

    public async Task<bool> UpdateAttachmentCategoryAsync(string attachmentId, string newCategory)
    {
        try
        {
            var attachment = await _context.ProjectAttachments.FindAsync(attachmentId);
            if (attachment == null) return false;

            // Move file to new category directory if category changed
            if (attachment.Category != newCategory)
            {
                var oldPath = Path.Combine(_fileStorageRoot, attachment.ProjectId, attachment.Category, attachment.FileName);
                var newCategoryDir = Path.Combine(_fileStorageRoot, attachment.ProjectId, newCategory);
                Directory.CreateDirectory(newCategoryDir);
                var newPath = Path.Combine(newCategoryDir, attachment.FileName);

                if (File.Exists(oldPath))
                {
                    File.Move(oldPath, newPath);
                }

                // Update database record
                attachment.Category = newCategory;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated attachment {AttachmentId} category to {Category}", attachmentId, newCategory);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attachment {AttachmentId} category", attachmentId);
            throw;
        }
    }

    public Task CleanupProjectFilesAsync(string projectId)
    {
        try
        {
            var projectDir = Path.Combine(_fileStorageRoot, projectId);
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, true);
                _logger.LogInformation("Cleaned up files for project {ProjectId}", projectId);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up files for project {ProjectId}", projectId);
            throw;
        }
    }

    public string GetProjectFileDirectory(string projectId)
    {
        return Path.Combine(_fileStorageRoot, projectId);
    }
}