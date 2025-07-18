using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models.Import;
using ShopBoss.Web.Models;
using ShopBoss.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace ShopBoss.Web.Controllers;

/// <summary>
/// Phase I2: New Import Controller - Parallel import system using Work Order entities
/// Routes: /admin/import/* for the new import system
/// </summary>
public class NewImportController : Controller
{
    private readonly ImporterService _importerService;
    private readonly WorkOrderImportService _workOrderImportService;
    private readonly ShopBossDbContext _context;
    private readonly PartFilteringService _partFilteringService;
    private readonly IHubContext<ImportProgressHub> _hubContext;
    private readonly ILogger<NewImportController> _logger;
    private readonly string _tempUploadPath;
    
    // Reuse existing import sessions storage - sessions will have both Import and WorkOrder entities
    private static readonly ConcurrentDictionary<string, ImportSession> _importSessions = new();

    public NewImportController(
        ImporterService importerService,
        WorkOrderImportService workOrderImportService,
        ShopBossDbContext context,
        PartFilteringService partFilteringService,
        IHubContext<ImportProgressHub> hubContext,
        ILogger<NewImportController> logger,
        IWebHostEnvironment environment)
    {
        _importerService = importerService;
        _workOrderImportService = workOrderImportService;
        _context = context;
        _partFilteringService = partFilteringService;
        _hubContext = hubContext;
        _logger = logger;
        _tempUploadPath = Path.Combine(environment.ContentRootPath, "temp", "uploads");
        
        // Ensure temp directory exists
        Directory.CreateDirectory(_tempUploadPath);
    }

    /// <summary>
    /// Phase I2: New import upload endpoint
    /// Route: /admin/import/upload
    /// </summary>
    [HttpPost("admin/import/upload")]
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
    /// Route: /admin/import/start
    /// </summary>
    [HttpPost("admin/import/start")]
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
    /// Route: /admin/import/status
    /// </summary>
    [HttpGet("admin/import/status")]
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
    /// Route: /admin/import/tree
    /// </summary>
    [HttpGet("admin/import/tree")]
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
    /// Phase I4: Final conversion endpoint - Convert WorkOrder entities to database
    /// Route: /admin/import/convert
    /// </summary>
    [HttpPost("admin/import/convert")]
    public async Task<IActionResult> ProcessFinalImport([FromBody] FinalImportRequest request)
    {
        if (string.IsNullOrEmpty(request.SessionId))
        {
            return BadRequest(new { error = "Session ID is required" });
        }

        if (!_importSessions.TryGetValue(request.SessionId, out var session))
        {
            return NotFound(new { error = "Import session not found" });
        }

        if (session.Status != ImportStatus.Completed || session.WorkOrderEntities == null)
        {
            return BadRequest(new { error = "Import not completed or no WorkOrder data available" });
        }

        try
        {
            _logger.LogInformation("Phase I4: Starting final import conversion for session {SessionId}", request.SessionId);

            // Use independent conversion logic - no ImportSelectionService dependency
            var result = await ConvertWorkOrderToDatabaseAsync(
                session.WorkOrderEntities, 
                request.WorkOrderName ?? session.WorkOrderEntities.Name ?? "New Import Work Order");

            if (!result.Success)
            {
                _logger.LogWarning("Phase I4: Conversion failed for session {SessionId}: {Errors}", 
                    request.SessionId, string.Join(", ", result.Errors));
                return BadRequest(new { 
                    error = "Import conversion failed", 
                    details = result.Errors,
                    duplicateInfo = result.DuplicateInfo
                });
            }

            // Mark session as converted
            session.Status = ImportStatus.Converted;
            session.CompletedAt = DateTime.Now;

            _logger.LogInformation("Phase I4: Successfully converted WorkOrder {WorkOrderId} to database for session {SessionId}", 
                result.WorkOrderId, request.SessionId);

            return Ok(new { 
                success = true, 
                workOrderId = result.WorkOrderId,
                message = "Import completed successfully",
                statistics = result.Statistics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase I4: Error during final import conversion for session {SessionId}", request.SessionId);
            return StatusCode(500, new { error = "Failed to complete import conversion" });
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
                Name = "Products",
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
                        Name = "Parts",
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
                            Category = part.Category.ToString(),
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
                        Name = "Subassemblies",
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
                                Category = part.Category.ToString(),
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
                        Name = "Hardware",
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
                Name = "Detached Products",
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
                Name = "Hardware",
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
                Name = "Nest Sheets",
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
                        Category = part.Category.ToString(),
                        Children = new List<Models.Api.TreeItem>()
                    });
                }

                nestSheetsCategory.Children.Add(nestSheetItem);
            }

