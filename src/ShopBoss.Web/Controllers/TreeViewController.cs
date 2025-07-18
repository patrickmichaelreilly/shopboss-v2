using Microsoft.AspNetCore.Mvc;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Controllers
{
    /// <summary>
    /// Controller for rendering tree view partials
    /// Phase I1: Provides reusable tree partial rendering for universal use
    /// </summary>
    public class TreeViewController : Controller
    {
        /// <summary>
        /// Render the WorkOrderTreeView partial with specified configuration
        /// </summary>
        /// <param name="options">Tree view configuration options</param>
        /// <returns>Partial view result</returns>
        public IActionResult RenderTreeView(WorkOrderTreeViewOptions options)
        {
            if (options == null)
            {
                return BadRequest("Tree view options are required");
            }

            // Validate required parameters based on mode
            if (options.Mode == "modify" && string.IsNullOrEmpty(options.WorkOrderId))
            {
                return BadRequest("WorkOrderId is required for modify mode");
            }

            if (options.Mode == "import" && string.IsNullOrEmpty(options.SessionId))
            {
                return BadRequest("SessionId is required for import mode");
            }

            return PartialView("_WorkOrderTreeView", options);
        }

        /// <summary>
        /// Render tree view for modify mode
        /// </summary>
        /// <param name="workOrderId">Work order ID</param>
        /// <param name="containerId">Container HTML ID</param>
        /// <param name="showAuditHistory">Whether to show audit history</param>
        /// <returns>Partial view result</returns>
        [HttpGet]
        public IActionResult ForModify(string workOrderId, string containerId = "workOrderTree", bool showAuditHistory = false)
        {
            var options = WorkOrderTreeViewOptions.ForModify(workOrderId, containerId, showAuditHistory);
            return RenderTreeView(options);
        }

        /// <summary>
        /// Render tree view for import mode
        /// </summary>
        /// <param name="sessionId">Import session ID</param>
        /// <param name="containerId">Container HTML ID</param>
        /// <param name="showInstructions">Whether to show instructions</param>
        /// <returns>Partial view result</returns>
        [HttpGet]
        public IActionResult ForImport(string sessionId, string containerId = "importTree", bool showInstructions = true)
        {
            var options = WorkOrderTreeViewOptions.ForImport(sessionId, containerId, showInstructions);
            return RenderTreeView(options);
        }
    }
}