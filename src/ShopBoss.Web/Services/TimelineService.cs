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

            // Get all blocks for this project with their events
            var allBlocks = await _context.TaskBlocks
                .Where(tb => tb.ProjectId == projectId)
                .Include(tb => tb.Events)
                    .ThenInclude(e => e.Attachment)
                .Include(tb => tb.ChildTaskBlocks)
                .AsSplitQuery()
                .ToListAsync();

            // Get all events for this project
            var allEvents = await _context.ProjectEvents
                .Where(pe => pe.ProjectId == projectId)
                .Include(pe => pe.Attachment)
                .Include(pe => pe.WorkOrder!)
                    .ThenInclude(wo => wo.NestSheets)
                .AsSplitQuery()
                .ToListAsync();

            // Build the hierarchical structure
            var rootBlocks = allBlocks.Where(tb => tb.ParentTaskBlockId == null)
                .OrderBy(tb => tb.DisplayOrder)
                .ToList();

            var rootEvents = allEvents.Where(pe => pe.ParentBlockId == null)
                .OrderBy(pe => pe.DisplayOrder)
                .ToList();

            return new TimelineData
            {
                Project = project,
                RootBlocks = rootBlocks,
                RootEvents = rootEvents,
                AllBlocks = allBlocks,
                AllEvents = allEvents
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
            // Load block with direct children (events and child blocks)
            var block = await _context.TaskBlocks
                .Include(tb => tb.Events)
                .Include(tb => tb.ChildTaskBlocks)
                .FirstOrDefaultAsync(tb => tb.Id == blockId);

            if (block == null) return false;

            var parentId = block.ParentTaskBlockId; // null for root
            var insertionOrder = block.DisplayOrder;

            // Build mixed ordered children (by DisplayOrder within the block)
            var childBlocks = (block.ChildTaskBlocks ?? new List<TaskBlock>())
                .OrderBy(b => b.DisplayOrder)
                .ToList();
            var childEvents = (block.Events ?? new List<ProjectEvent>())
                .OrderBy(e => e.DisplayOrder)
                .ToList();

            var mixedChildren = new List<(string Type, object Entity)>();
            int i = 0, j = 0;
            while (i < childBlocks.Count || j < childEvents.Count)
            {
                var nextIsBlock = j >= childEvents.Count || (i < childBlocks.Count && childBlocks[i].DisplayOrder <= childEvents[j].DisplayOrder);
                if (nextIsBlock)
                {
                    mixedChildren.Add(("TaskBlock", childBlocks[i++]));
                }
                else
                {
                    mixedChildren.Add(("Event", childEvents[j++]));
                }
            }

            // Compute shift: children replacing the deleted block at its index
            var shift = Math.Max(mixedChildren.Count - 1, 0);

            // Shift siblings after the block forward by 'shift'
            if (shift > 0)
            {
                // Sibling blocks under same parent (exclude the block itself)
                var siblingBlocks = _context.TaskBlocks.Where(tb => tb.ProjectId == block.ProjectId && tb.ParentTaskBlockId == parentId && tb.Id != block.Id);
                await siblingBlocks.Where(tb => tb.DisplayOrder > insertionOrder).ForEachAsync(tb => tb.DisplayOrder += shift);

                // Sibling events under same parent
                var siblingEvents = _context.ProjectEvents.Where(pe => pe.ProjectId == block.ProjectId && pe.ParentBlockId == parentId);
                await siblingEvents.Where(pe => pe.DisplayOrder > insertionOrder).ForEachAsync(pe => pe.DisplayOrder += shift);
            }

            // Reparent children into the parent's container at the block's position
            var order = insertionOrder;
            foreach (var (type, entity) in mixedChildren)
            {
                if (type == "TaskBlock")
                {
                    var child = (TaskBlock)entity;
                    child.ParentTaskBlockId = parentId;
                    child.DisplayOrder = order++;
                }
                else // Event
                {
                    var evt = (ProjectEvent)entity;
                    evt.ParentBlockId = parentId;
                    evt.DisplayOrder = order++;
                }
            }

            // Finally, remove the block itself
            _context.TaskBlocks.Remove(block);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted TaskBlock {BlockId} and spliced {Count} children into parent", blockId, mixedChildren.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TaskBlock: {BlockId}", blockId);
            throw;
        }
    }


    // Event assignment operations (now handled by ReorderItemsAsync)
    public async Task<bool> AssignEventsToBlockAsync(string blockId, List<string> eventIds)
    {
        try
        {
            var events = await _context.ProjectEvents
                .Where(pe => eventIds.Contains(pe.Id))
                .ToListAsync();

            // Get the next display order for each event
            var maxOrder = await _context.ProjectEvents
                .Where(pe => pe.ParentBlockId == blockId)
                .MaxAsync(pe => (int?)pe.DisplayOrder) ?? 0;

            for (int i = 0; i < events.Count; i++)
            {
                events[i].ParentBlockId = blockId;
                events[i].DisplayOrder = maxOrder + i + 1;
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
                evt.ParentBlockId = null;
                evt.DisplayOrder = 0;
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

    /// <summary>
    /// Unified method to reorder any items within a parent container
    /// </summary>
    /// <param name="parentBlockId">Parent TaskBlock ID, or null for root level</param>
    /// <param name="items">List of items with Type ("TaskBlock" or "Event"), Id, and Order</param>
    public async Task<bool> ReorderItemsAsync(string? parentBlockId, List<(string Type, string Id, int Order)> items)
    {
        try
        {
            foreach (var (type, id, order) in items)
            {
                if (type == "TaskBlock")
                {
                    var block = await _context.TaskBlocks.FindAsync(id);
                    if (block != null)
                    {
                        block.DisplayOrder = order;
                        // Ensure parent relationship is correct
                        block.ParentTaskBlockId = parentBlockId;
                    }
                }
                else if (type == "Event")
                {
                    var evt = await _context.ProjectEvents.FindAsync(id);
                    if (evt != null)
                    {
                        evt.DisplayOrder = order;
                        // Ensure parent relationship is correct
                        evt.ParentBlockId = parentBlockId;
                    }
                }
            }

            await _context.SaveChangesAsync();
            
            var containerName = parentBlockId != null ? $"TaskBlock: {parentBlockId}" : "root level";
            _logger.LogInformation("Reordered {Count} items in {Container}", items.Count, containerName);
            return true;
        }
        catch (Exception ex)
        {
            var containerName = parentBlockId != null ? $"TaskBlock: {parentBlockId}" : "root level";
            _logger.LogError(ex, "Error reordering items in {Container}", containerName);
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

}

// View model for timeline data
public class TimelineData
{
    public Project Project { get; set; } = null!;
    public List<TaskBlock> RootBlocks { get; set; } = new();
    public List<ProjectEvent> RootEvents { get; set; } = new();
    public List<TaskBlock> AllBlocks { get; set; } = new();
    public List<ProjectEvent> AllEvents { get; set; } = new();
}
