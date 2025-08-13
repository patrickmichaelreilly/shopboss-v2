using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Models;
using ShopBoss.Web.Services;
using System.Text.RegularExpressions;

namespace ShopBoss.Web.Controllers;

public class CncController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<CncController> _logger;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly AuditTrailService _auditTrail;
    public CncController(ShopBossDbContext context, ILogger<CncController> logger, 
        IHubContext<StatusHub> hubContext, AuditTrailService auditTrail)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
        _auditTrail = auditTrail;
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
                return View(new List<NestSheet>());
            }

            // Get nest sheets for active work order (excluding default nest sheet)
            var nestSheets = await _context.NestSheets
                .Include(n => n.Parts)
                .Where(n => n.WorkOrderId == activeWorkOrderId && n.Name != "Default Nest Sheet")
                .OrderBy(n => n.Name)
                .ToListAsync();

            // Get active work order details for display
            var activeWorkOrder = await _context.WorkOrders.FindAsync(activeWorkOrderId);
            ViewBag.ActiveWorkOrderId = activeWorkOrderId;
            ViewBag.ActiveWorkOrderName = activeWorkOrder?.Name ?? "Unknown";

            return View(nestSheets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nest sheets for CNC station");
            TempData["ErrorMessage"] = "An error occurred while loading the nest sheets.";
            return View(new List<NestSheet>());
        }
    }

    // ProcessScan method removed - using event-based Universal Scanner architecture

    [HttpPost]
    public async Task<IActionResult> ScanNestSheet(string barcode)
    {
        return await ProcessNestSheet(barcode);
    }

    [HttpPost]
    public async Task<IActionResult> ProcessNestSheet(string barcode)
    {
        var sessionId = HttpContext.Session.Id;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var station = "CNC";
        
        // Enhanced barcode validation
        var validationResult = ValidateBarcodeInternal(barcode);
        if (!validationResult.IsValid)
        {
            await _auditTrail.LogScanAsync(barcode ?? "[empty]", station, false, 
                validationResult.ErrorMessage, sessionId: sessionId, ipAddress: ipAddress,
                details: "Barcode validation failed");
            return Json(new { success = false, message = validationResult.ErrorMessage, type = "validation" });
        }

        var cleanBarcode = barcode!.Trim();
        
        try
        {
            // Get active work order from session
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                    "No active work order selected", sessionId: sessionId, ipAddress: ipAddress,
                    details: "No active work order in session");
                return Json(new { success = false, message = "No active work order selected. Please set an active work order from the Admin station.", type = "session" });
            }

            // Check for recent duplicate scans (within 30 seconds)
            var isDuplicateScan = await _auditTrail.HasRecentDuplicateScanAsync(cleanBarcode, station, TimeSpan.FromSeconds(30));
            if (isDuplicateScan)
            {
                await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                    "Duplicate scan within 30 seconds", workOrderId: activeWorkOrderId, 
                    sessionId: sessionId, ipAddress: ipAddress, details: "Duplicate scan prevention");
                return Json(new { success = false, message = "This barcode was scanned recently. Please wait before scanning again.", type = "duplicate" });
            }

            // Find nest sheet by barcode/name within the active work order
            var nestSheet = await _context.NestSheets
                .Include(n => n.Parts)
                .FirstOrDefaultAsync(n => n.WorkOrderId == activeWorkOrderId && 
                                         (EF.Functions.Collate(n.Barcode, "NOCASE") == EF.Functions.Collate(cleanBarcode, "NOCASE") || 
                                          EF.Functions.Collate(n.Name, "NOCASE") == EF.Functions.Collate(cleanBarcode, "NOCASE")));

            if (nestSheet == null)
            {
                // Try to find similar barcodes for helpful suggestions
                var suggestions = await GetSimilarBarcodes(cleanBarcode, activeWorkOrderId);
                var suggestionMessage = suggestions.Any() ? 
                    $" Did you mean: {string.Join(", ", suggestions.Take(3))}?" : "";
                    
                await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                    "Nest sheet not found", workOrderId: activeWorkOrderId, 
                    sessionId: sessionId, ipAddress: ipAddress, 
                    details: $"Searched in work order {activeWorkOrderId}");
                    
                return Json(new { 
                    success = false, 
                    message = $"Nest sheet with barcode/name '{cleanBarcode}' not found in active work order.{suggestionMessage}", 
                    type = "not_found",
                    suggestions = suggestions.Take(3).ToList()
                });
            }

            if (nestSheet.Status == PartStatus.Cut)
            {
                await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                    "Nest sheet already processed", nestSheet.Id, activeWorkOrderId, 
                    sessionId: sessionId, ipAddress: ipAddress, 
                    details: $"Already processed on {nestSheet.StatusUpdatedDate:yyyy-MM-dd HH:mm}");
                    
                return Json(new { 
                    success = false, 
                    message = $"Nest sheet '{nestSheet.Name}' has already been processed on {nestSheet.StatusUpdatedDate:yyyy-MM-dd HH:mm}.", 
                    type = "already_processed",
                    processedDate = nestSheet.StatusUpdatedDate?.ToString("yyyy-MM-dd HH:mm")
                });
            }

            // Store original values for audit trail
            var originalNestSheet = new { nestSheet.Status, nestSheet.StatusUpdatedDate };
            var originalParts = nestSheet.Parts.Where(p => p.Status == PartStatus.Pending)
                .Select(p => new { p.Id, p.Status, p.StatusUpdatedDate }).ToList();

            // Mark nest sheet as processed
            nestSheet.Status = PartStatus.Cut;
            nestSheet.StatusUpdatedDate = DateTime.Now;

            // Mark all parts on this nest sheet as Cut
            var partsToUpdate = nestSheet.Parts.Where(p => p.Status == PartStatus.Pending).ToList();
            foreach (var part in partsToUpdate)
            {
                part.Status = PartStatus.Cut;
                part.StatusUpdatedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            // Log successful scan and processing
            await _auditTrail.LogScanAsync(cleanBarcode, station, true, 
                nestSheetId: nestSheet.Id, workOrderId: activeWorkOrderId, 
                partsProcessed: partsToUpdate.Count, sessionId: sessionId, ipAddress: ipAddress,
                details: $"Processed {partsToUpdate.Count} parts from {nestSheet.Material} sheet");

            // Log audit trail for nest sheet processing
            await _auditTrail.LogAsync("ProcessNestSheet", "NestSheet", nestSheet.Id, 
                originalNestSheet, new { nestSheet.Status, nestSheet.StatusUpdatedDate },
                station: station, workOrderId: activeWorkOrderId, 
                details: $"Scanned barcode '{cleanBarcode}' - {partsToUpdate.Count} parts marked as Cut",
                sessionId: sessionId, ipAddress: ipAddress);

            // Log audit trail for each part status change
            foreach (var part in partsToUpdate)
            {
                var originalPart = originalParts.FirstOrDefault(p => p.Id == part.Id);
                if (originalPart != null)
                {
                    await _auditTrail.LogAsync("StatusChange", "Part", part.Id,
                        new { Status = originalPart.Status.ToString(), originalPart.StatusUpdatedDate },
                        new { Status = part.Status.ToString(), part.StatusUpdatedDate },
                        station: station, workOrderId: activeWorkOrderId,
                        details: $"Part status changed via nest sheet scan of '{cleanBarcode}'",
                        sessionId: sessionId, ipAddress: ipAddress);
                }
            }

            // Send enhanced real-time updates via SignalR
            var updateData = new
            {
                nestSheetId = nestSheet.Id,
                nestSheetName = nestSheet.Name,
                material = nestSheet.Material,
                partsProcessed = partsToUpdate.Count,
                processedDate = nestSheet.StatusUpdatedDate?.ToString("yyyy-MM-dd HH:mm"),
                station = station,
                barcode = cleanBarcode
            };

            await _hubContext.Clients.Groups($"workorder-{nestSheet.WorkOrderId}")
                .SendAsync("NestSheetProcessed", updateData);

            await _hubContext.Clients.Group("cnc-station")
                .SendAsync("StatusUpdate", new
                {
                    type = "nest-sheet-processed",
                    nestSheetId = nestSheet.Id,
                    nestSheetName = nestSheet.Name,
                    partsProcessed = partsToUpdate.Count,
                    material = nestSheet.Material,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

            // Send progress update to all connected clients
            await _hubContext.Clients.All.SendAsync("ProgressUpdate", new
            {
                workOrderId = activeWorkOrderId,
                station = station,
                action = "nest-sheet-processed",
                details = updateData
            });

            _logger.LogInformation("Successfully processed nest sheet {NestSheetId} ({NestSheetName}) - {PartsCount} parts marked as Cut via barcode {Barcode}", 
                nestSheet.Id, nestSheet.Name, partsToUpdate.Count, cleanBarcode);

            return Json(new { 
                success = true, 
                message = $"✅ Successfully processed nest sheet '{nestSheet.Name}'. {partsToUpdate.Count} parts marked as Cut.",
                nestSheetId = nestSheet.Id,
                nestSheetName = nestSheet.Name,
                partsProcessed = partsToUpdate.Count,
                material = nestSheet.Material,
                processedDate = nestSheet.StatusUpdatedDate?.ToString("yyyy-MM-dd HH:mm"),
                type = "success"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing nest sheet with barcode {Barcode}", cleanBarcode);
            
            await _auditTrail.LogScanAsync(cleanBarcode, station, false, 
                $"System error: {ex.Message}", sessionId: sessionId, ipAddress: ipAddress,
                details: $"Exception: {ex.GetType().Name}");
                
            return Json(new { 
                success = false, 
                message = "An unexpected error occurred while processing the nest sheet. Please try again or contact support.",
                type = "system_error"
            });
        }
    }

    private static (bool IsValid, string ErrorMessage) ValidateBarcodeInternal(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return (false, "⚠️ Barcode is required. Please scan or enter a barcode.");
        }

        var cleanBarcode = barcode.Trim();
        
        if (cleanBarcode.Length < 2)
        {
            return (false, "⚠️ Barcode is too short. Please check and try again.");
        }

        if (cleanBarcode.Length > 100)
        {
            return (false, "⚠️ Barcode is too long. Please check and try again.");
        }

        // Check for potentially dangerous characters
        if (Regex.IsMatch(cleanBarcode, @"[<>""'&]"))
        {
            return (false, "⚠️ Barcode contains invalid characters. Please check and try again.");
        }

        return (true, string.Empty);
    }

    private async Task<List<string>> GetSimilarBarcodes(string barcode, string workOrderId)
    {
        try
        {
            var nestSheets = await _context.NestSheets
                .Where(n => n.WorkOrderId == workOrderId)
                .Select(n => new { n.Barcode, n.Name })
                .ToListAsync();

            var similarities = new List<(string Code, int Distance)>();
            
            foreach (var sheet in nestSheets)
            {
                // Check both barcode and name
                var barcodeDistance = CalculateLevenshteinDistance(barcode.ToLower(), sheet.Barcode.ToLower());
                var nameDistance = CalculateLevenshteinDistance(barcode.ToLower(), sheet.Name.ToLower());
                
                var minDistance = Math.Min(barcodeDistance, nameDistance);
                if (minDistance <= 3) // Allow up to 3 character differences
                {
                    similarities.Add((sheet.Barcode, minDistance));
                }
            }

            return similarities
                .OrderBy(s => s.Distance)
                .Select(s => s.Code)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding similar barcodes for {Barcode}", barcode);
            return new List<string>();
        }
    }

    private static int CalculateLevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    // Manual nest sheet creation removed - nest sheets should only come from import process

    public async Task<IActionResult> GetNestSheetDetails(string id)
    {
        try
        {
            var nestSheet = await _context.NestSheets
                .Include(n => n.Parts)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (nestSheet == null)
            {
                return Json(new { success = false, message = "Nest sheet not found." });
            }

            var details = new
            {
                success = true,
                nestSheet = new
                {
                    id = nestSheet.Id,
                    name = nestSheet.Name,
                    material = nestSheet.Material,
                    length = nestSheet.Length,
                    width = nestSheet.Width,
                    thickness = nestSheet.Thickness,
                    barcode = nestSheet.Barcode,
                    isProcessed = nestSheet.Status == PartStatus.Cut,
                    processedDate = nestSheet.StatusUpdatedDate?.ToString("yyyy-MM-dd HH:mm"),
                    createdDate = nestSheet.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                    partCount = nestSheet.Parts.Count,
                    cutPartCount = nestSheet.Parts.Count(p => p.Status >= PartStatus.Cut),
                    parts = await Task.WhenAll(nestSheet.Parts.Select(async p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        qty = p.Qty,
                        status = p.Status.ToString(),
                        productName = await GetProductNameForPart(p.ProductId),
                        material = p.Material,
                        length = p.Length,
                        width = p.Width,
                        thickness = p.Thickness
                    }))
                }
            };

            return Json(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nest sheet details for {NestSheetId}", id);
            return Json(new { success = false, message = "An error occurred while retrieving nest sheet details." });
        }
    }

    public async Task<IActionResult> GetRecentScans()
    {
        try
        {
            var recentScans = await _auditTrail.GetRecentScansAsync("CNC", 5);
            
            var scanData = recentScans.Select(s => new
            {
                id = s.Id,
                timestamp = s.Timestamp.ToString("HH:mm:ss"),
                barcode = s.Barcode,
                isSuccessful = s.IsSuccessful,
                nestSheetName = s.NestSheet?.Name,
                partsProcessed = s.PartsProcessed,
                errorMessage = s.ErrorMessage
            }).ToList();

            return Json(new { success = true, scans = scanData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent scans for CNC station");
            return Json(new { success = false, message = "Failed to load recent scan history." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ValidateBarcode(string barcode)
    {
        var validation = ValidateBarcodeInternal(barcode);
        if (!validation.IsValid)
        {
            return Json(new { success = false, message = validation.ErrorMessage, type = "validation" });
        }

        try
        {
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return Json(new { success = false, message = "No active work order selected.", type = "session" });
            }

            var cleanBarcode = barcode.Trim();
            var nestSheet = await _context.NestSheets
                .Where(n => n.WorkOrderId == activeWorkOrderId && 
                           (n.Barcode == cleanBarcode || n.Name == cleanBarcode))
                .Select(n => new { n.Id, n.Name, n.Status, n.StatusUpdatedDate })
                .FirstOrDefaultAsync();

            if (nestSheet == null)
            {
                var suggestions = await GetSimilarBarcodes(cleanBarcode, activeWorkOrderId);
                return Json(new { 
                    success = false, 
                    message = "Nest sheet not found.", 
                    type = "not_found",
                    suggestions = suggestions.Take(3).ToList()
                });
            }

            if (nestSheet.Status == PartStatus.Cut)
            {
                return Json(new { 
                    success = false, 
                    message = $"Already processed on {nestSheet.StatusUpdatedDate:yyyy-MM-dd HH:mm}", 
                    type = "already_processed"
                });
            }

            return Json(new { 
                success = true, 
                message = $"Ready to process: {nestSheet.Name}",
                nestSheetId = nestSheet.Id,
                nestSheetName = nestSheet.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating barcode {Barcode}", barcode);
            return Json(new { success = false, message = "Validation failed.", type = "system_error" });
        }
    }

    /// <summary>
    /// Gets the product display name for a part in "ItemNumber - ProductName" format, checking both Products and DetachedProducts tables
    /// </summary>
    private async Task<string> GetProductNameForPart(string? productId)
    {
        if (string.IsNullOrEmpty(productId))
            return "Unknown";

        try
        {
            // First try regular Products table
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
                return $"{product.ItemNumber} - {product.Name}";

            // If not found, try DetachedProducts table
            var detachedProduct = await _context.DetachedProducts.FindAsync(productId);
            if (detachedProduct != null)
                return $"{detachedProduct.ItemNumber} - {detachedProduct.Name}";

            return "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting product name for ProductId {ProductId}", productId);
            return "Unknown";
        }
    }

    /// <summary>
    /// Get the label HTML for a specific part
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPartLabel(string partId)
    {
        try
        {
            if (string.IsNullOrEmpty(partId))
            {
                return BadRequest("Part ID is required");
            }

            // Get the active work order from session
            var activeWorkOrderId = HttpContext.Session.GetString("ActiveWorkOrderId");
            if (string.IsNullOrEmpty(activeWorkOrderId))
            {
                return BadRequest("No active work order selected");
            }

            // Find the label for this part in this work order
            var label = await _context.PartLabels
                .FirstOrDefaultAsync(l => l.PartId == partId && l.WorkOrderId == activeWorkOrderId);

            if (label == null)
            {
                _logger.LogInformation("No label found for part {PartId} in work order {WorkOrderId}", partId, activeWorkOrderId);
                return NotFound($"No label found for part {partId}");
            }

            // Return the HTML content directly
            return Content(label.LabelHtml, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving label for part {PartId}", partId);
            return StatusCode(500, "An error occurred while retrieving the part label");
        }
    }
}