            response.Items.Add(nestSheetsCategory);
        }
    }

    /// <summary>
    /// Phase I4: Independent duplicate detection logic (copied from ImportSelectionService)
    /// </summary>
    private async Task<SelectionValidationResult> CheckForDuplicateWorkOrderDirect(string workOrderId, string workOrderName)
    {
        var result = new SelectionValidationResult { IsValid = true };

        // Check for duplicate Microvellum ID
        var existingById = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Id == workOrderId);
        var existingByName = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Name == workOrderName);

        if (existingById != null || existingByName != null)
        {
            result.IsValid = false;
            
            // Generate unique suggestions
            var suggestedId = await GenerateUniqueWorkOrderId(workOrderId);
            var suggestedName = await GenerateUniqueWorkOrderName(workOrderName);
            
            result.DuplicateInfo = new DuplicateDetectionResult
            {
                HasDuplicates = true,
                DuplicateWorkOrderId = existingById?.Id,
                DuplicateWorkOrderName = existingByName?.Name,
                ExistingImportDate = existingById?.ImportedDate ?? existingByName?.ImportedDate,
                SuggestedNewId = suggestedId,
                SuggestedNewName = suggestedName
            };

            if (existingById != null)
            {
                result.Errors.Add($"Work order with Microvellum ID '{workOrderId}' already exists (imported as '{existingById.Name}' on {existingById.ImportedDate:yyyy-MM-dd})");
                result.DuplicateInfo.ConflictMessages.Add($"ID conflict: '{workOrderId}' exists");
            }

            if (existingByName != null)
            {
                result.Errors.Add($"Work order with name '{workOrderName}' already exists (Microvellum ID: {existingByName.Id}, imported on {existingByName.ImportedDate:yyyy-MM-dd})");
                result.DuplicateInfo.ConflictMessages.Add($"Name conflict: '{workOrderName}' exists");
            }
        }

        return result;
    }

    /// <summary>
    /// Phase I4: Generate unique WorkOrder ID (copied from ImportSelectionService)
    /// </summary>
    private async Task<string> GenerateUniqueWorkOrderId(string baseId)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var suggestedId = $"{baseId}_{timestamp}";
        
        // Ensure it's truly unique
        var existing = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Id == suggestedId);
        if (existing != null)
        {
            suggestedId = $"{baseId}_{timestamp}_{Guid.NewGuid().ToString("N")[..6]}";
        }
        
        return suggestedId;
    }

    /// <summary>
    /// Phase I4: Generate unique WorkOrder name (copied from ImportSelectionService)
    /// </summary>
    private async Task<string> GenerateUniqueWorkOrderName(string baseName)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var suggestedName = $"{baseName} (Reimported {timestamp})";
        
        // Ensure it's truly unique
        var existing = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Name == suggestedName);
        if (existing != null)
        {
            suggestedName = $"{baseName} (Reimported {timestamp} - {Guid.NewGuid().ToString("N")[..6]})";
        }
        
        return suggestedName;
    }

    /// <summary>
    /// Phase I4: Ensure unique ID across different entity types (copied from ImportSelectionService)
    /// </summary>
    private async Task<string> EnsureUniqueIdAsync(string tableName, string originalId)
    {
        var counter = 1;
        var testId = originalId;
        
        while (await IdExistsInDatabaseAsync(tableName, testId))
        {
            testId = $"{originalId}_{counter}";
            counter++;
        }
        
        return testId;
    }

    /// <summary>
    /// Phase I4: Check if ID exists in database (copied from ImportSelectionService)
    /// </summary>
    private async Task<bool> IdExistsInDatabaseAsync(string tableName, string id)
    {
        return tableName switch
        {
            "Products" => await _context.Products.AnyAsync(p => p.Id == id),
            "Parts" => await _context.Parts.AnyAsync(p => p.Id == id),
            "Subassemblies" => await _context.Subassemblies.AnyAsync(s => s.Id == id),
            "DetachedProducts" => await _context.DetachedProducts.AnyAsync(d => d.Id == id),
            "NestSheets" => await _context.NestSheets.AnyAsync(n => n.Id == id),
            "Hardware" => await _context.Hardware.AnyAsync(h => h.Id == id),
            _ => false
        };
    }

