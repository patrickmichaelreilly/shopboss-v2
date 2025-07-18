using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models.Import;
using ShopBoss.Web.Models;
using System.Collections.Concurrent;

namespace ShopBoss.Web.Controllers;

/// <summary>
/// Phase I2: New Import Controller - Parallel import system using Work Order entities
/// Routes: /admin/newimport/* to avoid conflicts with existing import system
/// </summary>
public class NewImportController : Controller
{
    private readonly ImporterService _importerService;
    private readonly WorkOrderImportService _workOrderImportService;
    private readonly ImportSelectionService _selectionService;
    private readonly IHubContext<ImportProgressHub> _hubContext;
    private readonly ILogger<NewImportController> _logger;
    private readonly string _tempUploadPath;
    
    // Reuse existing import sessions storage - sessions will have both Import and WorkOrder entities
    private static readonly ConcurrentDictionary<string, ImportSession> _importSessions = new();

    public NewImportController(
        ImporterService importerService,
        WorkOrderImportService workOrderImportService,
        ImportSelectionService selectionService,
        IHubContext<ImportProgressHub> hubContext,
        ILogger<NewImportController> logger,
        IWebHostEnvironment environment)
    {
        _importerService = importerService;
        _workOrderImportService = workOrderImportService;
        _selectionService = selectionService;
        _hubContext = hubContext;
        _logger = logger;
        _tempUploadPath = Path.Combine(environment.ContentRootPath, "temp", "uploads");
        
        // Ensure temp directory exists
        Directory.CreateDirectory(_tempUploadPath);
    }

    /// <summary>
    /// Phase I2: New import upload endpoint
    /// Route: /admin/newimport/upload
    /// </summary>
    [HttpPost("admin/newimport/upload")]
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

            // Create import session - Phase I2: Will store both Import and WorkOrder entities
            var session = new ImportSession
            {
                Id = sessionId,
                FileName = file.FileName,
                FilePath = filePath,
                Status = ImportStatus.Uploaded,
                CreatedAt = DateTime.Now
            };

            _importSessions[sessionId] = session;

            _logger.LogInformation("Phase I2: New import file uploaded successfully: {FileName} (Session: {SessionId})", 
                file.FileName, sessionId);

