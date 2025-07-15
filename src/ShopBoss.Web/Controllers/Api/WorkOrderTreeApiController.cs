using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Models;
using ShopBoss.Web.Models.Api;
using ShopBoss.Web.Services;

namespace ShopBoss.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class WorkOrderTreeApiController : ControllerBase
{
    private readonly WorkOrderService _workOrderService;
    private readonly ILogger<WorkOrderTreeApiController> _logger;

    public WorkOrderTreeApiController(WorkOrderService workOrderService, ILogger<WorkOrderTreeApiController> logger)
    {
        _workOrderService = workOrderService;
        _logger = logger;
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
                    Name = $"Products ({workOrderData.ProductNodes.Count})",
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
                        Status = includeStatus ? productNode.EffectiveStatus.ToString() : null,
                        Children = new List<TreeItem>()
                    };

                    // Add subcategories under each product (only if they have items)
                    
                    // Parts subcategory
                    if (productNode.Parts.Any())
                    {
                        var partsCategory = new TreeItem
                        {
                            Id = $"category_parts_{productNode.Product.Id}",
                            Name = $"Parts ({productNode.Parts.Count})",
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
                            Name = $"Subassemblies ({productNode.Subassemblies.Count})",
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
                                Status = includeStatus ? CalculateSubassemblyStatus(subassembly.Parts).ToString() : null,
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
                            Name = $"Hardware ({productNode.Hardware.Count})",
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
                    Name = $"Detached Products ({workOrderData.DetachedProducts.Count})",
                    Type = "category",
                    Quantity = workOrderData.DetachedProducts.Count,
                    Status = null,
                    Children = new List<TreeItem>()
                };

                foreach (var detachedProduct in workOrderData.DetachedProducts)
                {
                    detachedProductsCategory.Children.Add(new TreeItem
                    {
                        Id = detachedProduct.Id,
                        Name = detachedProduct.Name,
                        Type = "detached_product",
                        Quantity = detachedProduct.Qty,
                        Status = includeStatus ? (detachedProduct.Status == PartStatus.Shipped ? "Shipped" : "Pending") : null,
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
                    Name = $"Nest Sheets ({workOrderData.NestSheets.Count})",
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
                        Status = includeStatus ? (nestSheet.StatusString == "Processed" ? "Processed" : "Pending") : null,
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

    private Models.PartStatus CalculateSubassemblyStatus(List<Models.Part> parts)
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

}