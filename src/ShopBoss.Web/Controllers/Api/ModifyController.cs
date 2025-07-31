using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Models.Api;
using ShopBoss.Web.Services;
using System.Text.Json;

namespace ShopBoss.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ModifyController : ControllerBase
{
    private readonly WorkOrderService _workOrderService;
    private readonly ILogger<ModifyController> _logger;
    private readonly ShopBossDbContext _context;
    private readonly AuditTrailService _auditTrailService;
    private readonly WorkOrderDeletionService _deletionService;

    public ModifyController(WorkOrderService workOrderService, ILogger<ModifyController> logger, ShopBossDbContext context, AuditTrailService auditTrailService, WorkOrderDeletionService deletionService)
    {
        _workOrderService = workOrderService;
        _logger = logger;
        _context = context;
        _auditTrailService = auditTrailService;
        _deletionService = deletionService;
    }

    [HttpGet("{workOrderId}")]
    public async Task<ActionResult<TreeDataResponse>> GetTreeData(string workOrderId, [FromQuery] bool includeStatus = false)
    {
        try
        {
            var workOrderData = await _workOrderService.GetWorkOrderManagementDataAsync(workOrderId);
            
            if (workOrderData.WorkOrder == null)
            {
                return NotFound($"Work order '{workOrderId}' not found.");
            }

            var response = new TreeDataResponse
            {
                WorkOrderId = workOrderData.WorkOrder.Id,
                WorkOrderName = workOrderData.WorkOrder.Name,
                Items = new List<TreeItem>()
            };

            // Create fixed top-level categories: Products, DetachedProducts, and Nest Sheets
            
            // 1. Products Category
            if (workOrderData.ProductNodes.Any())
            {
                var productsCategory = new TreeItem
                {
                    Id = "category_products",
                    Name = "Products",
                    Type = "category",
                    Quantity = workOrderData.ProductNodes.Count,
                    Status = null,
                    Children = new List<TreeItem>()
                };

                foreach (var productNode in workOrderData.ProductNodes)
                {
                    var productItem = new TreeItem
                    {
                        Id = productNode.Product.Id,
                        Name = productNode.Product.Name,
                        Type = "product",
                        Quantity = productNode.Product.Qty,
                        ItemNumber = productNode.Product.ItemNumber,
                        Status = includeStatus ? await CalculateProductStatus(productNode.Product.Id) : null,
                        Children = new List<TreeItem>()
                    };

                    // Add subcategories under each product (only if they have items)
                    
                    // Parts subcategory
                    if (productNode.Parts.Any())
                    {
                        var partsCategory = new TreeItem
                        {
                            Id = $"category_parts_{productNode.Product.Id}",
                            Name = "Parts",
                            Type = "category",
                            Quantity = productNode.Parts.Count,
                            Status = null,
                            Children = new List<TreeItem>()
                        };

                        foreach (var part in productNode.Parts)
                        {
                            partsCategory.Children.Add(new TreeItem
                            {
                                Id = part.Id,
                                Name = part.Name,
                                Type = "part",
                                Quantity = part.Qty,
                                Status = includeStatus ? part.Status.ToString() : null,
                                Category = part.Category.ToString(),
                                Children = new List<TreeItem>()
                            });
                        }

                        productItem.Children.Add(partsCategory);
                    }

                    // Subassemblies subcategory
                    if (productNode.Subassemblies.Any())
                    {
                        var subassembliesCategory = new TreeItem
                        {
                            Id = $"category_subassemblies_{productNode.Product.Id}",
                            Name = "Subassemblies",
                            Type = "category",
                            Quantity = productNode.Subassemblies.Count,
                            Status = null,
                            Children = new List<TreeItem>()
                        };

                        foreach (var subassembly in productNode.Subassemblies)
                        {
                            var subassemblyItem = new TreeItem
                            {
                                Id = subassembly.Id,
                                Name = subassembly.Name,
                                Type = "subassembly",
                                Quantity = subassembly.Qty,
                                Status = includeStatus ? CalculateEffectiveStatus(subassembly.Parts).ToString() : null,
                                Children = new List<TreeItem>()
                            };

                            // Add parts under subassembly
                            foreach (var part in subassembly.Parts)
                            {
                                subassemblyItem.Children.Add(new TreeItem
                                {
                                    Id = part.Id,
                                    Name = part.Name,
                                    Type = "part",
                                    Quantity = part.Qty,
                                    Status = includeStatus ? part.Status.ToString() : null,
                                    Category = part.Category.ToString(),
                                    Children = new List<TreeItem>()
                                });
                            }

                            subassembliesCategory.Children.Add(subassemblyItem);
                        }

                        productItem.Children.Add(subassembliesCategory);
                    }

                    // Hardware subcategory
                    if (productNode.Hardware.Any())
                    {
                        var hardwareCategory = new TreeItem
                        {
                            Id = $"category_hardware_{productNode.Product.Id}",
                            Name = "Hardware",
                            Type = "category",
                            Quantity = productNode.Hardware.Count,
                            Status = null,
                            Children = new List<TreeItem>()
                        };

                        foreach (var hardware in productNode.Hardware)
                        {
                            hardwareCategory.Children.Add(new TreeItem
                            {
                                Id = hardware.Id,
                                Name = hardware.Name,
                                Type = "hardware",
                                Quantity = hardware.Qty,
                                Status = includeStatus ? hardware.Status.ToString() : null,
                                Children = new List<TreeItem>()
                            });
                        }

                        productItem.Children.Add(hardwareCategory);
                    }

                    productsCategory.Children.Add(productItem);
                }

                response.Items.Add(productsCategory);
            }

            // 2. DetachedProducts Category
            if (workOrderData.DetachedProducts.Any())
            {
                var detachedProductsCategory = new TreeItem
                {
                    Id = "category_detached_products",
                    Name = "Detached Products",
                    Type = "category",
                    Quantity = workOrderData.DetachedProducts.Count,
                    Status = null,
                    Children = new List<TreeItem>()
                };

                foreach (var detachedProduct in workOrderData.DetachedProducts)
                {
                    string? statusDisplay = null;
                    if (includeStatus)
                    {
                        // Calculate EffectiveStatus from DetachedProduct's Parts (same as regular Products)
                        var detachedProductParts = await _context.Parts
                            .Where(p => p.ProductId == detachedProduct.Id)
                            .ToListAsync();
                        
                        var effectiveStatus = CalculateEffectiveStatus(detachedProductParts);
                        statusDisplay = effectiveStatus.ToString();
                    }

                    detachedProductsCategory.Children.Add(new TreeItem
                    {
                        Id = detachedProduct.Id,
                        Name = detachedProduct.Name,
                        Type = "detached_product",
                        Quantity = detachedProduct.Qty,
                        ItemNumber = detachedProduct.ItemNumber,
                        Status = statusDisplay,
                        Children = new List<TreeItem>()
                    });
                }

                response.Items.Add(detachedProductsCategory);
            }

            // 3. Nest Sheets Category
            if (workOrderData.NestSheets.Any())
            {
                var nestSheetsCategory = new TreeItem
                {
                    Id = "category_nestsheets",
                    Name = "Nest Sheets",
                    Type = "category",
                    Quantity = workOrderData.NestSheets.Count,
                    Status = null,
                    Children = new List<TreeItem>()
                };

                foreach (var nestSheet in workOrderData.NestSheets)
                {
                    var nestSheetItem = new TreeItem
                    {
                        Id = nestSheet.Id,
                        Name = nestSheet.Name,
                        Type = "nestsheet",
                        Quantity = 1,
                        Status = includeStatus ? nestSheet.Status.ToString() : null,
                        Children = new List<TreeItem>()
                    };

                    foreach (var part in nestSheet.Parts)
                    {
                        nestSheetItem.Children.Add(new TreeItem
                        {
                            Id = part.Id,
                            Name = part.Name,
                            Type = "part",
                            Quantity = part.Qty,
                            Status = includeStatus ? part.Status.ToString() : null,
                            Category = part.Category.ToString(),
                            Children = new List<TreeItem>()
                        });
                    }

                    nestSheetsCategory.Children.Add(nestSheetItem);
                }

                response.Items.Add(nestSheetsCategory);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tree data for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while retrieving tree data.");
        }
    }

    private Models.PartStatus CalculateEffectiveStatus(List<Models.Part> parts)
    {
        if (!parts.Any()) return Models.PartStatus.Pending;

        // If all parts have the same status, return that status
        var distinctStatuses = parts.Select(p => p.Status).Distinct().ToList();
        if (distinctStatuses.Count == 1)
        {
            return distinctStatuses.First();
        }

        // Return the "lowest" status if mixed
        if (parts.Any(p => p.Status == Models.PartStatus.Pending)) return Models.PartStatus.Pending;
        if (parts.Any(p => p.Status == Models.PartStatus.Cut)) return Models.PartStatus.Cut;
        if (parts.Any(p => p.Status == Models.PartStatus.Sorted)) return Models.PartStatus.Sorted;
        if (parts.Any(p => p.Status == Models.PartStatus.Assembled)) return Models.PartStatus.Assembled;
        return Models.PartStatus.Shipped;
    }

    private async Task<string> CalculateProductStatus(string productId)
    {
        // Calculate EffectiveStatus from Product's Parts (direct + subassembly parts)
        // Following successful WorkOrderService logic pattern
        var productParts = await _context.Parts
            .Where(p => p.ProductId == productId)
            .ToListAsync();
        
        var subassemblyParts = await _context.Parts
            .Where(p => p.Subassembly != null && p.Subassembly.ProductId == productId)
            .ToListAsync();
        
        // Combine all parts like WorkOrderService does
        var allProductParts = productParts.ToList();
        allProductParts.AddRange(subassemblyParts);
        
        var effectiveStatus = CalculateEffectiveStatus(allProductParts);
        return effectiveStatus.ToString();
    }

    [HttpPost("updateCategory")]
    public async Task<IActionResult> UpdatePartCategory([FromForm] string partId, [FromForm] string category, [FromForm] string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(partId) || string.IsNullOrEmpty(category))
            {
                return BadRequest(new { success = false, message = "PartId and category are required" });
            }

            var part = await _context.Parts.FindAsync(partId);
            if (part == null)
            {
                return NotFound(new { success = false, message = $"Part '{partId}' not found." });
            }

            // Parse the category string to enum
            if (!Enum.TryParse<PartCategory>(category, out var categoryEnum))
            {
                return BadRequest(new { success = false, message = $"Invalid category value: {category}" });
            }

            // Store old values for audit trail
            var oldCategory = part.Category;
            var oldValue = new { Category = oldCategory.ToString() };
            var newValue = new { Category = categoryEnum.ToString() };

            // Update the part
            part.Category = categoryEnum;
            part.StatusUpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // Log the category change to audit trail
            await _auditTrailService.LogAsync(
                action: "ManualCategoryChange",
                entityType: "Part",
                entityId: partId,
                oldValue: oldValue,
                newValue: newValue,
                station: "Manual",
                workOrderId: workOrderId,
                details: $"Manual category change via Admin interface. Category: {oldCategory} → {categoryEnum}",
                sessionId: HttpContext.Session.Id
            );

            return Ok(new { success = true, message = "Category updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating part category for {PartId}", partId);
            return StatusCode(500, new { success = false, message = "Error updating category" });
        }
    }

