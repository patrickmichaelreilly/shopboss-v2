namespace ShopBoss.Web.Models.Scanner;

public enum BarcodeType
{
    Unknown,
    NestSheet,
    Part,
    Product,
    Hardware,
    DetachedProduct,
    NavigationCommand,
    SystemCommand,
    AdminCommand,
    StationCommand
}

public enum NavigationCommand
{
    Unknown,
    GoToAdmin,
    GoToCnc,
    GoToSorting,
    GoToAssembly,
    GoToShipping,
    GoToHealthDashboard,
    GoToBackupManagement,
    GoToRackConfiguration
}

public enum SystemCommand
{
    Unknown,
    Refresh,
    Help,
    Cancel,
    ClearSession,
    Logout,
    ShowRecentScans,
    ShowWorkOrderSummary
}

public enum AdminCommand
{
    Unknown,
    CreateBackup,
    ArchiveActiveWorkOrder,
    ClearAllSessions,
    RunHealthCheck,
    ViewAuditLog
}

public enum StationCommand
{
    Unknown,
    // CNC Commands
    ShowRecentNestSheets,
    ShowUnprocessedNestSheets,
    // Sorting Commands
    ShowRackSummary,
    ShowAssemblyReadiness,
    // Assembly Commands
    ShowAssemblyQueue,
    ShowProductProgress,
    // Shipping Commands
    ShowShippingQueue,
    ShowWorkOrderProgress
}