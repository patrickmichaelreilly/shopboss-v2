using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models.Import;
using System.Collections.Concurrent;

namespace ShopBoss.Web.Controllers;

public class ImportController : Controller
{
    private readonly ImporterService _importerService;
    private readonly ImportDataTransformService _transformService;
    private readonly IHubContext<ImportProgressHub> _hubContext;
    private readonly ILogger<ImportController> _logger;
    private readonly string _tempUploadPath;
    
    // In-memory storage for import sessions (in production, use distributed cache or database)
    private static readonly ConcurrentDictionary<string, ImportSession> _importSessions = new();

    public ImportController(
        ImporterService importerService,
        ImportDataTransformService transformService,
        IHubContext<ImportProgressHub> hubContext,
        ILogger<ImportController> logger,
        IWebHostEnvironment environment)
    {
        _importerService = importerService;
        _transformService = transformService;
        _hubContext = hubContext;
        _logger = logger;
        _tempUploadPath = Path.Combine(environment.ContentRootPath, "temp", "uploads");
        
        // Ensure temp directory exists
        Directory.CreateDirectory(_tempUploadPath);
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        if (!file.FileName.EndsWith(".sdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only .sdf files are allowed" });
        }

        if (file.Length > 100 * 1024 * 1024) // 100MB limit
        {
            return BadRequest(new { error = "File too large. Maximum size is 100MB" });
        }