    [HttpDelete("part/{partId}")]
    public async Task<IActionResult> DeletePart(string partId, [FromQuery] string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(partId) || string.IsNullOrEmpty(workOrderId))
            {
                return BadRequest(new { success = false, message = "PartId and workOrderId are required" });
            }

            var result = await _deletionService.DeletePartAsync(partId, workOrderId, "Admin Interface");
            
            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message, itemsDeleted = result.ItemsDeleted });
            }
            else
            {
                return BadRequest(new { success = false, message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeletePart endpoint for {PartId}", partId);
            return StatusCode(500, new { success = false, message = "Error deleting part" });
        }
    }

    [HttpDelete("hardware/{hardwareId}")]
    public async Task<IActionResult> DeleteHardware(string hardwareId, [FromQuery] string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(hardwareId) || string.IsNullOrEmpty(workOrderId))
            {
                return BadRequest(new { success = false, message = "HardwareId and workOrderId are required" });
            }

            var result = await _deletionService.DeleteHardwareAsync(hardwareId, workOrderId, "Admin Interface");
            
            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message, itemsDeleted = result.ItemsDeleted });
            }
            else
            {
                return BadRequest(new { success = false, message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteHardware endpoint for {HardwareId}", hardwareId);
            return StatusCode(500, new { success = false, message = "Error deleting hardware" });
        }
    }

    [HttpDelete("subassembly/{subassemblyId}")]
    public async Task<IActionResult> DeleteSubassembly(string subassemblyId, [FromQuery] string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(subassemblyId) || string.IsNullOrEmpty(workOrderId))
            {
                return BadRequest(new { success = false, message = "SubassemblyId and workOrderId are required" });
            }

            var result = await _deletionService.DeleteSubassemblyAsync(subassemblyId, workOrderId, "Admin Interface");
            
            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message, itemsDeleted = result.ItemsDeleted });
            }
            else
            {
                return BadRequest(new { success = false, message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteSubassembly endpoint for {SubassemblyId}", subassemblyId);
            return StatusCode(500, new { success = false, message = "Error deleting subassembly" });
        }
    }

    [HttpDelete("product/{productId}")]
    public async Task<IActionResult> DeleteProduct(string productId, [FromQuery] string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(workOrderId))
            {
                return BadRequest(new { success = false, message = "ProductId and workOrderId are required" });
            }

            var result = await _deletionService.DeleteProductAsync(productId, workOrderId, "Admin Interface");
            
            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message, itemsDeleted = result.ItemsDeleted });
            }
            else
            {
                return BadRequest(new { success = false, message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteProduct endpoint for {ProductId}", productId);
            return StatusCode(500, new { success = false, message = "Error deleting product" });
        }
    }

    [HttpDelete("detached-product/{detachedProductId}")]
    public async Task<IActionResult> DeleteDetachedProduct(string detachedProductId, [FromQuery] string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(detachedProductId) || string.IsNullOrEmpty(workOrderId))
            {
                return BadRequest(new { success = false, message = "DetachedProductId and workOrderId are required" });
            }

            var result = await _deletionService.DeleteDetachedProductAsync(detachedProductId, workOrderId, "Admin Interface");
            
            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message, itemsDeleted = result.ItemsDeleted });
            }
            else
            {
                return BadRequest(new { success = false, message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteDetachedProduct endpoint for {DetachedProductId}", detachedProductId);
            return StatusCode(500, new { success = false, message = "Error deleting detached product" });
        }
    }

    [HttpDelete("nestsheet/{nestSheetId}")]
    public async Task<IActionResult> DeleteNestSheet(string nestSheetId, [FromQuery] string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(nestSheetId) || string.IsNullOrEmpty(workOrderId))
            {
                return BadRequest(new { success = false, message = "NestSheetId and workOrderId are required" });
            }

            var result = await _deletionService.DeleteNestSheetAsync(nestSheetId, workOrderId, "Admin Interface");
            
            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message, itemsDeleted = result.ItemsDeleted });
            }
            else
            {
                return BadRequest(new { success = false, message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteNestSheet endpoint for {NestSheetId}", nestSheetId);
            return StatusCode(500, new { success = false, message = "Error deleting nest sheet" });
        }
    }

}