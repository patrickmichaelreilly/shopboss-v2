using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class TimelineService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<TimelineService> _logger;

    public TimelineService(ShopBossDbContext context, ILogger<TimelineService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Timeline data retrieval
    public async Task<TimelineData> GetTimelineDataAsync(string projectId)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.Events)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                throw new ArgumentException($"Project not found: {projectId}", nameof(projectId));

            // Get only root-level blocks (no parent) with their full hierarchy
            var blocks = await _context.TaskBlocks
                .Where(tb => tb.ProjectId == projectId && tb.ParentTaskBlockId == null)
                .Include(tb => tb.Events)
                    .ThenInclude(e => e.Attachment)
                .Include(tb => tb.ChildTaskBlocks)
                    .ThenInclude(child => child.Events)
                        .ThenInclude(e => e.Attachment)
                .OrderBy(tb => tb.GlobalDisplayOrder ?? tb.DisplayOrder)
                .ToListAsync();

            // For deeper nesting, we need to recursively load child blocks
            await LoadAllChildrenRecursively(blocks);

            // Get all events for this project, ordered by GlobalDisplayOrder, then chronologically
            var allEvents = await _context.ProjectEvents
                .Where(pe => pe.ProjectId == projectId)
                .Include(pe => pe.Attachment)
                .Include(pe => pe.WorkOrder!)
                    .ThenInclude(wo => wo.NestSheets)
                .OrderBy(pe => pe.GlobalDisplayOrder ?? int.MaxValue)
                .ThenBy(pe => pe.EventDate)
                .ToListAsync();

            // Separate events into blocked and unblocked (including nested block events)
            var blockedEventIds = GetAllEventsFromBlocksRecursively(blocks).Select(e => e.Id).ToHashSet();
            var unblockedEvents = allEvents.Where(e => !blockedEventIds.Contains(e.Id)).ToList();

            return new TimelineData
            {
                Project = project,
                TaskBlocks = blocks,
                UnblockedEvents = unblockedEvents
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving timeline data for project: {ProjectId}", projectId);
            throw;
        }
    }

    // TaskBlock CRUD operations
    public async Task<TaskBlock> CreateTaskBlockAsync(string projectId, string name, string? description = null)
    {
        try
        {
            // Get the next display order
            var maxOrder = await _context.TaskBlocks
                .Where(tb => tb.ProjectId == projectId)
                .MaxAsync(tb => (int?)tb.DisplayOrder) ?? 0;

            var block = new TaskBlock
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                Name = name,
                Description = description,
                DisplayOrder = maxOrder + 1,
                IsTemplate = false
            };

            _context.TaskBlocks.Add(block);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created TaskBlock: {BlockName} for project: {ProjectId}", name, projectId);
            return block;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating TaskBlock: {BlockName} for project: {ProjectId}", name, projectId);
            throw;
        }
    }

    public async Task<TaskBlock?> UpdateTaskBlockAsync(string blockId, string name, string? description = null)
    {
        try
        {
            var block = await _context.TaskBlocks.FindAsync(blockId);
            if (block == null) return null;

            block.Name = name;
            block.Description = description;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated TaskBlock: {BlockId} - {BlockName}", blockId, name);
            return block;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating TaskBlock: {BlockId}", blockId);
            throw;
        }
    }

    public async Task<bool> DeleteTaskBlockAsync(string blockId)
    {
        try
        {
            var block = await _context.TaskBlocks
                .Include(tb => tb.Events)
                .FirstOrDefaultAsync(tb => tb.Id == blockId);

            if (block == null) return false;

            // Unassign all events from this block (don't delete the events)
            foreach (var evt in block.Events)
            {
                evt.TaskBlockId = null;
                evt.BlockDisplayOrder = null;
            }

            _context.TaskBlocks.Remove(block);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted TaskBlock: {BlockId}", blockId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TaskBlock: {BlockId}", blockId);
            throw;
        }
    }

    public async Task<bool> ReorderTaskBlocksAsync(string projectId, List<string> blockIdsInOrder)
    {
        try
        {
            var blocks = await _context.TaskBlocks
                .Where(tb => tb.ProjectId == projectId && blockIdsInOrder.Contains(tb.Id))
                .ToListAsync();

            for (int i = 0; i < blockIdsInOrder.Count; i++)
            {
                var block = blocks.FirstOrDefault(tb => tb.Id == blockIdsInOrder[i]);
                if (block != null)
                {
                    block.DisplayOrder = i + 1;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Reordered {Count} TaskBlocks for project: {ProjectId}", blocks.Count, projectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering TaskBlocks for project: {ProjectId}", projectId);
            throw;
        }
    }

    // Event assignment operations
    public async Task<bool> AssignEventsToBlockAsync(string blockId, List<string> eventIds)
    {
        try
        {
            var events = await _context.ProjectEvents
                .Where(pe => eventIds.Contains(pe.Id))
                .ToListAsync();

            // Get the next block display order for each event
            var maxOrder = await _context.ProjectEvents
                .Where(pe => pe.TaskBlockId == blockId)
                .MaxAsync(pe => (int?)pe.BlockDisplayOrder) ?? 0;

            for (int i = 0; i < events.Count; i++)
            {
                events[i].TaskBlockId = blockId;
                events[i].BlockDisplayOrder = maxOrder + i + 1;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Assigned {Count} events to TaskBlock: {BlockId}", events.Count, blockId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning events to TaskBlock: {BlockId}", blockId);
            throw;
        }
    }

    public async Task<bool> UnassignEventsFromBlockAsync(List<string> eventIds)
    {
        try
        {
            var events = await _context.ProjectEvents
                .Where(pe => eventIds.Contains(pe.Id))
                .ToListAsync();

            foreach (var evt in events)
            {
                evt.TaskBlockId = null;
                evt.BlockDisplayOrder = null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Unassigned {Count} events from their blocks", events.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning events from blocks");
            throw;
        }
    }

    public async Task<bool> ReorderEventsInBlockAsync(string blockId, List<string> eventIdsInOrder)
    {
        try
        {
            var events = await _context.ProjectEvents
                .Where(pe => pe.TaskBlockId == blockId && eventIdsInOrder.Contains(pe.Id))
                .ToListAsync();

            for (int i = 0; i < eventIdsInOrder.Count; i++)
            {
                var evt = events.FirstOrDefault(pe => pe.Id == eventIdsInOrder[i]);
                if (evt != null)
                {
                    evt.BlockDisplayOrder = i + 1;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Reordered {Count} events in TaskBlock: {BlockId}", events.Count, blockId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering events in TaskBlock: {BlockId}", blockId);
            throw;
        }
    }

    public async Task<bool> ReorderMixedTimelineItemsAsync(string projectId, List<(string Type, string Id, int Order)> items)
    {
        try
        {
            foreach (var (type, id, order) in items)
            {
                if (type == "TaskBlock")
                {
                    var block = await _context.TaskBlocks.FindAsync(id);
                    if (block != null) block.GlobalDisplayOrder = order;
                }
                else if (type == "Event")
                {
                    var evt = await _context.ProjectEvents.FindAsync(id);
                    if (evt != null) evt.GlobalDisplayOrder = order;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Reordered {Count} mixed timeline items for project: {ProjectId}", items.Count, projectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering mixed timeline items for project: {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<bool> NestTaskBlockAsync(string childBlockId, string? parentBlockId)
    {
        try
        {
            var childBlock = await _context.TaskBlocks.FindAsync(childBlockId);
            if (childBlock == null) return false;

            // Prevent circular references
            if (parentBlockId != null && await WouldCreateCircularReference(childBlockId, parentBlockId))
            {
                _logger.LogWarning("Prevented circular reference: Block {ChildId} cannot be nested under {ParentId}", childBlockId, parentBlockId);
                return false;
            }

            childBlock.ParentTaskBlockId = parentBlockId;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Nested TaskBlock {ChildId} under {ParentId}", childBlockId, parentBlockId ?? "root");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error nesting TaskBlock {ChildId} under {ParentId}", childBlockId, parentBlockId);
            throw;
        }
    }

    private async Task<bool> WouldCreateCircularReference(string childId, string potentialParentId)
    {
        // Check if potentialParent is actually a descendant of child
        var potentialParent = await _context.TaskBlocks
            .Include(tb => tb.ParentTaskBlock)
            .FirstOrDefaultAsync(tb => tb.Id == potentialParentId);

        while (potentialParent?.ParentTaskBlock != null)
        {
            if (potentialParent.ParentTaskBlockId == childId) return true;
            potentialParent = potentialParent.ParentTaskBlock;
        }

        return false;
    }

    private async Task LoadAllChildrenRecursively(IEnumerable<TaskBlock> taskBlocks)
    {
        foreach (var block in taskBlocks)
        {
            if (block.ChildTaskBlocks.Any())
            {
                // Load next level of children for each child block
                await _context.Entry(block)
                    .Collection(b => b.ChildTaskBlocks)
                    .Query()
                    .Include(child => child.Events)
                        .ThenInclude(e => e.Attachment)
                    .Include(child => child.ChildTaskBlocks)
                    .LoadAsync();

                // Recursively load deeper levels
                await LoadAllChildrenRecursively(block.ChildTaskBlocks);
            }
        }
    }

    private IEnumerable<ProjectEvent> GetAllEventsFromBlocksRecursively(IEnumerable<TaskBlock> blocks)
    {
        var allEvents = new List<ProjectEvent>();
        
        foreach (var block in blocks)
        {
            allEvents.AddRange(block.Events);
            allEvents.AddRange(GetAllEventsFromBlocksRecursively(block.ChildTaskBlocks));
        }
        
        return allEvents;
    }
}

// View model for timeline data
public class TimelineData
{
    public Project Project { get; set; } = null!;
    public List<TaskBlock> TaskBlocks { get; set; } = new();
    public List<ProjectEvent> UnblockedEvents { get; set; } = new();
}