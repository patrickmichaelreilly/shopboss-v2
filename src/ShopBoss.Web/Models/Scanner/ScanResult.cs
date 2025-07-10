namespace ShopBoss.Web.Models.Scanner;

public class ScanResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public BarcodeType BarcodeType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
    public object? EntityData { get; set; }
    public string? RedirectUrl { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public string ScanType { get; set; } = "scan";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = new();
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public object? Data { get; set; }
    public bool RequiresRefresh { get; set; }
    public string CommandType { get; set; } = string.Empty;
}

public class BarcodeInfo
{
    public string CleanBarcode { get; set; } = string.Empty;
    public BarcodeType Type { get; set; }
    public string? CommandString { get; set; }
    public object? ParsedCommand { get; set; }
    public bool IsCommand => Type == BarcodeType.NavigationCommand || 
                           Type == BarcodeType.SystemCommand || 
                           Type == BarcodeType.AdminCommand || 
                           Type == BarcodeType.StationCommand;
}