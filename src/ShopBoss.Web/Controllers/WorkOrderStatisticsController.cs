using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Services;

namespace ShopBoss.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkOrderStatisticsController : ControllerBase
    {
        private readonly WorkOrderService _workOrderService;
        private readonly ShopBossDbContext _context;
        private readonly ILogger<WorkOrderStatisticsController> _logger;

        public WorkOrderStatisticsController(
            WorkOrderService workOrderService,
            ShopBossDbContext context,
            ILogger<WorkOrderStatisticsController> logger)
        {
            _workOrderService = workOrderService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get comprehensive work order statistics for the ModifyWorkOrder interface
        /// </summary>
        /// <param name="workOrderId">The work order ID</param>
        /// <returns>Work order data with all statistics</returns>
        [HttpGet("{workOrderId}")]
        public async Task<IActionResult> GetWorkOrderStatistics(string workOrderId)
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

                // Calculate statistics for the UI cards
                var statistics = CalculateStatistics(workOrderData);

                return Ok(new
                {
                    success = true,
                    workOrder = new
                    {
                        id = workOrderData.WorkOrder.Id,
                        name = workOrderData.WorkOrder.Name,
                        importDate = workOrderData.WorkOrder.ImportedDate
                    },
                    statistics = statistics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting work order statistics for {WorkOrderId}", workOrderId);
                return StatusCode(500, new { success = false, message = "An error occurred while loading statistics" });
            }
        }

        /// <summary>
        /// Calculate statistics from work order management data using proper shipping logic
        /// </summary>
        private object CalculateStatistics(WorkOrderManagementData data)
        {
            var productsStats = new Dictionary<string, int> { ["total"] = 0, ["pending"] = 0, ["cut"] = 0, ["sorted"] = 0, ["assembled"] = 0, ["shipped"] = 0 };
            var partsStats = new Dictionary<string, int> { ["total"] = 0, ["pending"] = 0, ["cut"] = 0, ["sorted"] = 0, ["assembled"] = 0, ["shipped"] = 0 };
            var hardwareStats = new Dictionary<string, int> { ["total"] = 0, ["pending"] = 0, ["sorted"] = 0, ["assembled"] = 0, ["shipped"] = 0 };
            var detachedProductsStats = new Dictionary<string, int> { ["total"] = 0, ["pending"] = 0, ["cut"] = 0, ["sorted"] = 0, ["assembled"] = 0, ["shipped"] = 0 };
            var nestSheetsStats = new Dictionary<string, int> { ["total"] = 0, ["pending"] = 0, ["cut"] = 0, ["sorted"] = 0, ["assembled"] = 0, ["shipped"] = 0 };

            // Count products (shipped = all parts are shipped)
            foreach (var productNode in data.ProductNodes)
            {
                productsStats["total"]++;
                
                // Product is shipped if ALL its parts are shipped
                var allParts = new List<Models.Part>();
                allParts.AddRange(productNode.Parts);
                foreach (var subassembly in productNode.Subassemblies)
                {
                    allParts.AddRange(subassembly.Parts);
                }
                
                if (allParts.Any() && allParts.All(p => p.Status == Models.PartStatus.Shipped))
                {
                    productsStats["shipped"]++;
                }
                else
                {
                    // Use the product's own status for non-shipped products
                    IncrementStatusCount(productsStats, productNode.Product.Status.ToString());
                }

                // Count individual parts
                foreach (var part in allParts)
                {
                    partsStats["total"]++;
                    IncrementStatusCount(partsStats, part.Status.ToString());
                }
            }

            // Count detached products (use IsShipped flag + PartStatus)
            foreach (var detachedProduct in data.DetachedProducts)
            {
                detachedProductsStats["total"]++;
                if (detachedProduct.Status == PartStatus.Shipped)
                {
                    detachedProductsStats["shipped"]++;
                }
                else
                {
                    IncrementStatusCount(detachedProductsStats, detachedProduct.Status.ToString());
                }
            }

            // Count hardware (use IsShipped flag + PartStatus)
            if (data.WorkOrder?.Hardware != null)
            {
                foreach (var hardware in data.WorkOrder.Hardware)
                {
                    hardwareStats["total"]++;
                    if (hardware.Status == PartStatus.Shipped)
                    {
                        hardwareStats["shipped"]++;
                    }
                    else
                    {
                        IncrementStatusCount(hardwareStats, hardware.Status.ToString());
                    }
                }
            }

            // Count nest sheets
            foreach (var nestSheet in data.NestSheets)
            {
                nestSheetsStats["total"]++;
                IncrementStatusCount(nestSheetsStats, nestSheet.Status.ToString());
            }

            return new
            {
                products = productsStats,
                parts = partsStats,
                hardware = hardwareStats,
                detachedProducts = detachedProductsStats,
                nestSheets = nestSheetsStats
            };
        }

        /// <summary>
        /// Get audit history for a specific work order
        /// </summary>
        /// <param name="workOrderId">The work order ID</param>
        /// <returns>Audit trail entries for the work order</returns>
        [HttpGet("{workOrderId}/audit")]
        public async Task<IActionResult> GetWorkOrderAuditHistory(string workOrderId)
        {
            try
            {
                if (string.IsNullOrEmpty(workOrderId))
                {
                    return BadRequest(new { success = false, message = "Work order ID is required" });
                }

                // Get all audit logs for this work order, including both work order level and entity level changes
                var auditEntries = await _context.AuditLogs
                    .Where(a => a.WorkOrderId == workOrderId)
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();

                var formattedEntries = auditEntries.Select(entry => new
                {
                    id = entry.Id,
                    timestamp = entry.Timestamp,
                    action = entry.Action,
                    entityType = entry.EntityType,
                    entityId = entry.EntityId,
                    oldValue = entry.OldValue,
                    newValue = entry.NewValue,
                    station = entry.Station ?? "System",
                    details = entry.Details ?? "",
                    userId = entry.UserId
                }).ToList();

                return Ok(new { success = true, auditEntries = formattedEntries });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit history for work order {WorkOrderId}", workOrderId);
                return StatusCode(500, new { success = false, message = "An error occurred while loading audit history" });
            }
        }

        private void IncrementStatusCount(Dictionary<string, int> statusDict, string status)
        {
            var statusKey = status?.ToLower();
            if (statusKey != null && statusDict.ContainsKey(statusKey))
            {
                statusDict[statusKey]++;
            }
        }

        private void IncrementHardwareStatusCount(Dictionary<string, int> statusDict, string status)
        {
            var statusKey = status?.ToLower();
            if (statusKey != null && statusDict.ContainsKey(statusKey))
            {
                statusDict[statusKey]++;
            }
        }

    }
}