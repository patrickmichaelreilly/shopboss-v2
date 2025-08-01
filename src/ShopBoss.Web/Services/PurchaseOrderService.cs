using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class PurchaseOrderService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<PurchaseOrderService> _logger;

    public PurchaseOrderService(ShopBossDbContext context, ILogger<PurchaseOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<PurchaseOrder>> GetPurchaseOrdersByProjectIdAsync(string projectId)
    {
        try
        {
            return await _context.PurchaseOrders
                .Where(po => po.ProjectId == projectId)
                .OrderByDescending(po => po.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<PurchaseOrder?> GetPurchaseOrderByIdAsync(string id)
    {
        try
        {
            return await _context.PurchaseOrders
                .FirstOrDefaultAsync(po => po.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase order {PurchaseOrderId}", id);
            throw;
        }
    }

    public async Task<PurchaseOrder> CreatePurchaseOrderAsync(PurchaseOrder purchaseOrder)
    {
        try
        {
            purchaseOrder.Id = Guid.NewGuid().ToString();
            purchaseOrder.CreatedDate = DateTime.UtcNow;

            _context.PurchaseOrders.Add(purchaseOrder);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created purchase order {PurchaseOrderId} for project {ProjectId}", 
                purchaseOrder.Id, purchaseOrder.ProjectId);

            return purchaseOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order for project {ProjectId}", purchaseOrder.ProjectId);
            throw;
        }
    }

    public async Task<PurchaseOrder?> UpdatePurchaseOrderAsync(PurchaseOrder purchaseOrder)
    {
        try
        {
            var existingPurchaseOrder = await _context.PurchaseOrders
                .FirstOrDefaultAsync(po => po.Id == purchaseOrder.Id);

            if (existingPurchaseOrder == null)
            {
                _logger.LogWarning("Purchase order {PurchaseOrderId} not found for update", purchaseOrder.Id);
                return null;
            }

            // Update properties
            existingPurchaseOrder.PurchaseOrderNumber = purchaseOrder.PurchaseOrderNumber;
            existingPurchaseOrder.VendorName = purchaseOrder.VendorName;
            existingPurchaseOrder.VendorContact = purchaseOrder.VendorContact;
            existingPurchaseOrder.VendorPhone = purchaseOrder.VendorPhone;
            existingPurchaseOrder.VendorEmail = purchaseOrder.VendorEmail;
            existingPurchaseOrder.Description = purchaseOrder.Description;
            existingPurchaseOrder.OrderDate = purchaseOrder.OrderDate;
            existingPurchaseOrder.ExpectedDeliveryDate = purchaseOrder.ExpectedDeliveryDate;
            existingPurchaseOrder.ActualDeliveryDate = purchaseOrder.ActualDeliveryDate;
            existingPurchaseOrder.TotalAmount = purchaseOrder.TotalAmount;
            existingPurchaseOrder.Status = purchaseOrder.Status;
            existingPurchaseOrder.Notes = purchaseOrder.Notes;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated purchase order {PurchaseOrderId}", purchaseOrder.Id);

            return existingPurchaseOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating purchase order {PurchaseOrderId}", purchaseOrder.Id);
            throw;
        }
    }

    public async Task<bool> DeletePurchaseOrderAsync(string id)
    {
        try
        {
            var purchaseOrder = await _context.PurchaseOrders
                .FirstOrDefaultAsync(po => po.Id == id);

            if (purchaseOrder == null)
            {
                _logger.LogWarning("Purchase order {PurchaseOrderId} not found for deletion", id);
                return false;
            }

            _context.PurchaseOrders.Remove(purchaseOrder);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted purchase order {PurchaseOrderId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting purchase order {PurchaseOrderId}", id);
            throw;
        }
    }
}