using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Services;
using ShopBoss.Web.Models.Api;

namespace ShopBoss.Web.Controllers.Api;

[ApiController]
[Route("api/workorder")]
public class WorkOrderApiController : ControllerBase
{
    private readonly WorkOrderService _workOrderService;
    private readonly ILogger<WorkOrderApiController> _logger;

    public WorkOrderApiController(WorkOrderService workOrderService, ILogger<WorkOrderApiController> logger)
    {
        _workOrderService = workOrderService;
        _logger = logger;
    }

    [HttpGet("{workOrderId}/tree")]
    public async Task<IActionResult> GetTreeData(string workOrderId, int page = 0, int size = 100)
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                return BadRequest(new { success = false, message = "Work order ID is required" });
            }

            var workOrderData = await _workOrderService.GetWorkOrderManagementDataAsync(workOrderId);
            
            if (workOrderData.WorkOrder == null)
            {
                return NotFound(new { success = false, message = "Work order not found" });
            }

            // Apply pagination to product nodes if requested
            var productNodes = workOrderData.ProductNodes;
            if (page > 0 || size < productNodes.Count)
            {
                productNodes = productNodes
                    .Skip(page * size)
                    .Take(size)
                    .ToList();
            }

            var response = new TreeDataResponse
            {
                Success = true,
                Data = new TreeData
                {
                    WorkOrder = new WorkOrderTreeNode
                    {
                        Id = workOrderData.WorkOrder.Id,
                        Name = workOrderData.WorkOrder.Name,
                        ImportedDate = workOrderData.WorkOrder.ImportedDate,
                        Hardware = workOrderData.WorkOrder.Hardware?.Select(h => new HardwareTreeNode
                        {
                            Id = h.Id,
                            Name = h.Name,
                            Qty = h.Qty,
                            IsShipped = h.IsShipped
                        }).ToList() ?? new List<HardwareTreeNode>(),
                        DetachedProducts = workOrderData.WorkOrder.DetachedProducts?.Select(d => new DetachedProductTreeNode
                        {
                            Id = d.Id,
                            Name = d.Name,
                            ProductNumber = d.ProductNumber,
                            Qty = d.Qty,
                            Length = (double)(d.Length ?? 0),
                            Width = (double)(d.Width ?? 0),
                            Thickness = (double)(d.Thickness ?? 0),
                            IsShipped = d.IsShipped
                        }).ToList() ?? new List<DetachedProductTreeNode>()
                    },
                    ProductNodes = productNodes.Select(pn => new ProductTreeNode
                    {
                        Product = new ProductInfo
                        {
                            Id = pn.Product.Id,
                            Name = pn.Product.Name,
                            ProductNumber = pn.Product.ProductNumber,
                            Qty = pn.Product.Qty
                        },
                        Parts = pn.Parts.Select(p => new PartTreeNode
                        {
                            Id = p.Id,
                            Name = p.Name,
                            Qty = p.Qty,
                            Length = (double)(p.Length ?? 0),
                            Width = (double)(p.Width ?? 0),
                            Thickness = (double)(p.Thickness ?? 0),
                            Material = p.Material,
                            Status = p.Status.ToString()
                        }).ToList(),
                        Subassemblies = pn.Subassemblies.Select(s => new SubassemblyTreeNode
                        {
                            Id = s.Id,
                            Name = s.Name,
                            Qty = s.Qty,
                            Parts = s.Parts.Select(p => new PartTreeNode
                            {
                                Id = p.Id,
                                Name = p.Name,
                                Qty = p.Qty,
                                Length = (double)(p.Length ?? 0),
                                Width = (double)(p.Width ?? 0),
                                Thickness = (double)(p.Thickness ?? 0),
                                Material = p.Material,
                                Status = p.Status.ToString()
                            }).ToList()
                        }).ToList(),
                        EffectiveStatus = pn.EffectiveStatus.ToString()
                    }).ToList(),
                    NestSheetSummary = new NestSheetSummaryInfo
                    {
                        TotalNestSheets = workOrderData.NestSheetSummary.TotalNestSheets,
                        ProcessedNestSheets = workOrderData.NestSheetSummary.ProcessedNestSheets,
                        PendingNestSheets = workOrderData.NestSheetSummary.PendingNestSheets,
                        TotalPartsOnNestSheets = workOrderData.NestSheetSummary.TotalPartsOnNestSheets
                    }
                },
                Pagination = new PaginationInfo
                {
                    Page = page,
                    Size = size,
                    TotalItems = workOrderData.ProductNodes.Count,
                    TotalPages = (int)Math.Ceiling((double)workOrderData.ProductNodes.Count / size),
                    HasNextPage = (page + 1) * size < workOrderData.ProductNodes.Count,
                    HasPreviousPage = page > 0
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tree data for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpGet("{workOrderId}/products/{productId}/details")]
    public async Task<IActionResult> GetProductDetails(string workOrderId, string productId)
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId) || string.IsNullOrEmpty(productId))
            {
                return BadRequest(new { success = false, message = "Work order ID and product ID are required" });
            }

            // For now, use the main service method and filter to the specific product
            // In future iterations, we could optimize this with a dedicated method
            var workOrderData = await _workOrderService.GetWorkOrderManagementDataAsync(workOrderId);
            
            if (workOrderData.WorkOrder == null)
            {
                return NotFound(new { success = false, message = "Work order not found" });
            }

            var productNode = workOrderData.ProductNodes.FirstOrDefault(pn => pn.Product.Id == productId);
            if (productNode == null)
            {
                return NotFound(new { success = false, message = "Product not found" });
            }

            var productDetails = new ProductDetailsResponse
            {
                Success = true,
                Product = new ProductTreeNode
                {
                    Product = new ProductInfo
                    {
                        Id = productNode.Product.Id,
                        Name = productNode.Product.Name,
                        ProductNumber = productNode.Product.ProductNumber,
                        Qty = productNode.Product.Qty
                    },
                    Parts = productNode.Parts.Select(p => new PartTreeNode
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Qty = p.Qty,
                        Length = (double)(p.Length ?? 0),
                        Width = (double)(p.Width ?? 0),
                        Thickness = (double)(p.Thickness ?? 0),
                        Material = p.Material,
                        Status = p.Status.ToString()
                    }).ToList(),
                    Subassemblies = productNode.Subassemblies.Select(s => new SubassemblyTreeNode
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Qty = s.Qty,
                        Parts = s.Parts.Select(p => new PartTreeNode
                        {
                            Id = p.Id,
                            Name = p.Name,
                            Qty = p.Qty,
                            Length = (double)(p.Length ?? 0),
                            Width = (double)(p.Width ?? 0),
                            Thickness = (double)(p.Thickness ?? 0),
                            Material = p.Material,
                            Status = p.Status.ToString()
                        }).ToList()
                    }).ToList(),
                    EffectiveStatus = productNode.EffectiveStatus.ToString()
                }
            };

            return Ok(productDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product details for work order {WorkOrderId}, product {ProductId}", workOrderId, productId);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    [HttpGet("{workOrderId}/summary")]
    public async Task<IActionResult> GetWorkOrderSummary(string workOrderId)
    {
        try
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                return BadRequest(new { success = false, message = "Work order ID is required" });
            }

            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(workOrderId);
            
            if (workOrder == null)
            {
                return NotFound(new { success = false, message = "Work order not found" });
            }

            var summary = new WorkOrderSummaryResponse
            {
                Success = true,
                WorkOrder = new WorkOrderSummary
                {
                    Id = workOrder.Id,
                    Name = workOrder.Name,
                    ImportedDate = workOrder.ImportedDate
                }
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order summary for {WorkOrderId}", workOrderId);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }
}