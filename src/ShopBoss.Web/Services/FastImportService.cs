using System.Diagnostics;
using System.Text.Json;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

/// <summary>
/// Fast import service that uses FastSdfReader for 0.2 second SDF imports
/// Parallel implementation to ImporterService, orchestrates FastSdfReader → WorkOrderImportService
/// </summary>
public class FastImportService
{
    private readonly ILogger<FastImportService> _logger;
    private readonly WorkOrderImportService _workOrderImportService;
    private readonly string _fastSdfReaderPath;

    public FastImportService(
        ILogger<FastImportService> logger, 
        WorkOrderImportService workOrderImportService,
        IConfiguration configuration)
    {
        _logger = logger;
        _workOrderImportService = workOrderImportService;
        
        var configPath = configuration["FastSdfReaderPath"];
        
        if (!string.IsNullOrEmpty(configPath))
        {
            // If it's a relative path, make it relative to the executable directory
            if (!Path.IsPathRooted(configPath))
            {
                var baseDir = GetExecutableDirectory();
                _fastSdfReaderPath = Path.Combine(baseDir, configPath);
            }
            else
            {
                _fastSdfReaderPath = configPath;
            }
        }
        else
        {
            _fastSdfReaderPath = GetDefaultFastSdfReaderPath();
        }
        
        _logger.LogInformation("FastImportService: FastSdfReader path resolved to: {FastSdfReaderPath}", _fastSdfReaderPath);
        _logger.LogInformation("FastImportService: FastSdfReader executable exists: {Exists}", File.Exists(_fastSdfReaderPath));
    }

    private string GetExecutableDirectory()
    {
        // Strategy 1 (Primary): Use process path (available in .NET 6+)
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                var dir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    _logger.LogInformation("FastImportService: Using process directory: {Directory}", dir);
                    return dir;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FastImportService: Failed to get process path");
        }
        
        // Strategy 2 (Fallback): Use AppContext base directory
        var appDir = AppContext.BaseDirectory;
        _logger.LogInformation("FastImportService: Using AppContext.BaseDirectory: {Directory}", appDir);
        return appDir;
    }

    private string GetDefaultFastSdfReaderPath()
    {
        var baseDir = GetExecutableDirectory();
        
        // Common path patterns for FastSdfReader
        var commonPaths = new[]
        {
            // Development paths
            Path.Combine(baseDir, "tools", "fast-sdf-reader", "bin", "Release", "net8.0", "win-x86", "FastSdfReader.exe"),
            Path.Combine(baseDir, "tools", "fast-sdf-reader", "bin", "Debug", "net8.0", "win-x86", "FastSdfReader.exe"),
            // Parent directory searches
            Path.Combine(baseDir, "..", "tools", "fast-sdf-reader", "bin", "Release", "net8.0", "win-x86", "FastSdfReader.exe"),
            Path.Combine(baseDir, "..", "..", "tools", "fast-sdf-reader", "bin", "Release", "net8.0", "win-x86", "FastSdfReader.exe"),
            // Simplified deployment fallback (where testdeploy.sh will put it)
            Path.Combine(baseDir, "tools", "fast-sdf-reader", "FastSdfReader.exe")
        };
        
        foreach (var path in commonPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                _logger.LogInformation("Found FastSdfReader using common path strategy at: {Path}", fullPath);
                return fullPath;
            }
        }
        
        // Final fallback - return the expected path even if it doesn't exist
        var fallbackPath = Path.GetFullPath(Path.Combine(baseDir, "tools", "fast-sdf-reader", "FastSdfReader.exe"));
        _logger.LogWarning("Could not find FastSdfReader executable. Using fallback path: {Path}", fallbackPath);
        return fallbackPath;
    }

    /// <summary>
    /// Import SDF file using FastSdfReader and return WorkOrder entity
    /// This is the parallel method to ImporterService.ImportSdfFileAsync()
    /// </summary>
    public async Task<WorkOrder> ImportSdfFileAsync(string sdfFilePath, string workOrderName, IProgress<FastImportProgress>? progress = null)
    {
        if (!File.Exists(sdfFilePath))
        {
            throw new FileNotFoundException($"SDF file not found: {sdfFilePath}");
        }

        if (!File.Exists(_fastSdfReaderPath))
        {
            throw new FileNotFoundException($"FastSdfReader executable not found: {_fastSdfReaderPath}");
        }

        var startTime = DateTime.Now;
        
        try
        {
            // Stage 1: FastSdfReader extraction (~0.16s)
            progress?.Report(new FastImportProgress 
            { 
                Stage = "Reading SDF file directly...", 
                Percentage = 10,
                EstimatedTimeRemaining = TimeSpan.FromMilliseconds(400)
            });

            var rawData = await ExtractSdfDataAsync(sdfFilePath);
            
            // Stage 2: Transform to WorkOrder (~0.1s)
            progress?.Report(new FastImportProgress 
            { 
                Stage = "Creating WorkOrder entities...", 
                Percentage = 70,
                EstimatedTimeRemaining = TimeSpan.FromMilliseconds(100)
            });

            var workOrder = await _workOrderImportService.TransformToWorkOrderAsync(rawData, workOrderName);

            // Stage 3: Complete
            progress?.Report(new FastImportProgress 
            { 
                Stage = "Complete!", 
                Percentage = 100,
                EstimatedTimeRemaining = TimeSpan.Zero
            });

            var totalTime = (DateTime.Now - startTime).TotalSeconds;
            _logger.LogInformation("FastImportService: Successfully imported SDF file {FilePath} in {TotalTime:F2} seconds", 
                sdfFilePath, totalTime);

            return workOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FastImportService: Error importing SDF file {FilePath}", sdfFilePath);
            throw;
        }
    }

    /// <summary>
    /// Execute FastSdfReader.exe and parse JSON output to ImportData
    /// </summary>
    private async Task<ImportData> ExtractSdfDataAsync(string sdfFilePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _fastSdfReaderPath,
            Arguments = $"\"{sdfFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        process.Start();

        // Read the JSON output
        var jsonOutput = await process.StandardOutput.ReadToEndAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("FastSdfReader process failed with exit code {ExitCode}. Error: {ErrorOutput}", 
                process.ExitCode, errorOutput);
            throw new InvalidOperationException($"FastSdfReader process failed: {errorOutput}");
        }

        // Parse the JSON result
        if (string.IsNullOrWhiteSpace(jsonOutput))
        {
            throw new InvalidOperationException("FastSdfReader returned empty output");
        }

        try
        {
            var importData = JsonSerializer.Deserialize<ImportData>(jsonOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (importData == null)
            {
                throw new InvalidOperationException("Failed to parse FastSdfReader JSON output");
            }

            _logger.LogInformation("FastSdfReader extracted data - Products: {ProductCount}, Parts: {PartCount}, Hardware: {HardwareCount}", 
                importData.Products?.Count ?? 0, importData.Parts?.Count ?? 0, importData.Hardware?.Count ?? 0);

            return importData;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse FastSdfReader JSON output: {JsonOutput}", jsonOutput);
            throw new InvalidOperationException($"Invalid JSON from FastSdfReader: {ex.Message}");
        }
    }
}

/// <summary>
/// Progress reporting for FastImportService (parallel to ImporterProgress)
/// </summary>
public class FastImportProgress
{
    public string Stage { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
}