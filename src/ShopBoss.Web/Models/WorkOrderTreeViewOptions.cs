namespace ShopBoss.Web.Models
{
    /// <summary>
    /// Configuration options for the reusable WorkOrderTreeView partial
    /// Phase I1: Parameter-driven tree component for universal reuse
    /// </summary>
    public class WorkOrderTreeViewOptions
    {
        /// <summary>
        /// HTML ID for the tree container element
        /// </summary>
        public string ContainerId { get; set; } = "workOrderTree";

        /// <summary>
        /// Mode of operation: "modify" or "import"
        /// </summary>
        public string Mode { get; set; } = "modify";

        /// <summary>
        /// API URL for tree data operations
        /// </summary>
        public string ApiUrl { get; set; } = "/api/WorkOrderTreeApi";

        /// <summary>
        /// Work Order ID (for modify mode)
        /// </summary>
        public string? WorkOrderId { get; set; }

        /// <summary>
        /// Session ID (for import mode)
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Whether to show instructional text
        /// </summary>
        public bool ShowInstructions { get; set; } = true;

        /// <summary>
        /// Whether to show audit history panel
        /// </summary>
        public bool ShowAuditHistory { get; set; } = false;

        /// <summary>
        /// Create options for modify mode
        /// </summary>
        public static WorkOrderTreeViewOptions ForModify(string workOrderId, string containerId = "workOrderTree", bool showAuditHistory = false)
        {
            return new WorkOrderTreeViewOptions
            {
                ContainerId = containerId,
                Mode = "modify",
                ApiUrl = "/api/WorkOrderTreeApi",
                WorkOrderId = workOrderId,
                ShowAuditHistory = showAuditHistory
            };
        }

        /// <summary>
        /// Create options for import mode
        /// </summary>
        public static WorkOrderTreeViewOptions ForImport(string sessionId, string containerId = "importTree", bool showInstructions = true)
        {
            return new WorkOrderTreeViewOptions
            {
                ContainerId = containerId,
                Mode = "import",
                ApiUrl = "/Import/GetImportTreeData",
                SessionId = sessionId,
                ShowInstructions = showInstructions,
                ShowAuditHistory = false
            };
        }
    }
}