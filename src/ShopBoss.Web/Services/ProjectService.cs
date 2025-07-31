using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class ProjectService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(ShopBossDbContext context, ILogger<ProjectService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Project>> GetProjectSummariesAsync(string? search = null, bool includeArchived = false, ProjectCategory? projectCategory = null)
    {
        try
        {
            var query = _context.Projects
                .Include(p => p.WorkOrders)
                .Include(p => p.Attachments)
                .AsQueryable();

            if (!includeArchived)
            {
                query = query.Where(p => !p.IsArchived);
            }

            if (projectCategory.HasValue)
            {
                query = query.Where(p => p.ProjectCategory == projectCategory.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.ToLower();
                query = query.Where(p => 
                    p.ProjectId.ToLower().Contains(searchTerm) ||
                    p.ProjectName.ToLower().Contains(searchTerm) ||
                    (p.ProjectAddress != null && p.ProjectAddress.ToLower().Contains(searchTerm)) ||
                    (p.ProjectContact != null && p.ProjectContact.ToLower().Contains(searchTerm)) ||
                    (p.GeneralContractor != null && p.GeneralContractor.ToLower().Contains(searchTerm))
                );
            }

            return await query
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project summaries");
            throw;
        }
    }

    public async Task<Project?> GetProjectByIdAsync(string id)
    {
        try
        {
            return await _context.Projects
                .Include(p => p.WorkOrders)
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project with ID: {ProjectId}", id);
            throw;
        }
    }

    public async Task<Project> CreateProjectAsync(Project project)
    {
        try
        {
            project.Id = Guid.NewGuid().ToString();
            project.CreatedDate = DateTime.UtcNow;
            project.IsArchived = false;

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new project: {ProjectId} - {ProjectName}", project.ProjectId, project.ProjectName);
            return project;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
        {
            _logger.LogWarning("Duplicate project ID attempted: {ProjectId}", project.ProjectId);
            throw new InvalidOperationException($"A project with ID '{project.ProjectId}' already exists. Please use a different Project ID.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project: {ProjectId}", project.ProjectId);
            throw;
        }
    }

    public async Task<Project> UpdateProjectAsync(Project project)
    {
        try
        {
            _context.Projects.Update(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated project: {ProjectId} - {ProjectName}", project.ProjectId, project.ProjectName);
            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project: {ProjectId}", project.ProjectId);
            throw;
        }
    }

    public async Task<bool> ArchiveProjectAsync(string id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) return false;

            project.IsArchived = true;
            project.ArchivedDate = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Archived project: {ProjectId}", project.ProjectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving project with ID: {ProjectId}", id);
            throw;
        }
    }

    public async Task<bool> UnarchiveProjectAsync(string id)
    {
        try
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) return false;

            project.IsArchived = false;
            project.ArchivedDate = null;
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Unarchived project: {ProjectId}", project.ProjectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unarchiving project with ID: {ProjectId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteProjectAsync(string id)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.WorkOrders)
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.Id == id);
                
            if (project == null) return false;

            // Detach work orders from project (don't delete them)
            foreach (var workOrder in project.WorkOrders)
            {
                workOrder.ProjectId = null;
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted project: {ProjectId}", project.ProjectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project with ID: {ProjectId}", id);
            throw;
        }
    }

    public async Task<bool> AttachWorkOrdersToProjectAsync(List<string> workOrderIds, string projectId)
    {
        try
        {
            var workOrders = await _context.WorkOrders
                .Where(w => workOrderIds.Contains(w.Id))
                .ToListAsync();

            foreach (var workOrder in workOrders)
            {
                workOrder.ProjectId = projectId;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Attached {Count} work orders to project: {ProjectId}", workOrders.Count, projectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching work orders to project: {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<bool> DetachWorkOrderFromProjectAsync(string workOrderId)
    {
        try
        {
            var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
            if (workOrder == null) return false;

            workOrder.ProjectId = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Detached work order {WorkOrderId} from project", workOrderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detaching work order: {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<List<WorkOrder>> GetUnassignedWorkOrdersAsync()
    {
        try
        {
            return await _context.WorkOrders
                .Where(w => w.ProjectId == null && !w.IsArchived)
                .OrderBy(w => w.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unassigned work orders");
            throw;
        }
    }
}