/// <summary>
/// Phase I4: Independent WorkOrder to database conversion (adapted from ImportSelectionService)
/// </summary>
    private async Task<ImportConversionResult> ConvertWorkOrderToDatabaseAsync(
        WorkOrder workOrder, 
        string workOrderName)
    {
        var result = new ImportConversionResult();
        
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            _logger.LogInformation("Phase I4: Starting independent conversion of WorkOrder entities to database: {WorkOrderName}", workOrderName);

            // Check for duplicate work order and automatically handle with unique identifiers
            var duplicateCheck = await CheckForDuplicateWorkOrderDirect(workOrder.Id, workOrderName);
            
            // Always automatically resolve duplicates - no user interaction needed
            if (duplicateCheck.DuplicateInfo?.HasDuplicates == true)
            {
                UpdateWorkOrderForDuplicates(workOrder, workOrderName, duplicateCheck.DuplicateInfo);
            }
            else
            {
                // Just update the name if no duplicates
                workOrder.Name = workOrderName;
            }

            // Set import date
            workOrder.ImportedDate = DateTime.Now;

            // Process all entities in the WorkOrder
            await ProcessWorkOrderEntitiesAsync(workOrder, result);

            // Save to database
            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();
            
            // Commit transaction
            await transaction.CommitAsync();

            result.Success = true;
            result.WorkOrderId = workOrder.Id;
            
            _logger.LogInformation("Phase I4: Successfully saved WorkOrder {WorkOrderId} with {ProductCount} products, {PartCount} parts, {SubassemblyCount} subassemblies, {HardwareCount} hardware items",
                workOrder.Id,
                result.Statistics.ConvertedProducts,
                result.Statistics.ConvertedParts,
                result.Statistics.ConvertedSubassemblies,
                result.Statistics.ConvertedHardware);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase I4: Error converting WorkOrder entities to database. Rolling back transaction.");
            await transaction.RollbackAsync();
            result.Errors.Add($"Import error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Phase I4: Update WorkOrder identifiers for duplicates (copied from ImportSelectionService)
    /// </summary>
    private void UpdateWorkOrderForDuplicates(WorkOrder workOrder, string workOrderName, DuplicateDetectionResult duplicateInfo)
    {
        var originalId = workOrder.Id;
        var originalName = workOrder.Name;
        
        // Update work order identifiers
        workOrder.Id = duplicateInfo.SuggestedNewId;
        workOrder.Name = duplicateInfo.SuggestedNewName;
        
        _logger.LogInformation("Phase I4: Updated WorkOrder identifiers due to duplicates: ID '{OriginalId}' -> '{NewId}', Name '{OriginalName}' -> '{NewName}'",
            originalId, workOrder.Id, originalName, workOrder.Name);

        // Update all related entity IDs that reference the work order
        UpdateRelatedEntityIds(workOrder, originalId, workOrder.Id);
    }

    /// <summary>
    /// Phase I4: Update related entity IDs when WorkOrder ID changes (copied from ImportSelectionService)
    /// </summary>
    private void UpdateRelatedEntityIds(WorkOrder workOrder, string oldWorkOrderId, string newWorkOrderId)
    {
        // Update all Product WorkOrderIds
        foreach (var product in workOrder.Products)
        {
            product.WorkOrderId = newWorkOrderId;
            
            // Update all Part WorkOrderIds through navigation
            foreach (var part in product.Parts)
            {
                // Parts don't have WorkOrderId, they're linked through ProductId
            }
        }

        // Update DetachedProduct WorkOrderIds
        foreach (var detachedProduct in workOrder.DetachedProducts)
        {
            detachedProduct.WorkOrderId = newWorkOrderId;
        }

        // Update Hardware WorkOrderIds
        foreach (var hardware in workOrder.Hardware)
        {
            hardware.WorkOrderId = newWorkOrderId;
        }

        // Update NestSheet WorkOrderIds
        foreach (var nestSheet in workOrder.NestSheets)
        {
            nestSheet.WorkOrderId = newWorkOrderId;
        }
    }

    /// <summary>
    /// Phase I4: Process all entities in the WorkOrder for database persistence (copied from ImportSelectionService)
    /// </summary>
    private async Task ProcessWorkOrderEntitiesAsync(WorkOrder workOrder, ImportConversionResult result)
    {
        // Process Products
        foreach (var product in workOrder.Products)
        {
            // Ensure unique product ID
            product.Id = await EnsureUniqueIdAsync("Products", product.Id);
            
            // Process Parts within Product
            foreach (var part in product.Parts)
            {
                part.Id = await EnsureUniqueIdAsync("Parts", part.Id);
                part.ProductId = product.Id; // Update reference
                
                // Classify part if not already classified
                if (part.Category == 0)
                {
                    part.Category = _partFilteringService.ClassifyPart(part);
                }
            }

            // Process Subassemblies within Product
            foreach (var subassembly in product.Subassemblies)
            {
                subassembly.Id = await EnsureUniqueIdAsync("Subassemblies", subassembly.Id);
                subassembly.ProductId = product.Id; // Update reference
                
                // Process Parts within Subassembly
                foreach (var part in subassembly.Parts)
                {
                    part.Id = await EnsureUniqueIdAsync("Parts", part.Id);
                    part.ProductId = product.Id; // Update reference
                    part.SubassemblyId = subassembly.Id; // Update reference
                    
                    // Classify part if not already classified
                    if (part.Category == 0)
                    {
                        part.Category = _partFilteringService.ClassifyPart(part);
                    }
                }
            }

            // Process Hardware within Product - FIX: Use Guid.NewGuid() for unique IDs
            foreach (var hardware in product.Hardware)
            {
                hardware.Id = Guid.NewGuid().ToString(); // Generate unique GUID instead of using Microvellum ID
                hardware.ProductId = product.Id; // Update reference
                hardware.WorkOrderId = workOrder.Id; // Update reference
            }
            
            result.Statistics.ConvertedProducts++;
        }

        // Process DetachedProducts
        foreach (var detachedProduct in workOrder.DetachedProducts)
        {
            detachedProduct.Id = await EnsureUniqueIdAsync("DetachedProducts", detachedProduct.Id);
            detachedProduct.WorkOrderId = workOrder.Id; // Update reference
            
            result.Statistics.ConvertedDetachedProducts++;
        }

        // Process Hardware (standalone) - FIX: Use Guid.NewGuid() for unique IDs
        foreach (var hardware in workOrder.Hardware)
        {
            hardware.Id = Guid.NewGuid().ToString(); // Generate unique GUID instead of using Microvellum ID
            hardware.WorkOrderId = workOrder.Id; // Update reference
            result.Statistics.ConvertedHardware++;
        }

        // Process NestSheets
        foreach (var nestSheet in workOrder.NestSheets)
        {
            nestSheet.Id = await EnsureUniqueIdAsync("NestSheets", nestSheet.Id);
            nestSheet.WorkOrderId = workOrder.Id; // Update reference
            
            // Process Parts within NestSheet
            foreach (var part in nestSheet.Parts)
            {
                part.Id = await EnsureUniqueIdAsync("Parts", part.Id);
                part.NestSheetId = nestSheet.Id; // Update reference
                
                // Classify part if not already classified
                if (part.Category == 0)
                {
                    part.Category = _partFilteringService.ClassifyPart(part);
                }
            }
            
            result.Statistics.ConvertedNestSheets++;
        }

        // Count total parts and subassemblies
        result.Statistics.ConvertedParts = workOrder.Products.SelectMany(p => p.Parts).Count() +
                                          workOrder.Products.SelectMany(p => p.Subassemblies).SelectMany(s => s.Parts).Count() +
                                          workOrder.NestSheets.SelectMany(n => n.Parts).Count();
        
        result.Statistics.ConvertedSubassemblies = workOrder.Products.SelectMany(p => p.Subassemblies).Count();
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

/// <summary>
/// Phase I4: Selection validation result (copied from ImportSelectionService)
/// </summary>
public class SelectionValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public DuplicateDetectionResult? DuplicateInfo { get; set; }
}