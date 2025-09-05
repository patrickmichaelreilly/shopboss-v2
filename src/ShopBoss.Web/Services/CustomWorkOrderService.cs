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

    public async Task<CustomWorkOrder> CreateCustomWorkOrderAsync(CustomWorkOrder customWorkOrder, string? taskBlockId = null, string? createdBy = null)
    {
        try
        {
            customWorkOrder.Id = Guid.NewGuid().ToString();
            customWorkOrder.CreatedDate = DateTime.UtcNow;

            _context.CustomWorkOrders.Add(customWorkOrder);
            
            // Get the next display order for the target container
            var maxOrder = await _context.ProjectEvents
                .Where(pe => pe.ProjectId == customWorkOrder.ProjectId && pe.ParentBlockId == taskBlockId)
                .MaxAsync(pe => (int?)pe.DisplayOrder) ?? 0;

            // Create timeline event for custom work order creation
            var customWorkOrderEvent = new ProjectEvent
            {
                ProjectId = customWorkOrder.ProjectId,
                EventDate = customWorkOrder.CreatedDate,
                EventType = "custom_work_order",
                Description = customWorkOrder.Name,
                CreatedBy = createdBy,
                CustomWorkOrderId = customWorkOrder.Id,
                ParentBlockId = taskBlockId,
                DisplayOrder = maxOrder + 1
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

    public async Task<bool> UpdateCustomWorkOrderFieldAsync(string customWorkOrderId, string fieldName, string? value)
    {
        try
        {
            var customWorkOrder = await _context.CustomWorkOrders.FindAsync(customWorkOrderId);
            if (customWorkOrder == null)
            {
                _logger.LogWarning("CustomWorkOrder not found for field update: {CustomWorkOrderId}", customWorkOrderId);
                return false;
            }

            // Update the specific field based on fieldName
            switch (fieldName)
            {
                case "Name":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        _logger.LogWarning("CustomWorkOrder name cannot be empty: {CustomWorkOrderId}", customWorkOrderId);
                        return false;
                    }
                    customWorkOrder.Name = value;
                    break;
                case "Description":
                    customWorkOrder.Description = value ?? string.Empty;
                    break;
                case "AssignedTo":
                    customWorkOrder.AssignedTo = value;
                    break;
                default:
                    _logger.LogWarning("Unknown field name for CustomWorkOrder update: {FieldName}", fieldName);
                    return false;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated CustomWorkOrder field {FieldName} for CWO {CustomWorkOrderId}", fieldName, customWorkOrderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating CustomWorkOrder field {FieldName} for CWO {CustomWorkOrderId}", fieldName, customWorkOrderId);
            throw;
        }
    }
}