using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using System.Text.Json;

namespace ShopBoss.Web.Services;

public class AuditTrailService
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<AuditTrailService> _logger;

    public AuditTrailService(ShopBossDbContext context, ILogger<AuditTrailService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityType, string entityId, 
        object? oldValue = null, object? newValue = null, string? userId = null, 
        string station = "", string? workOrderId = null, string details = "", 
        string? sessionId = null, string? ipAddress = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue != null ? JsonSerializer.Serialize(oldValue) : null,
                NewValue = newValue != null ? JsonSerializer.Serialize(newValue) : null,
                UserId = userId,
                Station = station,
                WorkOrderId = workOrderId,
                Details = details,
                SessionId = sessionId,
                IPAddress = ipAddress
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Audit log created: {Action} on {EntityType} {EntityId} from {Station}", 
                action, entityType, entityId, station);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for {Action} on {EntityType} {EntityId}", 
                action, entityType, entityId);
        }
    }

    public async Task LogScanAsync(string barcode, string station, bool isSuccessful, 
        string? errorMessage = null, string? nestSheetId = null, string? workOrderId = null, 
        int? partsProcessed = null, string? sessionId = null, string? ipAddress = null, 
        string details = "")
    {
        try
        {
            var scanHistory = new ScanHistory
            {
                Barcode = barcode,
                Station = station,
                IsSuccessful = isSuccessful,
                ErrorMessage = errorMessage,
                NestSheetId = nestSheetId,
                WorkOrderId = workOrderId,
                PartsProcessed = partsProcessed,
                SessionId = sessionId,
                IPAddress = ipAddress,
                Details = details
            };

            _context.ScanHistory.Add(scanHistory);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Scan history logged: {Barcode} at {Station} - {Result}", 
                barcode, station, isSuccessful ? "Success" : "Failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log scan history for barcode {Barcode} at {Station}", 
                barcode, station);
        }
    }

    public async Task<List<ScanHistory>> GetRecentScansAsync(string station, int count = 10)
    {
        try
        {
            return await _context.ScanHistory
                .Include(s => s.NestSheet)
                .Include(s => s.WorkOrder)
                .Where(s => s.Station == station)
                .OrderByDescending(s => s.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent scans for station {Station}", station);
            return new List<ScanHistory>();
        }
    }

    public async Task<List<AuditLog>> GetEntityAuditTrailAsync(string entityType, string entityId)
    {
        try
        {
            return await _context.AuditLogs
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve audit trail for {EntityType} {EntityId}", 
                entityType, entityId);
            return new List<AuditLog>();
        }
    }

    public async Task<bool> HasRecentDuplicateScanAsync(string barcode, string station, TimeSpan timeWindow)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
            return await _context.ScanHistory
                .AnyAsync(s => s.Barcode == barcode && 
                              s.Station == station && 
                              s.IsSuccessful && 
                              s.Timestamp >= cutoffTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for duplicate scan of {Barcode} at {Station}", 
                barcode, station);
            return false;
        }
    }
}