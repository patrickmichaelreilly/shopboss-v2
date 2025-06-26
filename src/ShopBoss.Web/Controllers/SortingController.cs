using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Models;
using ShopBoss.Web.Services;

namespace ShopBoss.Web.Controllers;

public class SortingController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SortingController> _logger;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly AuditTrailService _auditTrail;
    private readonly SortingRuleService _sortingRules;

    public SortingController(ShopBossDbContext context, ILogger<SortingController> logger, 
        IHubContext<StatusHub> hubContext, AuditTrailService auditTrail, SortingRuleService sortingRules)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
        _auditTrail = auditTrail;
        _sortingRules = sortingRules;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Get active work order from session
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                ViewBag.ErrorMessage = "No active work order selected. Please set an active work order from the Admin station.";
                return View(new List<StorageRack>());
            }

            // Get active work order details for display
            var activeWorkOrder = await _context.WorkOrders.FindAsync(activeWorkOrderId);
            ViewBag.ActiveWorkOrderId = activeWorkOrderId;
            ViewBag.ActiveWorkOrderName = activeWorkOrder?.Name ?? "Unknown";

            // Get all active storage racks with their bins
            var racks = await _sortingRules.GetActiveRacksAsync();

            // Get cut parts that need sorting
            var cutParts = await _context.Parts
                .Include(p => p.Product)
                .Include(p => p.NestSheet)
                .Where(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && p.Status == PartStatus.Cut)
                .OrderBy(p => p.Product!.Name)
                .ThenBy(p => p.Name)
                .ToListAsync();

            ViewBag.CutPartsCount = cutParts.Count;
            ViewBag.CutParts = cutParts;

            return View(racks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sorting station data");
            TempData["ErrorMessage"] = "An error occurred while loading the sorting station.";
            return View(new List<StorageRack>());
        }
    }

    public async Task<IActionResult> GetRackDetails(string id)
    {
        try
        {
            var rack = await _sortingRules.GetRackWithBinsAsync(id);
            if (rack == null)
            {
                return Json(new { success = false, message = "Rack not found." });
            }

            // Create a 2D grid representation of the rack
            var grid = new object[rack.Rows, rack.Columns];
            for (int row = 1; row <= rack.Rows; row++)
            {
                for (int col = 1; col <= rack.Columns; col++)
                {
                    var bin = rack.Bins.FirstOrDefault(b => b.Row == row && b.Column == col);
                    if (bin == null)
                    {
                        // Create empty bin representation
                        grid[row - 1, col - 1] = new
                        {
                            row = row,
                            column = col,
                            label = $"{(char)('A' + row - 1)}{col:D2}",
                            status = "empty",
                            statusText = "Empty",
                            contents = "",
                            partsCount = 0,
                            maxCapacity = 50,
                            capacityPercentage = 0,
                            productName = "",
                            partName = "",
                            isAvailable = true
                        };
                    }
                    else
                    {
                        grid[row - 1, col - 1] = new
                        {
                            id = bin.Id,
                            row = bin.Row,
                            column = bin.Column,
                            label = bin.BinLabel,
                            status = bin.Status.ToString().ToLower(),
                            statusText = bin.Status.ToString(),
                            contents = bin.Contents,
                            partsCount = bin.PartsCount,
                            maxCapacity = bin.MaxCapacity,
                            capacityPercentage = Math.Round(bin.CapacityPercentage, 1),
                            productName = bin.Product?.Name ?? "",
                            partName = bin.Part?.Name ?? "",
                            isAvailable = bin.IsAvailable,
                            assignedDate = bin.AssignedDate?.ToString("yyyy-MM-dd HH:mm"),
                            notes = bin.Notes
                        };
                    }
                }
            }

            var rackData = new
            {
                success = true,
                rack = new
                {
                    id = rack.Id,
                    name = rack.Name,
                    type = rack.Type.ToString(),
                    description = rack.Description,
                    rows = rack.Rows,
                    columns = rack.Columns,
                    location = rack.Location,
                    isPortable = rack.IsPortable,
                    totalBins = rack.TotalBins,
                    occupiedBins = rack.OccupiedBins,
                    availableBins = rack.AvailableBins,
                    occupancyPercentage = Math.Round(rack.OccupancyPercentage, 1),
                    bins = grid
                }
            };

            return Json(rackData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rack details for {RackId}", id);
            return Json(new { success = false, message = "An error occurred while retrieving rack details." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ScanPart(string barcode)
    {
        var sessionId = HttpContext.Session.Id;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var station = "Sorting";

        try
        {
            // Get active work order from session
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                await _auditTrail.LogScanAsync(barcode ?? "[empty]", station, false, 
                    "No active work order selected", sessionId: sessionId, ipAddress: ipAddress);
                return Json(new { success = false, message = "No active work order selected.", type = "session" });
            }

            if (string.IsNullOrWhiteSpace(barcode))
            {
                await _auditTrail.LogScanAsync("[empty]", station, false, 
                    "Empty barcode scanned", sessionId: sessionId, ipAddress: ipAddress);
                return Json(new { success = false, message = "Please scan a valid part barcode.", type = "validation" });
            }

            var cleanBarcode = barcode.Trim();

            // Find the cut part by barcode (assuming part name or ID as barcode)
            var part = await _context.Parts
                .Include(p => p.Product)
                .Include(p => p.NestSheet)
                .FirstOrDefaultAsync(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && 
                                         (p.Id == cleanBarcode || p.Name == cleanBarcode) &&
                                         p.Status == PartStatus.Cut);

            if (part == null)
            {
                await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                    "Cut part not found", workOrderId: activeWorkOrderId, 
                    sessionId: sessionId, ipAddress: ipAddress);
                return Json(new { 
                    success = false, 
                    message = $"Cut part with barcode '{cleanBarcode}' not found in active work order.", 
                    type = "not_found"
                });
            }

            if (part.Status != PartStatus.Cut)
            {
                await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                    $"Part not in Cut status (current: {part.Status})", 
                    workOrderId: activeWorkOrderId, sessionId: sessionId, ipAddress: ipAddress);
                return Json(new { 
                    success = false, 
                    message = $"Part '{part.Name}' is not ready for sorting (Status: {part.Status}).", 
                    type = "invalid_status"
                });
            }

            // Find optimal bin placement
            var (rackId, row, column, placementMessage) = await _sortingRules.FindOptimalBinForPartAsync(part.Id, activeWorkOrderId);

            if (rackId == null || row == null || column == null)
            {
                await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                    placementMessage, workOrderId: activeWorkOrderId, 
                    sessionId: sessionId, ipAddress: ipAddress);
                return Json(new { 
                    success = false, 
                    message = placementMessage, 
                    type = "no_placement"
                });
            }

            // Assign part to bin
            var assignmentSuccess = await _sortingRules.AssignPartToBinAsync(part.Id, rackId, row.Value, column.Value, activeWorkOrderId);

            if (!assignmentSuccess)
            {
                await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                    "Failed to assign part to bin", workOrderId: activeWorkOrderId, 
                    sessionId: sessionId, ipAddress: ipAddress);
                return Json(new { 
                    success = false, 
                    message = "Failed to assign part to storage bin. Please try again.", 
                    type = "assignment_failed"
                });
            }

            // Log successful scan and sorting
            await _auditTrail.LogScanAsync(cleanBarcode, station, true, 
                workOrderId: activeWorkOrderId, partsProcessed: 1, 
                sessionId: sessionId, ipAddress: ipAddress,
                details: $"Part sorted to rack {rackId} bin {(char)('A' + row.Value - 1)}{column.Value:D2}");

            // Log audit trail for part status change
            await _auditTrail.LogAsync("StatusChange", "Part", part.Id, 
                new { Status = "Cut", part.StatusUpdatedDate },
                new { Status = "Sorted", StatusUpdatedDate = DateTime.UtcNow },
                station: station, workOrderId: activeWorkOrderId,
                details: $"Part sorted via barcode scan '{cleanBarcode}' to {placementMessage}",
                sessionId: sessionId, ipAddress: ipAddress);

            // Send real-time updates via SignalR
            var updateData = new
            {
                partId = part.Id,
                partName = part.Name,
                productName = part.Product?.Name,
                rackId = rackId,
                binLabel = $"{(char)('A' + row.Value - 1)}{column.Value:D2}",
                station = station,
                barcode = cleanBarcode
            };

            await _hubContext.Clients.Groups($"workorder-{activeWorkOrderId}")
                .SendAsync("PartSorted", updateData);

            await _hubContext.Clients.Group("sorting-station")
                .SendAsync("StatusUpdate", new
                {
                    type = "part-sorted",
                    partId = part.Id,
                    partName = part.Name,
                    rackId = rackId,
                    binLabel = updateData.binLabel,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });

            _logger.LogInformation("Successfully sorted part {PartId} ({PartName}) to rack {RackId} bin {BinLabel} via barcode {Barcode}", 
                part.Id, part.Name, rackId, updateData.binLabel, cleanBarcode);

            return Json(new { 
                success = true, 
                message = $"✅ Part '{part.Name}' sorted successfully!\n{placementMessage}",
                partId = part.Id,
                partName = part.Name,
                productName = part.Product?.Name,
                rackId = rackId,
                binLabel = updateData.binLabel,
                placementMessage = placementMessage,
                type = "success"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing part scan with barcode {Barcode}", barcode);
            
            await _auditTrail.LogScanAsync(barcode ?? "[error]", station, false, 
                $"System error: {ex.Message}", sessionId: sessionId, ipAddress: ipAddress);
                
            return Json(new { 
                success = false, 
                message = "An unexpected error occurred while sorting the part. Please try again or contact support.",
                type = "system_error"
            });
        }
    }

    public async Task<IActionResult> GetCutParts()
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected." });
            }

            var cutParts = await _context.Parts
                .Include(p => p.Product)
                .Include(p => p.NestSheet)
                .Where(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && p.Status == PartStatus.Cut)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    qty = p.Qty,
                    productName = p.Product!.Name,
                    productId = p.ProductId,
                    material = p.Material,
                    length = p.Length,
                    width = p.Width,
                    thickness = p.Thickness,
                    nestSheetName = p.NestSheet!.Name
                })
                .OrderBy(p => p.productName)
                .ThenBy(p => p.name)
                .ToListAsync();

            return Json(new { success = true, parts = cutParts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cut parts for sorting");
            return Json(new { success = false, message = "Failed to load cut parts." });
        }
    }
}