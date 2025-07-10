using System.Diagnostics;
using System.Text.Json;
using System.Reflection;

namespace ShopBoss.Web.Services;

public class ImporterService
{
    private readonly ILogger<ImporterService> _logger;
    private readonly string _importerPath;

    public ImporterService(ILogger<ImporterService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var configPath = configuration["ImporterPath"];
        
        if (!string.IsNullOrEmpty(configPath))
        {
            // If it's a relative path, make it relative to the executable directory
            if (!Path.IsPathRooted(configPath))
            {
                var baseDir = GetExecutableDirectory();
                _importerPath = Path.Combine(baseDir, configPath);
            }
            else
            {
                _importerPath = configPath;
            }
        }
        else
        {
            _importerPath = GetDefaultImporterPath();
        }
        
        _logger.LogInformation("UPDATED IMPORTER SERVICE: Importer path resolved to: {ImporterPath}", _importerPath);
        _logger.LogInformation("UPDATED IMPORTER SERVICE: Importer executable exists: {Exists}", File.Exists(_importerPath));
    }

    private string GetExecutableDirectory()
    {
        // Simplified 2-strategy approach for reliable path resolution
        // Works for both deploy-to-windows.sh testing and Windows Service production
        
        // Strategy 1 (Primary): Use process path (available in .NET 6+)
        try
        {
            var processPath = Environment.ProcessPath;
            _logger.LogInformation("UPDATED IMPORTER SERVICE: Process path: {Path}", processPath);
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                var dir = Path.GetDirectoryName(processPath)!;
                _logger.LogInformation("UPDATED IMPORTER SERVICE: Using process directory: {Directory}", dir);
                return dir;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Process path strategy failed");
        }
        
        // Strategy 2 (Fallback): AppDomain base directory
        var baseDirPath = AppDomain.CurrentDomain.BaseDirectory;
        _logger.LogInformation("UPDATED IMPORTER SERVICE: Fallback to AppDomain base directory: {Directory}", baseDirPath);
        return baseDirPath;
    }
    
    private string GetDefaultImporterPath()
    {
        var baseDir = GetExecutableDirectory();
        _logger.LogInformation("Using base directory for importer search: {BaseDirectory}", baseDir);
        
        // Strategy 1: Look for tools directory relative to executable
        var searchDir = baseDir;
        for (int i = 0; i < 10; i++) // Limit search to prevent infinite loop
        {
            var toolsPath = Path.Combine(searchDir, "tools", "importer", "bin", "Release", "net8.0", "win-x86", "Importer.exe");
            if (File.Exists(toolsPath))
            {
                _logger.LogInformation("Found importer using directory traversal strategy at: {Path}", toolsPath);
                return toolsPath;
            }
            
            var parentDir = Directory.GetParent(searchDir);
            if (parentDir == null) break;
            searchDir = parentDir.FullName;
        }
        
        // Strategy 2: Direct relative path from executable directory
        var directPath = Path.Combine(baseDir, "tools", "importer", "bin", "Release", "net8.0", "win-x86", "Importer.exe");
        if (File.Exists(directPath))
        {
            _logger.LogInformation("Found importer using direct relative path strategy at: {Path}", directPath);
            return directPath;
        }
        
        // Strategy 3: Look in common deployment locations (x86 only - x64 will NOT work)
        var commonPaths = new[]
        {
            // Direct deployment paths
            Path.Combine(baseDir, "tools", "importer", "bin", "Release", "net8.0", "win-x86", "Importer.exe"),
            Path.Combine(baseDir, "tools", "importer", "bin", "Debug", "net8.0", "win-x86", "Importer.exe"),
            // Parent directory searches
            Path.Combine(baseDir, "..", "tools", "importer", "bin", "Release", "net8.0", "win-x86", "Importer.exe"),
            Path.Combine(baseDir, "..", "..", "tools", "importer", "bin", "Release", "net8.0", "win-x86", "Importer.exe"),
            // Simplified deployment fallback
            Path.Combine(baseDir, "tools", "importer", "Importer.exe")
        };
        
        foreach (var path in commonPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                _logger.LogInformation("Found importer using common path strategy at: {Path}", fullPath);
                return fullPath;
            }
        }
        