            return Ok(new { sessionId, fileName = file.FileName, fileSize = file.Length });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase I2: Error uploading file: {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to upload file" });
        }
    }

    /// <summary>
    /// Phase I2: Start new import process
    /// Route: /admin/newimport/start
    /// </summary>
    [HttpPost("admin/newimport/start")]
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
            session.WorkOrderName = request.WorkOrderName ?? "New Import Work Order";

            // Start background import task - Phase I2: Creates both Import and WorkOrder entities
            _ = Task.Run(() => ProcessNewImportAsync(session));

            _logger.LogInformation("Phase I2: New import started for session {SessionId}", request.SessionId);

            return Ok(new { message = "New import started", sessionId = request.SessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase I2: Error starting new import for session {SessionId}", request.SessionId);
            session.Status = ImportStatus.Failed;
            session.ErrorMessage = ex.Message;
            return StatusCode(500, new { error = "Failed to start new import" });
        }
    }

    /// <summary>
    /// Phase I2: Get import status
    /// Route: /admin/newimport/status
    /// </summary>
    [HttpGet("admin/newimport/status")]
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

    /// <summary>
    /// Phase I2: Get tree data for new import preview
    /// Route: /admin/newimport/tree
    /// </summary>
    [HttpGet("admin/newimport/tree")]
    public IActionResult GetNewImportTreeData(string sessionId)
    {
        if (!_importSessions.TryGetValue(sessionId, out var session))
        {
            return NotFound(new { error = "Import session not found" });
        }

        if (session.Status != ImportStatus.Completed || session.WorkOrderEntities == null)
        {
            return BadRequest(new { error = "Import not completed or no WorkOrder data available" });
        }

        try
        {
            // Phase I2: Return tree data based on WorkOrder entities instead of Import entities
            var workOrder = session.WorkOrderEntities;
            var response = new Models.Api.TreeDataResponse
            {
                WorkOrderId = workOrder.Id,
                WorkOrderName = workOrder.Name,
                Items = new List<Models.Api.TreeItem>()
            };

            // Build tree structure from WorkOrder entities
            // This will work seamlessly with the existing tree partial since it uses the same data structure
            BuildTreeFromWorkOrderEntities(workOrder, response);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase I2: Error generating tree data for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to generate tree data" });
        }
    }

    /// <summary>
    /// Phase I2: Process import with WorkOrder entities
    /// </summary>
    private async Task ProcessNewImportAsync(ImportSession session)
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
                _logger.LogWarning(ex, "Phase I2: Failed to send progress update via SignalR for session {SessionId}", session.Id);
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

            // Run the importer to get raw data
            var result = await _importerService.ImportSdfFileAsync(session.FilePath, progress);

            if (!result.Success)
            {
                session.Status = ImportStatus.Failed;
                session.ErrorMessage = result.Message;
                
                await _hubContext.Clients.Group($"import-{session.Id}")
                    .SendAsync("ImportError", new { sessionId = session.Id, error = result.Message });
                return;
            }

            // Phase I2: Transform raw data directly to WorkOrder entities
            if (result.Data != null)
            {
                session.RawImportData = result.Data;
                
                // Create WorkOrder entities directly (no Import entities)
                session.WorkOrderEntities = await _workOrderImportService.TransformToWorkOrderAsync(
                    result.Data, session.WorkOrderName ?? "New Import Work Order");
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
                        statistics = session.WorkOrderEntities?.GetStatistics()
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phase I2: Failed to send completion notification via SignalR for session {SessionId}", session.Id);
            }

            _logger.LogInformation("Phase I2: New import completed successfully for session {SessionId}", session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase I2: Error processing new import for session {SessionId}", session.Id);
            
            session.Status = ImportStatus.Failed;
            session.ErrorMessage = ex.Message;

            try
            {
                await _hubContext.Clients.Group($"import-{session.Id}")
                    .SendAsync("ImportError", new { sessionId = session.Id, error = ex.Message });
            }
            catch (Exception signalREx)
            {
                _logger.LogWarning(signalREx, "Phase I2: Failed to send error notification via SignalR for session {SessionId}", session.Id);
            }
        }
    }

    /// <summary>
    /// Phase I2: Build tree structure from WorkOrder entities
    /// </summary>
    private void BuildTreeFromWorkOrderEntities(WorkOrder workOrder, Models.Api.TreeDataResponse response)
    {
        // Similar structure to existing tree builder but using WorkOrder entities
        // This ensures compatibility with the existing tree partial
        
        // Products Category
        if (workOrder.Products.Any())
        {
            var productsCategory = new Models.Api.TreeItem
            {
                Id = "category_products",
                Name = $"Products ({workOrder.Products.Count})",
                Type = "category",
                Quantity = workOrder.Products.Count,
                Status = null,
                Children = new List<Models.Api.TreeItem>()
            };

            foreach (var product in workOrder.Products)
            {
                var productItem = new Models.Api.TreeItem
                {
                    Id = product.Id,
                    Name = product.Name,
                    Type = "product",
                    Quantity = product.Qty,
                    Status = product.Status.ToString(),
                    Children = new List<Models.Api.TreeItem>()
                };

                // Add Parts subcategory
                if (product.Parts.Any())
                {
                    var partsCategory = new Models.Api.TreeItem
                    {
                        Id = $"category_parts_{product.Id}",
                        Name = $"Parts ({product.Parts.Count})",
                        Type = "category",
                        Quantity = product.Parts.Count,
                        Status = null,
                        Children = new List<Models.Api.TreeItem>()
                    };

                    foreach (var part in product.Parts)
                    {
                        partsCategory.Children.Add(new Models.Api.TreeItem
                        {
                            Id = part.Id,
                            Name = part.Name,
                            Type = "part",
                            Quantity = part.Qty,
                            Status = part.Status.ToString(),
                            Children = new List<Models.Api.TreeItem>()
                        });
                    }

                    productItem.Children.Add(partsCategory);
                }

                // Add Subassemblies subcategory
                if (product.Subassemblies.Any())
                {
                    var subassembliesCategory = new Models.Api.TreeItem
                    {
                        Id = $"category_subassemblies_{product.Id}",
                        Name = $"Subassemblies ({product.Subassemblies.Count})",
                        Type = "category",
                        Quantity = product.Subassemblies.Count,
                        Status = null,
                        Children = new List<Models.Api.TreeItem>()
                    };

                    foreach (var subassembly in product.Subassemblies)
                    {
                        var subassemblyItem = new Models.Api.TreeItem
                        {
                            Id = subassembly.Id,
                            Name = subassembly.Name,
                            Type = "subassembly",
                            Quantity = subassembly.Qty,
                            Status = "", // Subassembly doesn't have Status in current schema
                            Children = new List<Models.Api.TreeItem>()
                        };

                        // Add parts under subassembly
                        foreach (var part in subassembly.Parts)
                        {
                            subassemblyItem.Children.Add(new Models.Api.TreeItem
                            {
                                Id = part.Id,
                                Name = part.Name,
                                Type = "part",
                                Quantity = part.Qty,
                                Status = part.Status.ToString(),
                                Children = new List<Models.Api.TreeItem>()
                            });
                        }

                        subassembliesCategory.Children.Add(subassemblyItem);
                    }

                    productItem.Children.Add(subassembliesCategory);
                }

                // Add Hardware subcategory
                if (product.Hardware.Any())
                {
                    var hardwareCategory = new Models.Api.TreeItem
                    {
                        Id = $"category_hardware_{product.Id}",
                        Name = $"Hardware ({product.Hardware.Count})",
                        Type = "category",
                        Quantity = product.Hardware.Count,
                        Status = null,
                        Children = new List<Models.Api.TreeItem>()
                    };

                    foreach (var hardware in product.Hardware)
                    {
                        hardwareCategory.Children.Add(new Models.Api.TreeItem
                        {
                            Id = hardware.Id,
                            Name = hardware.Name,
                            Type = "hardware",
                            Quantity = hardware.Qty,
                            Status = hardware.Status.ToString(),
                            Children = new List<Models.Api.TreeItem>()
                        });
                    }

                    productItem.Children.Add(hardwareCategory);
                }
                
                productsCategory.Children.Add(productItem);
            }

            response.Items.Add(productsCategory);
        }

        // DetachedProducts Category
        if (workOrder.DetachedProducts.Any())
        {
            var detachedProductsCategory = new Models.Api.TreeItem
            {
                Id = "category_detached_products",
                Name = $"Detached Products ({workOrder.DetachedProducts.Count})",
                Type = "category",
                Quantity = workOrder.DetachedProducts.Count,
                Status = null,
                Children = new List<Models.Api.TreeItem>()
            };

            foreach (var detachedProduct in workOrder.DetachedProducts)
            {
                detachedProductsCategory.Children.Add(new Models.Api.TreeItem
                {
                    Id = detachedProduct.Id,
                    Name = detachedProduct.Name,
                    Type = "detached_product",
                    Quantity = detachedProduct.Qty,
                    Status = detachedProduct.Status.ToString(),
                    Children = new List<Models.Api.TreeItem>()
                });
            }

            response.Items.Add(detachedProductsCategory);
        }

        // Hardware Category
        if (workOrder.Hardware.Any())
        {
            var hardwareCategory = new Models.Api.TreeItem
            {
                Id = "category_hardware",
                Name = $"Hardware ({workOrder.Hardware.Count})",
                Type = "category",
                Quantity = workOrder.Hardware.Count,
                Status = null,
                Children = new List<Models.Api.TreeItem>()
            };

            foreach (var hardware in workOrder.Hardware)
            {
                hardwareCategory.Children.Add(new Models.Api.TreeItem
                {
                    Id = hardware.Id,
                    Name = hardware.Name,
                    Type = "hardware",
                    Quantity = hardware.Qty,
                    Status = hardware.Status.ToString(),
                    Children = new List<Models.Api.TreeItem>()
                });
            }

            response.Items.Add(hardwareCategory);
        }

        // Nest Sheets Category
        if (workOrder.NestSheets.Any())
        {
            var nestSheetsCategory = new Models.Api.TreeItem
            {
                Id = "category_nestsheets",
                Name = $"Nest Sheets ({workOrder.NestSheets.Count})",
                Type = "category",
                Quantity = workOrder.NestSheets.Count,
                Status = null,
                Children = new List<Models.Api.TreeItem>()
            };

            foreach (var nestSheet in workOrder.NestSheets)
            {
                var nestSheetItem = new Models.Api.TreeItem
                {
                    Id = nestSheet.Id,
                    Name = nestSheet.Name,
                    Type = "nestsheet",
                    Quantity = 1,
                    Status = nestSheet.Status.ToString(),
                    Children = new List<Models.Api.TreeItem>()
                };

                foreach (var part in nestSheet.Parts)
                {
                    nestSheetItem.Children.Add(new Models.Api.TreeItem
                    {
                        Id = part.Id,
                        Name = part.Name,
                        Type = "part",
                        Quantity = part.Qty,
                        Status = part.Status.ToString(),
                        Children = new List<Models.Api.TreeItem>()
                    });
                }

                nestSheetsCategory.Children.Add(nestSheetItem);
            }

            response.Items.Add(nestSheetsCategory);
        }
    }
}

/// <summary>
/// Phase I2: Extension methods for WorkOrder statistics
/// </summary>
public static class WorkOrderExtensions
{
    public static object GetStatistics(this WorkOrder workOrder)
    {
        return new
        {
            totalProducts = workOrder.Products.Count,
            totalParts = workOrder.Products.SelectMany(p => p.Parts).Count() + 
                        workOrder.Products.SelectMany(p => p.Subassemblies).SelectMany(s => s.Parts).Count(),
            totalHardware = workOrder.Hardware.Count + workOrder.Products.SelectMany(p => p.Hardware).Count(),
            totalDetachedProducts = workOrder.DetachedProducts.Count,
            totalNestSheets = workOrder.NestSheets.Count
        };
    }
}