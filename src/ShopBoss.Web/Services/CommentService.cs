using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class CommentService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<CommentService> _logger;

    public CommentService(ShopBossDbContext context, ILogger<CommentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Create a new comment and associated timeline event
    /// </summary>
    public async Task<bool> CreateCommentAsync(string projectId, string description, DateTime eventDate, string? createdBy, string? parentBlockId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID is required", nameof(projectId));
            
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Comment description is required", nameof(description));

            // Get the next display order for the target container
            var maxOrder = await _context.ProjectEvents
                .Where(pe => pe.ProjectId == projectId && pe.ParentBlockId == parentBlockId)
                .MaxAsync(pe => (int?)pe.DisplayOrder) ?? 0;

            // Create timeline event for the comment
            var commentEvent = new ProjectEvent
            {
                ProjectId = projectId,
                EventDate = eventDate,
                EventType = "comment",
                Description = description,
                CreatedBy = createdBy,
                ParentBlockId = parentBlockId,
                DisplayOrder = maxOrder + 1
            };

            _context.ProjectEvents.Add(commentEvent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created comment for project {ProjectId} in container {ParentBlockId}", projectId, parentBlockId ?? "root");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment for project {ProjectId}", projectId);
            return false;
        }
    }

    /// <summary>
    /// Update an existing comment's description
    /// </summary>
    public async Task<bool> UpdateCommentAsync(string eventId, string description)
    {
        try
        {
            var commentEvent = await _context.ProjectEvents
                .FirstOrDefaultAsync(pe => pe.Id == eventId && pe.EventType == "comment");

            if (commentEvent == null)
            {
                _logger.LogWarning("Comment event {EventId} not found for update", eventId);
                return false;
            }

            commentEvent.Description = description;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated comment {EventId}", eventId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating comment {EventId}", eventId);
            return false;
        }
    }

    /// <summary>
    /// Delete a comment
    /// </summary>
    public async Task<bool> DeleteCommentAsync(string eventId)
    {
        try
        {
            var commentEvent = await _context.ProjectEvents
                .FirstOrDefaultAsync(pe => pe.Id == eventId && pe.EventType == "comment");

            if (commentEvent == null)
            {
                _logger.LogWarning("Comment event {EventId} not found for deletion", eventId);
                return false;
            }

            _context.ProjectEvents.Remove(commentEvent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted comment {EventId}", eventId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment {EventId}", eventId);
            return false;
        }
    }

    /// <summary>
    /// Get all comments for a project
    /// </summary>
    public async Task<List<ProjectEvent>> GetProjectCommentsAsync(string projectId)
    {
        try
        {
            return await _context.ProjectEvents
                .Where(pe => pe.ProjectId == projectId && pe.EventType == "comment")
                .OrderBy(pe => pe.EventDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving comments for project {ProjectId}", projectId);
            return new List<ProjectEvent>();
        }
    }
}