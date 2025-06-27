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

            // Create a list of rows, each containing a list of bins
            var grid = new List<List<object>>();
            for (int row = 1; row <= rack.Rows; row++)
            {
                var binRow = new List<object>();
                for (int col = 1; col <= rack.Columns; col++)
                {
                    var bin = rack.Bins.FirstOrDefault(b => b.Row == row && b.Column == col);
                    if (bin == null)
                    {
                        // Create empty bin representation
                        binRow.Add(new
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
                        });
                    }
                    else
                    {
                        binRow.Add(new
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
                        });
                    }
                }
                grid.Add(binRow);
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

    public async Task<IActionResult> GetRackOccupancy(string id)
    {
        try
        {
            var rack = await _sortingRules.GetRackWithBinsAsync(id);
            if (rack == null)
            {
                return Json(new { success = false });
            }
            
            return Json(new { 
                success = true, 
                occupiedBins = rack.OccupiedBins,
                totalBins = rack.TotalBins 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rack occupancy for {RackId}", id);
            return Json(new { success = false });
        }
    }

    public async Task<IActionResult> GetCurrentCutPartsCount()
    {
        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, count = 0 });
            }

            var cutPartsCount = await _context.Parts
                .Include(p => p.NestSheet)
                .CountAsync(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && p.Status == PartStatus.Cut);

            return Json(new { success = true, count = cutPartsCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current cut parts count");
            return Json(new { success = false, count = 0 });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ScanPart(string barcode, string? selectedRackId = null)
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

            // Find optimal bin placement - prefer selected rack if provided
            var (rackId, row, column, placementMessage) = await _sortingRules.FindOptimalBinForPartAsync(part.Id, activeWorkOrderId, selectedRackId);

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

            // Send rack occupancy update for real-time badge refresh
            var updatedRack = await _sortingRules.GetRackWithBinsAsync(rackId);
            if (updatedRack != null)
            {
                await _hubContext.Clients.Group("sorting-station")
                    .SendAsync("RackOccupancyUpdate", new
                    {
                        rackId = rackId,
                        occupiedBins = updatedRack.OccupiedBins,
                        totalBins = updatedRack.TotalBins,
                        timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    });
            }

            // Send cut parts count update
            var currentCutParts = await _context.Parts
                .Include(p => p.NestSheet)
                .CountAsync(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && p.Status == PartStatus.Cut);
                
            await _hubContext.Clients.Group("sorting-station")
                .SendAsync("CutPartsCountUpdate", new
                {
                    count = currentCutParts,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });

            // Check for assembly readiness after successful part sorting
            try
            {
                var readyProducts = await _sortingRules.CheckAssemblyReadinessAsync(activeWorkOrderId);
                
                if (readyProducts.Any())
                {
                    foreach (var productId in readyProducts)
                    {
                        // Get product details for notification
                        var readyProduct = await _context.Products.FindAsync(productId);
                        if (readyProduct != null)
                        {
                            // Mark product as ready for assembly
                            await _sortingRules.MarkProductReadyForAssemblyAsync(productId);
                            
                            // Send assembly readiness notification to all stations
                            await _hubContext.Clients.Groups($"workorder-{activeWorkOrderId}")
                                .SendAsync("ProductReadyForAssembly", new
                                {
                                    productId = readyProduct.Id,
                                    productName = readyProduct.Name,
                                    productNumber = readyProduct.ProductNumber,
                                    workOrderId = activeWorkOrderId,
                                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                                    sortingStation = station
                                });

                            // Send specific notification to assembly station
                            await _hubContext.Clients.Group("assembly-station")
                                .SendAsync("NewProductReady", new
                                {
                                    productId = readyProduct.Id,
                                    productName = readyProduct.Name,
                                    productNumber = readyProduct.ProductNumber,
                                    workOrderId = activeWorkOrderId,
                                    readyTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                                    message = $"Product '{readyProduct.Name}' is ready for assembly - all parts sorted!"
                                });

                            _logger.LogInformation("Product {ProductId} ({ProductName}) marked as ready for assembly after sorting part {PartId}", 
                                readyProduct.Id, readyProduct.Name, part.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking assembly readiness after sorting part {PartId}", part.Id);
                // Don't fail the sort operation if assembly checking fails
            }

            _logger.LogInformation("Successfully sorted part {PartId} ({PartName}) to rack {RackId} bin {BinLabel} via barcode {Barcode}", 
                part.Id, part.Name, rackId, updateData.binLabel, cleanBarcode);

            // Get remaining cut parts count
            var remainingCutParts = await _context.Parts
                .Include(p => p.NestSheet)
                .CountAsync(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && p.Status == PartStatus.Cut);
                
            return Json(new { 
                success = true, 
                message = $"✅ Part '{part.Name}' sorted successfully!\n{placementMessage}",
                partId = part.Id,
                partName = part.Name,
                productName = part.Product?.Name,
                rackId = rackId,
                binLabel = updateData.binLabel,
                placementMessage = placementMessage,
                type = "success",
                updatedRackOccupancy = new
                {
                    rackId = rackId,
                    occupiedBins = updatedRack?.OccupiedBins,
                    totalBins = updatedRack?.TotalBins
                },
                remainingCutParts = currentCutParts
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

    public async Task<IActionResult> GetBinDetails(string rackId, int row, int column)
    {
        try
        {
            var bin = await _context.Bins
                .Include(b => b.Part)
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.StorageRackId == rackId && b.Row == row && b.Column == column);

            if (bin == null)
            {
                return Json(new { success = false, message = "Bin not found." });
            }

            // Get parts that are currently assigned to this specific bin location
            var binLocation = $"{rackId}-{bin.BinLabel}";
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            var binParts = new List<object>();

            if (!string.IsNullOrEmpty(activeWorkOrderId))
            {
                var sortedParts = await _context.Parts
                    .Include(p => p.Product)
                    .Include(p => p.NestSheet)
                    .Where(p => p.Location == binLocation && 
                               p.Status == PartStatus.Sorted &&
                               p.NestSheet!.WorkOrderId == activeWorkOrderId)
                    .ToListAsync();

                binParts = sortedParts.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    qty = p.Qty,
                    productName = p.Product?.Name ?? "Unknown",
                    material = p.Material,
                    length = p.Length,
                    width = p.Width,
                    thickness = p.Thickness,
                    sortedDate = p.StatusUpdatedDate?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown"
                }).Cast<object>().ToList();
            }

            var binDetails = new
            {
                success = true,
                bin = new
                {
                    id = bin.Id,
                    rackId = bin.StorageRackId,
                    row = bin.Row,
                    column = bin.Column,
                    label = bin.BinLabel,
                    status = bin.Status.ToString().ToLower(),
                    statusText = bin.Status.ToString(),
                    partsCount = bin.PartsCount,
                    maxCapacity = bin.MaxCapacity,
                    capacityPercentage = Math.Round(bin.CapacityPercentage, 1),
                    productName = bin.Product?.Name,
                    workOrderId = bin.WorkOrderId,
                    assignedDate = bin.AssignedDate?.ToString("yyyy-MM-dd HH:mm"),
                    lastUpdatedDate = bin.LastUpdatedDate?.ToString("yyyy-MM-dd HH:mm"),
                    notes = bin.Notes,
                    contents = bin.Contents,
                    parts = binParts
                }
            };

            return Json(binDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bin details for rack {RackId} bin {Row},{Column}", rackId, row, column);
            return Json(new { success = false, message = "An error occurred while retrieving bin details." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RemovePartFromBin(string partId)
    {
        var sessionId = HttpContext.Session.Id;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var station = "Sorting";

        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected." });
            }

            var part = await _context.Parts
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == partId && p.Status == PartStatus.Sorted);

            if (part == null)
            {
                return Json(new { success = false, message = "Part not found or not currently sorted." });
            }

            // Find the bin this part is currently in
            var bin = await _context.Bins
                .FirstOrDefaultAsync(b => b.ProductId == part.ProductId && b.PartsCount > 0);

            if (bin != null)
            {
                // Update bin - reduce parts count and adjust status
                bin.PartsCount = Math.Max(0, bin.PartsCount - part.Qty);
                
                if (bin.PartsCount == 0)
                {
                    // Bin is now empty
                    bin.Status = BinStatus.Empty;
                    bin.PartId = null;
                    bin.ProductId = null;
                    bin.WorkOrderId = null;
                    bin.Contents = string.Empty;
                    bin.AssignedDate = null;
                }
                else
                {
                    // Update bin status based on remaining capacity
                    bin.Status = bin.PartsCount >= bin.MaxCapacity ? BinStatus.Full : BinStatus.Partial;
                    
                    // Update contents to remove this part
                    if (!string.IsNullOrEmpty(bin.Contents))
                    {
                        // This is a simplified content update - in a real system you'd track parts more precisely
                        bin.Contents = $"{part.Product?.Name}: {bin.PartsCount} parts";
                    }
                }
                
                bin.LastUpdatedDate = DateTime.UtcNow;
            }

            // Change part status back to Cut and clear location
            part.Status = PartStatus.Cut;
            part.StatusUpdatedDate = DateTime.UtcNow;
            part.Location = null;

            await _context.SaveChangesAsync();

            // Log audit trail
            await _auditTrail.LogAsync("RemoveFromBin", "Part", part.Id,
                new { Status = "Sorted", part.StatusUpdatedDate },
                new { Status = "Cut", StatusUpdatedDate = DateTime.UtcNow },
                station: station, workOrderId: activeWorkOrderId,
                details: $"Part '{part.Name}' removed from bin and status changed back to Cut",
                sessionId: sessionId, ipAddress: ipAddress);

            // Get updated cut parts count
            var updatedCutPartsCount = await _context.Parts
                .Include(p => p.NestSheet)
                .CountAsync(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && p.Status == PartStatus.Cut);

            _logger.LogInformation("Part {PartId} ({PartName}) removed from bin and status changed to Cut", 
                part.Id, part.Name);

            return Json(new { 
                success = true, 
                message = $"Part '{part.Name}' removed from bin successfully.",
                updatedCutPartsCount = updatedCutPartsCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing part {PartId} from bin", partId);
            return Json(new { success = false, message = "An error occurred while removing the part from the bin." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ClearBin(string rackId, int row, int column)
    {
        var sessionId = HttpContext.Session.Id;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var station = "Sorting";

        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected." });
            }

            var bin = await _context.Bins
                .FirstOrDefaultAsync(b => b.StorageRackId == rackId && b.Row == row && b.Column == column);

            if (bin == null)
            {
                return Json(new { success = false, message = "Bin not found." });
            }

            if (bin.PartsCount == 0)
            {
                return Json(new { success = false, message = "Bin is already empty." });
            }

            // Find all parts that are currently sorted to this specific bin location
            var binLocation = $"{rackId}-{bin.BinLabel}";
            var partsToRemove = await _context.Parts
                .Include(p => p.Product)
                .Include(p => p.NestSheet)
                .Where(p => p.Location == binLocation && 
                           p.Status == PartStatus.Sorted &&
                           p.NestSheet!.WorkOrderId == activeWorkOrderId)
                .ToListAsync();

            var partsRemoved = 0;
            
            // Change all parts back to Cut status and clear location
            foreach (var part in partsToRemove)
            {
                part.Status = PartStatus.Cut;
                part.StatusUpdatedDate = DateTime.UtcNow;
                part.Location = null;
                partsRemoved++;

                // Log individual part status change
                await _auditTrail.LogAsync("ClearBin", "Part", part.Id,
                    new { Status = "Sorted", part.StatusUpdatedDate, part.Location },
                    new { Status = "Cut", StatusUpdatedDate = DateTime.UtcNow, Location = (string?)null },
                    station: station, workOrderId: activeWorkOrderId,
                    details: $"Part '{part.Name}' status changed to Cut and location cleared due to bin clear operation",
                    sessionId: sessionId, ipAddress: ipAddress);
            }

            // Clear the bin
            var originalBin = new { 
                bin.Status, bin.PartsCount, bin.PartId, bin.ProductId, 
                bin.WorkOrderId, bin.Contents, bin.AssignedDate 
            };

            bin.Status = BinStatus.Empty;
            bin.PartsCount = 0;
            bin.PartId = null;
            bin.ProductId = null;
            bin.WorkOrderId = null;
            bin.Contents = string.Empty;
            bin.AssignedDate = null;
            bin.LastUpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Log bin clear operation
            await _auditTrail.LogAsync("ClearBin", "Bin", bin.Id,
                originalBin,
                new { bin.Status, bin.PartsCount, bin.PartId, bin.ProductId, 
                     bin.WorkOrderId, bin.Contents, bin.AssignedDate },
                station: station, workOrderId: activeWorkOrderId,
                details: $"Bin {bin.BinLabel} cleared - {partsRemoved} parts removed",
                sessionId: sessionId, ipAddress: ipAddress);

            // Get updated cut parts count
            var updatedCutPartsCount = await _context.Parts
                .Include(p => p.NestSheet)
                .CountAsync(p => p.NestSheet!.WorkOrderId == activeWorkOrderId && p.Status == PartStatus.Cut);

            _logger.LogInformation("Bin {BinLabel} cleared - {PartsRemoved} parts removed and changed to Cut status", 
                bin.BinLabel, partsRemoved);

            return Json(new { 
                success = true, 
                message = $"Bin {bin.BinLabel} cleared successfully.",
                partsRemoved = partsRemoved,
                updatedCutPartsCount = updatedCutPartsCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing bin at rack {RackId} position {Row},{Column}", rackId, row, column);
            return Json(new { success = false, message = "An error occurred while clearing the bin." });
        }
    }
}