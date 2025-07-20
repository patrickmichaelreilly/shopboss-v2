using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public class WorkOrderDeletionService
{
    private readonly ShopBossDbContext _context;
    private readonly AuditTrailService _auditTrailService;
    private readonly ILogger<WorkOrderDeletionService> _logger;

    public WorkOrderDeletionService(
        ShopBossDbContext context, 
        AuditTrailService auditTrailService, 
        ILogger<WorkOrderDeletionService> logger)
    {
        _context = context;
        _auditTrailService = auditTrailService;
        _logger = logger;
    }

    /// <summary>
    /// Delete a part from the database with audit logging
    /// </summary>
    public async Task<DeletionResult> DeletePartAsync(string partId, string workOrderId, string station = "Manual", string? parentContext = null)
    {
        try
        {
            var part = await _context.Parts.FindAsync(partId);
            if (part == null)
            {
                return new DeletionResult 
                { 
                    Success = false, 
                    Message = $"Part '{partId}' not found." 
                };
            }

            // Store old values for audit trail
            var oldValue = new 
            { 
                Id = part.Id,
                Name = part.Name,
                Status = part.Status.ToString(),
                Category = part.Category.ToString(),
                ProductId = part.ProductId,
                SubassemblyId = part.SubassemblyId,
                NestSheetId = part.NestSheetId
            };

            // Remove the part
            _context.Parts.Remove(part);
            await _context.SaveChangesAsync();

            // Log the deletion
            var details = string.IsNullOrEmpty(parentContext) 
                ? $"Part '{part.Name}' deleted from work order"
                : $"Part '{part.Name}' deleted from work order (via {parentContext} deletion)";
                
            await _auditTrailService.LogAsync(
                action: "DeletePart",
                entityType: "Part",
                entityId: partId,
                oldValue: oldValue,
                newValue: null,
                station: station,
                workOrderId: workOrderId,
                details: details,
                sessionId: null
            );

            _logger.LogInformation("Part {PartId} deleted from work order {WorkOrderId}", partId, workOrderId);

            return new DeletionResult 
            { 
                Success = true, 
                Message = $"Part '{part.Name}' deleted successfully.",
                ItemsDeleted = 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting part {PartId}", partId);
            return new DeletionResult 
            { 
                Success = false, 
                Message = "Error deleting part: " + ex.Message 
            };
        }
    }

    /// <summary>
    /// Delete a hardware item from the database with audit logging
    /// </summary>
    public async Task<DeletionResult> DeleteHardwareAsync(string hardwareId, string workOrderId, string station = "Manual", string? parentContext = null)
    {
        try
        {
            var hardware = await _context.Hardware.FindAsync(hardwareId);
            if (hardware == null)
            {
                return new DeletionResult 
                { 
                    Success = false, 
                    Message = $"Hardware '{hardwareId}' not found." 
                };
            }

            // Store old values for audit trail
            var oldValue = new 
            { 
                Id = hardware.Id,
                Name = hardware.Name,
                Status = hardware.Status.ToString(),
                WorkOrderId = hardware.WorkOrderId
            };

            // Remove the hardware
            _context.Hardware.Remove(hardware);
            await _context.SaveChangesAsync();

            // Log the deletion
            var details = string.IsNullOrEmpty(parentContext) 
                ? $"Hardware '{hardware.Name}' deleted from work order"
                : $"Hardware '{hardware.Name}' deleted from work order (via {parentContext} deletion)";
                
            await _auditTrailService.LogAsync(
                action: "DeleteHardware",
                entityType: "Hardware",
                entityId: hardwareId,
                oldValue: oldValue,
                newValue: null,
                station: station,
                workOrderId: workOrderId,
                details: details,
                sessionId: null
            );

            _logger.LogInformation("Hardware {HardwareId} deleted from work order {WorkOrderId}", hardwareId, workOrderId);

            return new DeletionResult 
            { 
                Success = true, 
                Message = $"Hardware '{hardware.Name}' deleted successfully.",
                ItemsDeleted = 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hardware {HardwareId}", hardwareId);
            return new DeletionResult 
            { 
                Success = false, 
                Message = "Error deleting hardware: " + ex.Message 
            };
        }
    }

    /// <summary>
    /// Delete a subassembly and all its parts with cascade deletion and audit logging
    /// </summary>
    public async Task<DeletionResult> DeleteSubassemblyAsync(string subassemblyId, string workOrderId, string station = "Manual", string? parentContext = null)
    {
        try
        {
            var subassembly = await _context.Subassemblies
                .Include(s => s.Parts)
                .FirstOrDefaultAsync(s => s.Id == subassemblyId);

            if (subassembly == null)
            {
                return new DeletionResult 
                { 
                    Success = false, 
                    Message = $"Subassembly '{subassemblyId}' not found." 
                };
            }

            var deletedItems = 0;

            // Delete all parts in the subassembly first
            foreach (var part in subassembly.Parts.ToList())
            {
                var partResult = await DeletePartAsync(part.Id, workOrderId, station, $"Subassembly '{subassembly.Name}'");
                if (partResult.Success)
                {
                    deletedItems += partResult.ItemsDeleted;
                }
            }

            // Store old values for audit trail
            var oldValue = new 
            { 
                Id = subassembly.Id,
                Name = subassembly.Name,
                PartsCount = subassembly.Parts.Count,
                ProductId = subassembly.ProductId,
                ParentSubassemblyId = subassembly.ParentSubassemblyId
            };

            // Remove the subassembly
            _context.Subassemblies.Remove(subassembly);
            await _context.SaveChangesAsync();

            // Log the subassembly deletion
            var details = string.IsNullOrEmpty(parentContext) 
                ? $"Subassembly '{subassembly.Name}' and {oldValue.PartsCount} parts deleted from work order"
                : $"Subassembly '{subassembly.Name}' and {oldValue.PartsCount} parts deleted from work order (via {parentContext} deletion)";
                
            await _auditTrailService.LogAsync(
                action: "DeleteSubassembly",
                entityType: "Subassembly",
                entityId: subassemblyId,
                oldValue: oldValue,
                newValue: null,
                station: station,
                workOrderId: workOrderId,
                details: details,
                sessionId: null
            );

            _logger.LogInformation("Subassembly {SubassemblyId} and {PartsCount} parts deleted from work order {WorkOrderId}", 
                subassemblyId, oldValue.PartsCount, workOrderId);

            deletedItems++; // Count the subassembly itself

            return new DeletionResult 
            { 
                Success = true, 
                Message = $"Subassembly '{subassembly.Name}' and all children deleted successfully.",
                ItemsDeleted = deletedItems
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subassembly {SubassemblyId}", subassemblyId);
            return new DeletionResult 
            { 
                Success = false, 
                Message = "Error deleting subassembly: " + ex.Message 
            };
        }
    }

    /// <summary>
    /// Delete a product and all its children (parts, subassemblies, hardware) with cascade deletion and audit logging
    /// </summary>
    public async Task<DeletionResult> DeleteProductAsync(string productId, string workOrderId, string station = "Manual")
    {
        try
        {
            var product = await _context.Products
                .Include(p => p.Parts)
                .Include(p => p.Subassemblies)
                    .ThenInclude(s => s.Parts)
                .Include(p => p.Hardware)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return new DeletionResult 
                { 
                    Success = false, 
                    Message = $"Product '{productId}' not found." 
                };
            }

            var deletedItems = 0;

            // Delete all subassemblies (which will cascade to their parts)
            foreach (var subassembly in product.Subassemblies.ToList())
            {
                var subassemblyResult = await DeleteSubassemblyAsync(subassembly.Id, workOrderId, station, $"Product '{product.Name}'");
                if (subassemblyResult.Success)
                {
                    deletedItems += subassemblyResult.ItemsDeleted;
                }
            }

            // Delete all direct parts
            foreach (var part in product.Parts.ToList())
            {
                var partResult = await DeletePartAsync(part.Id, workOrderId, station, $"Product '{product.Name}'");
                if (partResult.Success)
                {
                    deletedItems += partResult.ItemsDeleted;
                }
            }

            // Delete all hardware
            foreach (var hardware in product.Hardware.ToList())
            {
                var hardwareResult = await DeleteHardwareAsync(hardware.Id, workOrderId, station, $"Product '{product.Name}'");
                if (hardwareResult.Success)
                {
                    deletedItems += hardwareResult.ItemsDeleted;
                }
            }

            // Store old values for audit trail
            var oldValue = new 
            { 
                Id = product.Id,
                Name = product.Name,
                Status = product.Status.ToString(),
                PartsCount = product.Parts.Count,
                SubassembliesCount = product.Subassemblies.Count,
                HardwareCount = product.Hardware.Count,
                WorkOrderId = product.WorkOrderId
            };

            // Remove the product
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            // Log the product deletion
            await _auditTrailService.LogAsync(
                action: "DeleteProduct",
                entityType: "Product",
                entityId: productId,
                oldValue: oldValue,
                newValue: null,
                station: station,
                workOrderId: workOrderId,
                details: $"Product '{product.Name}' and all children deleted from work order. " +
                        $"Parts: {oldValue.PartsCount}, Subassemblies: {oldValue.SubassembliesCount}, Hardware: {oldValue.HardwareCount}",
                sessionId: null
            );

            _logger.LogInformation("Product {ProductId} and all children deleted from work order {WorkOrderId}. " +
                                 "Total items deleted: {TotalDeleted}", 
                productId, workOrderId, deletedItems + 1);

            deletedItems++; // Count the product itself

            return new DeletionResult 
            { 
                Success = true, 
                Message = $"Product '{product.Name}' and all children deleted successfully.",
                ItemsDeleted = deletedItems
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId}", productId);
            return new DeletionResult 
            { 
                Success = false, 
                Message = "Error deleting product: " + ex.Message 
            };
        }
    }

    /// <summary>
    /// Delete a detached product with audit logging
    /// </summary>
    public async Task<DeletionResult> DeleteDetachedProductAsync(string detachedProductId, string workOrderId, string station = "Manual")
    {
        try
        {
            var detachedProduct = await _context.DetachedProducts.FindAsync(detachedProductId);
            if (detachedProduct == null)
            {
                return new DeletionResult 
                { 
                    Success = false, 
                    Message = $"Detached product '{detachedProductId}' not found." 
                };
            }

            // Store old values for audit trail
            var oldValue = new 
            { 
                Id = detachedProduct.Id,
                Name = detachedProduct.Name,
                Status = detachedProduct.Status.ToString(),
                WorkOrderId = detachedProduct.WorkOrderId
            };

            // Remove the detached product
            _context.DetachedProducts.Remove(detachedProduct);
            await _context.SaveChangesAsync();

            // Log the deletion
            await _auditTrailService.LogAsync(
                action: "DeleteDetachedProduct",
                entityType: "DetachedProduct",
                entityId: detachedProductId,
                oldValue: oldValue,
                newValue: null,
                station: station,
                workOrderId: workOrderId,
                details: $"Detached product '{detachedProduct.Name}' deleted from work order",
                sessionId: null
            );

            _logger.LogInformation("Detached product {DetachedProductId} deleted from work order {WorkOrderId}", detachedProductId, workOrderId);

            return new DeletionResult 
            { 
                Success = true, 
                Message = $"Detached product '{detachedProduct.Name}' deleted successfully.",
                ItemsDeleted = 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting detached product {DetachedProductId}", detachedProductId);
            return new DeletionResult 
            { 
                Success = false, 
                Message = "Error deleting detached product: " + ex.Message 
            };
        }
    }

    /// <summary>
    /// Delete a nest sheet and all its parts with cascade deletion and audit logging
    /// </summary>
    public async Task<DeletionResult> DeleteNestSheetAsync(string nestSheetId, string workOrderId, string station = "Manual")
    {
        try
        {
            var nestSheet = await _context.NestSheets
                .Include(n => n.Parts)
                .FirstOrDefaultAsync(n => n.Id == nestSheetId);

            if (nestSheet == null)
            {
                return new DeletionResult 
                { 
                    Success = false, 
                    Message = $"Nest sheet '{nestSheetId}' not found." 
                };
            }

            var deletedItems = 0;

            // Delete all parts in the nest sheet first
            foreach (var part in nestSheet.Parts.ToList())
            {
                var partResult = await DeletePartAsync(part.Id, workOrderId, station, $"NestSheet '{nestSheet.Name}'");
                if (partResult.Success)
                {
                    deletedItems += partResult.ItemsDeleted;
                }
            }

            // Store old values for audit trail
            var oldValue = new 
            { 
                Id = nestSheet.Id,
                Name = nestSheet.Name,
                Status = nestSheet.Status.ToString(),
                PartsCount = nestSheet.Parts.Count,
                WorkOrderId = nestSheet.WorkOrderId
            };

            // Remove the nest sheet
            _context.NestSheets.Remove(nestSheet);
            await _context.SaveChangesAsync();

            // Log the nest sheet deletion
            await _auditTrailService.LogAsync(
                action: "DeleteNestSheet",
                entityType: "NestSheet",
                entityId: nestSheetId,
                oldValue: oldValue,
                newValue: null,
                station: station,
                workOrderId: workOrderId,
                details: $"Nest sheet '{nestSheet.Name}' and {oldValue.PartsCount} parts deleted from work order",
                sessionId: null
            );

            _logger.LogInformation("Nest sheet {NestSheetId} and {PartsCount} parts deleted from work order {WorkOrderId}", 
                nestSheetId, oldValue.PartsCount, workOrderId);

            deletedItems++; // Count the nest sheet itself

            return new DeletionResult 
            { 
                Success = true, 
                Message = $"Nest sheet '{nestSheet.Name}' and all children deleted successfully.",
                ItemsDeleted = deletedItems
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting nest sheet {NestSheetId}", nestSheetId);
            return new DeletionResult 
            { 
                Success = false, 
                Message = "Error deleting nest sheet: " + ex.Message 
            };
        }
    }
}

/// <summary>
/// Result object for deletion operations
/// </summary>
public class DeletionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ItemsDeleted { get; set; } = 0;
}