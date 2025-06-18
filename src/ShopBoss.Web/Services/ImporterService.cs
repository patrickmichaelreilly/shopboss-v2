using System.Diagnostics;
using System.Text.Json;

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
            // If it's a relative path, make it relative to the application directory
            if (!Path.IsPathRooted(configPath))
            {
                _importerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath);
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
        
        _logger.LogInformation("Importer path resolved to: {ImporterPath}", _importerPath);
        _logger.LogInformation("Importer executable exists: {Exists}", File.Exists(_importerPath));
    }

    private static string GetDefaultImporterPath()
    {
        // Start from the application directory and work our way up to find the tools directory
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var searchDir = currentDir;
        
        // Look for the tools directory by going up the directory tree
        for (int i = 0; i < 10; i++) // Limit search to prevent infinite loop
        {
            var toolsPath = Path.Combine(searchDir, "tools", "importer", "bin", "Release", "net8.0", "win-x86", "Importer.exe");
            if (File.Exists(toolsPath))
            {
                return toolsPath;
            }
            
            var parentDir = Directory.GetParent(searchDir);
            if (parentDir == null) break;
            searchDir = parentDir.FullName;
        }
        
        // Fallback to the original relative path approach
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var importerDir = Path.Combine(basePath, "..", "..", "..", "..", "..", "tools", "importer", "bin", "Release", "net8.0", "win-x86");
        return Path.GetFullPath(Path.Combine(importerDir, "Importer.exe"));
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
}