        try
        {
            // Generate unique session ID and file path
            var sessionId = Guid.NewGuid().ToString();
            var fileName = $"{sessionId}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(_tempUploadPath, fileName);

            // Save uploaded file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Create import session
            var session = new ImportSession
            {
                Id = sessionId,
                FileName = file.FileName,
                FilePath = filePath,
                Status = ImportStatus.Uploaded,
                CreatedAt = DateTime.Now
            };

            _importSessions[sessionId] = session;

            _logger.LogInformation("File uploaded successfully: {FileName} (Session: {SessionId})", 
                file.FileName, sessionId);

            return Ok(new { sessionId, fileName = file.FileName, fileSize = file.Length });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to upload file" });
        }
    }

    [HttpPost]
    public IActionResult StartImport([FromBody] StartImportRequest request)
    {
        if (string.IsNullOrEmpty(request.SessionId))
        {
            return BadRequest(new { error = "Session ID is required" });
        }

        if (!_importSessions.TryGetValue(request.SessionId, out var session))
        {
            return NotFound(new { error = "Import session not found" });
        }

        if (session.Status != ImportStatus.Uploaded)
        {
            return BadRequest(new { error = "Import already started or completed" });
        }

        try
        {
            // Update session status
            session.Status = ImportStatus.Processing;
            session.WorkOrderName = request.WorkOrderName ?? "Imported Work Order";

            // Start background import task
            _ = Task.Run(() => ProcessImportAsync(session));

            _logger.LogInformation("Import started for session {SessionId}", request.SessionId);

            return Ok(new { message = "Import started", sessionId = request.SessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting import for session {SessionId}", request.SessionId);
            session.Status = ImportStatus.Failed;
            session.ErrorMessage = ex.Message;
            return StatusCode(500, new { error = "Failed to start import" });
        }
    }

    [HttpGet]
    public IActionResult GetImportStatus(string sessionId)
    {
        if (!_importSessions.TryGetValue(sessionId, out var session))
        {
            return NotFound(new { error = "Import session not found" });
        }

        return Ok(new
        {
            sessionId,
            status = session.Status.ToString(),
            progress = session.Progress,
            stage = session.CurrentStage,
            estimatedTimeRemaining = session.EstimatedTimeRemaining?.TotalSeconds,
            errorMessage = session.ErrorMessage,
            workOrderName = session.WorkOrderName,
            createdAt = session.CreatedAt,
            completedAt = session.CompletedAt
        });
    }

    [HttpGet]
    public IActionResult GetImportData(string sessionId)
    {
        if (!_importSessions.TryGetValue(sessionId, out var session))
        {
            return NotFound(new { error = "Import session not found" });
        }

        if (session.Status != ImportStatus.Completed || session.ImportWorkOrder == null)
        {
            return BadRequest(new { error = "Import not completed or no data available" });
        }

        return Ok(session.ImportWorkOrder);
    }

    [HttpGet]
    public IActionResult ExportRawDataCsv(string sessionId)
    {
        if (!_importSessions.TryGetValue(sessionId, out var session) || session.RawImportData == null)
        {
            return NotFound(new { error = "Import session not found or no raw data available" });
        }

        var csv = GenerateRawDataCsv(session.RawImportData);
        var fileName = $"raw-import-data-{sessionId[..8]}.csv";
        
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private string GenerateRawDataCsv(ImportData rawData)
    {
        var lines = new List<string>();
        
        // Get all unique keys from all data types to understand the actual column structure
        var allKeys = new HashSet<string>();
        
        foreach (var product in rawData.Products ?? new List<Dictionary<string, object?>>())
            foreach (var key in product.Keys) allKeys.Add(key);
            
        foreach (var subassembly in rawData.Subassemblies ?? new List<Dictionary<string, object?>>())
            foreach (var key in subassembly.Keys) allKeys.Add(key);
            
        foreach (var part in rawData.Parts ?? new List<Dictionary<string, object?>>())
            foreach (var key in part.Keys) allKeys.Add(key);
            
        foreach (var hardware in rawData.Hardware ?? new List<Dictionary<string, object?>>())
            foreach (var key in hardware.Keys) allKeys.Add(key);
        
        var sortedKeys = allKeys.OrderBy(k => k).ToList();
        
        // Header with DataType first, then all discovered keys
        lines.Add("DataType," + string.Join(",", sortedKeys));
        
        // Products
        foreach (var product in rawData.Products ?? new List<Dictionary<string, object?>>())
        {
            var values = sortedKeys.Select(key => GetCsvValue(product, key));
            lines.Add("Product," + string.Join(",", values));
        }
        
        // Subassemblies
        foreach (var subassembly in rawData.Subassemblies ?? new List<Dictionary<string, object?>>())
        {
            var values = sortedKeys.Select(key => GetCsvValue(subassembly, key));
            lines.Add("Subassembly," + string.Join(",", values));
        }
        
        // Parts
        foreach (var part in rawData.Parts ?? new List<Dictionary<string, object?>>())
        {
            var values = sortedKeys.Select(key => GetCsvValue(part, key));
            lines.Add("Part," + string.Join(",", values));
        }
        
        // Hardware
        foreach (var hardware in rawData.Hardware ?? new List<Dictionary<string, object?>>())
        {
            var values = sortedKeys.Select(key => GetCsvValue(hardware, key));
            lines.Add("Hardware," + string.Join(",", values));
        }
        
        return string.Join("\n", lines);
    }

    private string GetCsvValue(Dictionary<string, object?> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value != null)
        {
            var str = value.ToString() ?? "";
            // Escape quotes and wrap in quotes if contains comma
            if (str.Contains(',') || str.Contains('"') || str.Contains('\n'))
            {
                return $"\"{str.Replace("\"", "\"\"")}\"";
            }
            return str;
        }
        return "";
    }

    [HttpDelete]
    public IActionResult CancelImport(string sessionId)
    {
        if (!_importSessions.TryGetValue(sessionId, out var session))
        {
            return NotFound(new { error = "Import session not found" });
        }

        if (session.Status == ImportStatus.Processing)
        {
            session.CancellationRequested = true;
            _logger.LogInformation("Import cancellation requested for session {SessionId}", sessionId);
            return Ok(new { message = "Import cancellation requested" });
        }

        return BadRequest(new { error = "Import cannot be cancelled in current state" });
    }

    private async Task ProcessImportAsync(ImportSession session)
    {
        var progress = new Progress<ImporterProgress>(async p =>
        {
            session.Progress = p.Percentage;
            session.CurrentStage = p.Stage;
            session.EstimatedTimeRemaining = p.EstimatedTimeRemaining;

            // Send progress update via SignalR
            try
            {
                await _hubContext.Clients.Group($"import-{session.Id}")
                    .SendAsync("ImportProgress", new
                    {
                        sessionId = session.Id,
                        percentage = p.Percentage,
                        stage = p.Stage,
                        estimatedTimeRemaining = p.EstimatedTimeRemaining.TotalSeconds
                    });
            }
            catch (Exception ex)
            {
                // Log SignalR errors but don't fail the import
                _logger.LogWarning(ex, "Failed to send progress update via SignalR for session {SessionId}", session.Id);
            }
        });

        try
        {
            // Check for cancellation
            if (session.CancellationRequested)
            {
                session.Status = ImportStatus.Cancelled;
                return;
            }

            // Run the importer
            var result = await _importerService.ImportSdfFileAsync(session.FilePath, progress);

            if (!result.Success)
            {
                session.Status = ImportStatus.Failed;
                session.ErrorMessage = result.Message;
                
                await _hubContext.Clients.Group($"import-{session.Id}")
                    .SendAsync("ImportError", new { sessionId = session.Id, error = result.Message });
                return;
            }

            // Store raw data and transform the data
            if (result.Data != null)
            {
                session.RawImportData = result.Data;
                session.ImportWorkOrder = _transformService.TransformToImportWorkOrder(
                    result.Data, session.WorkOrderName ?? "Imported Work Order");
            }

            session.Status = ImportStatus.Completed;
            session.CompletedAt = DateTime.Now;

            // Send completion notification
            try
            {
                await _hubContext.Clients.Group($"import-{session.Id}")
                    .SendAsync("ImportComplete", new
                    {
                        sessionId = session.Id,
                        statistics = session.ImportWorkOrder?.Statistics
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send completion notification via SignalR for session {SessionId}", session.Id);
            }

            _logger.LogInformation("Import completed successfully for session {SessionId}", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing import for session {SessionId}", session.Id);
            
            session.Status = ImportStatus.Failed;
            session.ErrorMessage = ex.Message;

            try
            {
                await _hubContext.Clients.Group($"import-{session.Id}")
                    .SendAsync("ImportError", new { sessionId = session.Id, error = ex.Message });
            }
            catch (Exception signalREx)
            {
                _logger.LogWarning(signalREx, "Failed to send error notification via SignalR for session {SessionId}", session.Id);
            }
        }
        finally
        {
            // Schedule cleanup of temporary file (after 1 hour)
            _ = Task.Delay(TimeSpan.FromHours(1)).ContinueWith(_ =>
            {
                try
                {
                    if (System.IO.File.Exists(session.FilePath))
                    {
                        System.IO.File.Delete(session.FilePath);
                        _logger.LogInformation("Cleaned up temporary file for session {SessionId}", session.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary file for session {SessionId}", session.Id);
                }
            });
        }
    }
}

public class ImportSession
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? WorkOrderName { get; set; }
    public ImportStatus Status { get; set; }
    public int Progress { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool CancellationRequested { get; set; }
    public ImportWorkOrder? ImportWorkOrder { get; set; }
    public ImportData? RawImportData { get; set; }
}

public enum ImportStatus
{
    Uploaded,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public class StartImportRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string WorkOrderName { get; set; } = string.Empty;
}