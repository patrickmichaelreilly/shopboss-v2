using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class CustomWorkOrderService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<CustomWorkOrderService> _logger;

    public CustomWorkOrderService(ShopBossDbContext context, ILogger<CustomWorkOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<CustomWorkOrder>> GetCustomWorkOrdersByProjectIdAsync(string projectId)
    {
        try
        {
            return await _context.CustomWorkOrders
                .Where(cwo => cwo.ProjectId == projectId)
                .OrderByDescending(cwo => cwo.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving custom work orders for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<CustomWorkOrder?> GetCustomWorkOrderByIdAsync(string id)
    {
        try
        {
            return await _context.CustomWorkOrders
                .FirstOrDefaultAsync(cwo => cwo.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving custom work order {CustomWorkOrderId}", id);
            throw;
        }
    }

    public async Task<CustomWorkOrder> CreateCustomWorkOrderAsync(CustomWorkOrder customWorkOrder, string? taskBlockId = null)
    {
        try
        {
            customWorkOrder.Id = Guid.NewGuid().ToString();
            customWorkOrder.CreatedDate = DateTime.UtcNow;

            _context.CustomWorkOrders.Add(customWorkOrder);
            
            // Create timeline event for custom work order creation
            var customWorkOrderEvent = new ProjectEvent
            {
                ProjectId = customWorkOrder.ProjectId,
                EventDate = customWorkOrder.CreatedDate,
                EventType = "custom_work_order",
                Description = customWorkOrder.Name,
                CreatedBy = null, // Could be passed as parameter if needed
                CustomWorkOrderId = customWorkOrder.Id,
                TaskBlockId = taskBlockId
            };
            _context.ProjectEvents.Add(customWorkOrderEvent);
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created custom work order {CustomWorkOrderId} for project {ProjectId}", 
                customWorkOrder.Id, customWorkOrder.ProjectId);

            return customWorkOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating custom work order for project {ProjectId}", customWorkOrder.ProjectId);
            throw;
        }
    }

    public async Task<CustomWorkOrder?> UpdateCustomWorkOrderAsync(CustomWorkOrder customWorkOrder)
    {
        try
        {
            var existingCustomWorkOrder = await _context.CustomWorkOrders
                .FirstOrDefaultAsync(cwo => cwo.Id == customWorkOrder.Id);

            if (existingCustomWorkOrder == null)
            {
                _logger.LogWarning("Custom work order {CustomWorkOrderId} not found for update", customWorkOrder.Id);
                return null;
            }

            // Update properties
            existingCustomWorkOrder.Name = customWorkOrder.Name;
            existingCustomWorkOrder.WorkOrderType = customWorkOrder.WorkOrderType;
            existingCustomWorkOrder.Description = customWorkOrder.Description;
            existingCustomWorkOrder.AssignedTo = customWorkOrder.AssignedTo;
            existingCustomWorkOrder.EstimatedHours = customWorkOrder.EstimatedHours;
            existingCustomWorkOrder.ActualHours = customWorkOrder.ActualHours;
            existingCustomWorkOrder.Status = customWorkOrder.Status;
            existingCustomWorkOrder.StartDate = customWorkOrder.StartDate;
            existingCustomWorkOrder.CompletedDate = customWorkOrder.CompletedDate;
            existingCustomWorkOrder.Notes = customWorkOrder.Notes;

            // Update the associated timeline event to reflect the current name
            var timelineEvent = await _context.ProjectEvents
                .FirstOrDefaultAsync(pe => pe.CustomWorkOrderId == customWorkOrder.Id);
            if (timelineEvent != null)
            {
                timelineEvent.Description = customWorkOrder.Name;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated custom work order {CustomWorkOrderId}", customWorkOrder.Id);

            return existingCustomWorkOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating custom work order {CustomWorkOrderId}", customWorkOrder.Id);
            throw;
        }
    }

    public async Task<bool> DeleteCustomWorkOrderAsync(string id)
    {
        try
        {
            var customWorkOrder = await _context.CustomWorkOrders
                .FirstOrDefaultAsync(cwo => cwo.Id == id);

            if (customWorkOrder == null)
            {
                _logger.LogWarning("Custom work order {CustomWorkOrderId} not found for deletion", id);
                return false;
            }

            // Remove the associated timeline event (entity display pattern)
            var timelineEvent = await _context.ProjectEvents
                .FirstOrDefaultAsync(pe => pe.CustomWorkOrderId == id);
            if (timelineEvent != null)
            {
                _context.ProjectEvents.Remove(timelineEvent);
            }
            
            _context.CustomWorkOrders.Remove(customWorkOrder);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted custom work order {CustomWorkOrderId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting custom work order {CustomWorkOrderId}", id);
            throw;
        }
    }
}