        // Final fallback - return the expected path even if it doesn't exist
        var fallbackPath = Path.GetFullPath(Path.Combine(baseDir, "tools", "importer", "bin", "Release", "net8.0", "win-x86", "Importer.exe"));
        _logger.LogWarning("Could not find importer executable. Using fallback path: {Path}", fallbackPath);
        return fallbackPath;
    }

    public async Task<ImporterResult> ImportSdfFileAsync(string sdfFilePath, IProgress<ImporterProgress>? progress = null)
    {
        if (!File.Exists(sdfFilePath))
        {
            throw new FileNotFoundException($"SDF file not found: {sdfFilePath}");
        }

        if (!File.Exists(_importerPath))
        {
            throw new FileNotFoundException($"Importer executable not found: {_importerPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _importerPath,
            Arguments = $"\"{sdfFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var result = new ImporterResult();
        var startTime = DateTime.Now;

        try
        {
            using var process = new Process { StartInfo = startInfo };
            
            process.Start();

            // Start progress simulation (since we can't get real progress from the external tool)
            var progressTask = progress != null ? SimulateProgress(progress, process) : Task.CompletedTask;

            // Read the JSON output
            var jsonOutput = await process.StandardOutput.ReadToEndAsync();
            var errorOutput = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            await progressTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("Importer process failed with exit code {ExitCode}. Error: {ErrorOutput}", 
                    process.ExitCode, errorOutput);
                throw new InvalidOperationException($"Import process failed: {errorOutput}");
            }

            // Parse the JSON result
            if (string.IsNullOrWhiteSpace(jsonOutput))
            {
                throw new InvalidOperationException("Importer returned empty output");
            }

            var importData = JsonSerializer.Deserialize<ImportData>(jsonOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (importData == null)
            {
                throw new InvalidOperationException("Failed to parse importer output");
            }

            result.Success = true;
            result.Data = importData;
            result.TotalTime = (DateTime.Now - startTime).TotalSeconds;
            result.Message = "Import completed successfully";

            _logger.LogInformation("Successfully imported SDF file {FilePath} in {TotalTime:F1} seconds", 
                sdfFilePath, result.TotalTime);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing SDF file {FilePath}", sdfFilePath);
            result.Success = false;
            result.Message = ex.Message;
            result.TotalTime = (DateTime.Now - startTime).TotalSeconds;
            return result;
        }
    }

    private async Task SimulateProgress(IProgress<ImporterProgress> progress, Process process)
    {
        var stages = new[]
        {
            new { Stage = "Converting SDF...", Percentage = 30, Duration = 30000 },
            new { Stage = "Cleaning SQL...", Percentage = 60, Duration = 30000 },
            new { Stage = "Generating JSON...", Percentage = 90, Duration = 60000 },
            new { Stage = "Complete!", Percentage = 100, Duration = 1000 }
        };

        foreach (var stage in stages)
        {
            progress.Report(new ImporterProgress
            {
                Stage = stage.Stage,
                Percentage = stage.Percentage,
                EstimatedTimeRemaining = TimeSpan.FromMilliseconds(
                    stages.Skip(Array.IndexOf(stages, stage) + 1).Sum(s => s.Duration))
            });

            // Wait for the stage duration or until process completes
            var delay = Task.Delay(stage.Duration);
            var processTask = process.WaitForExitAsync();
            
            await Task.WhenAny(delay, processTask);
            
            if (process.HasExited)
            {
                progress.Report(new ImporterProgress
                {
                    Stage = "Complete!",
                    Percentage = 100,
                    EstimatedTimeRemaining = TimeSpan.Zero
                });
                break;
            }
        }
    }
}

public class ImporterResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double TotalTime { get; set; }
    public ImportData? Data { get; set; }
}

public class ImporterProgress
{
    public string Stage { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
}

public class ImportData
{
    public List<Dictionary<string, object?>> Products { get; set; } = new();
    public List<Dictionary<string, object?>> Parts { get; set; } = new();
    public List<Dictionary<string, object?>> PlacedSheets { get; set; } = new();
    public List<Dictionary<string, object?>> Hardware { get; set; } = new();
    public List<Dictionary<string, object?>> Subassemblies { get; set; } = new();
    public List<Dictionary<string, object?>> OptimizationResults { get; set; } = new();
    
    // Alias for PlacedSheets to make it clearer in code
    public List<Dictionary<string, object?>> NestSheets => PlacedSheets;
}