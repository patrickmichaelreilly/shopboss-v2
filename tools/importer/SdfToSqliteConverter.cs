using System.Diagnostics;
using System.Text;

namespace SdfImporter;

public class SdfToSqliteConverter
{
    private readonly string _toolsDirectory;

    public SdfToSqliteConverter(string? toolsDirectory = null)
    {
        _toolsDirectory = toolsDirectory ?? Path.Combine(AppContext.BaseDirectory, "native");
    }

    public async Task<string> ConvertAsync(string sdfPath)
    {
        
        if (!File.Exists(sdfPath))
        {
            throw new FileNotFoundException($"SDF file not found: {sdfPath}");
        }

        var sdfDirectory = Path.GetDirectoryName(sdfPath);
        
        if (string.IsNullOrEmpty(sdfDirectory))
        {
            // If no directory in path, use current directory
            sdfDirectory = Directory.GetCurrentDirectory();
        }

        var sqliteFilePath = Path.Combine(sdfDirectory, "work.sqlite");
        var tempSqlPath = Path.Combine(sdfDirectory, "temp.sql");
        

        try
        {
            // Step 1: Convert SDF to SQL script using ExportSqlCE40.exe
            await ConvertSdfToSqlScript(sdfPath, tempSqlPath, sdfDirectory);

            // Step 2: Clean up SQL file to fix SQLite compatibility issues
            await CleanSqlForSqlite(tempSqlPath);

            // Step 3: Create SQLite database from SQL script
            await CreateSqliteFromScript(tempSqlPath, sqliteFilePath);

            return sqliteFilePath;
        }
        finally
        {
            // Keep temp.sql file for inspection - don't delete it
            // The 28MB temp.sql contains the actual data we need
        }
    }

    private async Task ConvertSdfToSqlScript(string sdfPath, string sqlPath, string workingDirectory)
    {
        var exportTool = Path.Combine(_toolsDirectory, "ExportSqlCe40.exe");
        
        if (!File.Exists(exportTool))
        {
            throw new FileNotFoundException($"ExportSqlCe40.exe not found at: {exportTool}. Please ensure native binaries are included.");
        }

        var connectionString = $"Data Source={sdfPath};";
        var baseWorkPath = Path.Combine(workingDirectory, "work");
        
        
        using var process = new Process();
        process.StartInfo.FileName = exportTool;
        process.StartInfo.Arguments = $"\"{connectionString}\" \"{baseWorkPath}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        
        // Add timeout to prevent hanging
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Kill the process if it's hanging
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync(); // Wait for kill to complete
            }
            throw new InvalidOperationException("ExportSqlCe40.exe process timed out after 60 seconds");
        }
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ExportSqlCe40.exe failed with exit code {process.ExitCode}. Error: {error}");
        }

        // ExportSqlCe40.exe creates files like work_0000.sql, work_0001.sql, etc.
        // We need to combine them into a single temp.sql file
        await CombineSqlFiles(workingDirectory, sqlPath);
    }

    private async Task CombineSqlFiles(string directory, string outputPath)
    {
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directory));
        }
        
        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));
        }


        var workFiles = Directory.GetFiles(directory, "work_*")
                                .Where(f => !Path.HasExtension(f) || Path.GetExtension(f) == ".sql")
                                .OrderBy(f => f)
                                .ToArray();


        if (workFiles.Length == 0)
        {
            throw new InvalidOperationException($"No files found with pattern work_* in directory {directory}");
        }

        using var outputWriter = new StreamWriter(outputPath);
        
        foreach (var workFile in workFiles)
        {
            var content = await File.ReadAllTextAsync(workFile);
            await outputWriter.WriteAsync(content);
            await outputWriter.WriteLineAsync(); // Add newline between files
            
            // Clean up the work file
            File.Delete(workFile);
        }
        
        await outputWriter.FlushAsync();
        
    }

    private async Task CleanSqlForSqlite(string sqlPath)
    {
        // Read the entire SQL file
        var content = await File.ReadAllTextAsync(sqlPath);
        var originalSize = content.Length;
        Console.Error.WriteLine($"Cleaning SQL file ({originalSize / 1024 / 1024:F1} MB)...");
        
        // Fix common SQL Server Compact -> SQLite compatibility issues
        content = content
            // Replace N'' (empty NVARCHAR) with '' (empty string)
            .Replace("N''", "''")
            // Replace N'...' (NVARCHAR literals) with '...' (string literals)
            .Replace("N'", "'")
            // Replace [bracketed] identifiers with "quoted" identifiers
            .Replace("[", "\"")
            .Replace("]", "\"")
            // Remove SQL Server specific syntax
            .Replace("IDENTITY(1,1)", "")
            .Replace("PRIMARY KEY CLUSTERED", "PRIMARY KEY")
            .Replace("NONCLUSTERED", "")
            // Handle bit values
            .Replace("((1))", "1")
            .Replace("((0))", "0");
        
        // Fix SQL Server timestamp syntax for SQLite
        content = FixTimestampSyntax(content);
        
        // Remove large hex literals that cause SQLite issues
        content = RemoveLargeHexLiterals(content);
        
        // Fix SQLite constraint syntax
        content = FixConstraintSyntax(content);
        
        // Handle duplicate index creation
        content = HandleDuplicateIndexes(content);
        
        var finalSize = content.Length;
        var reductionMB = (originalSize - finalSize) / 1024.0 / 1024.0;
        Console.Error.WriteLine($"SQL cleanup completed (reduced by {reductionMB:F1} MB)");
        
        // Write the cleaned content back
        await File.WriteAllTextAsync(sqlPath, content);
    }
    
    private string RemoveLargeHexLiterals(string content)
    {
        // Pattern to match hex literals (0x followed by hex digits)
        // Replace large hex literals (>100 chars) with NULL
        var lines = content.Split('\n');
        var result = new StringBuilder();
        var replacementCount = 0;
        
        foreach (var line in lines)
        {
            var cleanedLine = line;
            
            // Look for hex literals that are too large
            var hexPattern = @"0x[0-9A-Fa-f]+";
            var matches = System.Text.RegularExpressions.Regex.Matches(line, hexPattern);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // If hex literal is very long (likely binary data), replace with NULL
                if (match.Value.Length > 100)
                {
                    cleanedLine = cleanedLine.Replace(match.Value, "NULL");
                    replacementCount++;
                }
            }
            
            result.AppendLine(cleanedLine);
        }
        
        if (replacementCount > 0)
        {
            Console.Error.WriteLine($"Cleaned {replacementCount} binary data columns");
        }
        return result.ToString();
    }
    
    private string FixConstraintSyntax(string content)
    {
        // SQLite doesn't support ALTER TABLE ADD CONSTRAINT for PRIMARY KEY
        // Remove these lines entirely as primary keys should be defined in CREATE TABLE
        var lines = content.Split('\n');
        var result = new StringBuilder();
        var removedCount = 0;
        
        foreach (var line in lines)
        {
            // Skip ALTER TABLE ADD CONSTRAINT PRIMARY KEY lines
            if (line.Trim().StartsWith("ALTER TABLE") && line.Contains("ADD CONSTRAINT") && line.Contains("PRIMARY KEY"))
            {
                removedCount++;
                continue;
            }
            
            result.AppendLine(line);
        }
        
        return result.ToString();
    }
    
    private string HandleDuplicateIndexes(string content)
    {
        // Track created indexes to avoid duplicates
        var createdIndexes = new HashSet<string>();
        var lines = content.Split('\n');
        var result = new StringBuilder();
        var duplicatesSkipped = 0;
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Check for CREATE INDEX statements
            if (trimmedLine.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase))
            {
                // Extract index name
                var parts = trimmedLine.Split(' ');
                if (parts.Length >= 3)
                {
                    var indexName = parts[2].Trim('"');
                    
                    if (createdIndexes.Contains(indexName))
                    {
                        duplicatesSkipped++;
                        continue;
                    }
                    
                    createdIndexes.Add(indexName);
                }
            }
            
            result.AppendLine(line);
        }
        
        return result.ToString();
    }
    
    private string FixTimestampSyntax(string content)
    {
        // Replace SQL Server {ts 'timestamp'} syntax with SQLite 'timestamp' format
        var timestampPattern = @"\{ts\s+'([^']+)'\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, timestampPattern);
        var replacementCount = 0;
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            // Replace {ts 'timestamp'} with just 'timestamp'
            content = content.Replace(match.Value, $"'{match.Groups[1].Value}'");
            replacementCount++;
        }
        
        return content;
    }

    private async Task CreateSqliteFromScript(string sqlPath, string sqliteFilePath)
    {
        // Delete existing SQLite file if it exists
        if (File.Exists(sqliteFilePath))
        {
            File.Delete(sqliteFilePath);
        }

        var sqlite3Path = Path.Combine(_toolsDirectory, "sqlite3.exe");
        
        if (!File.Exists(sqlite3Path))
        {
            throw new FileNotFoundException($"sqlite3.exe not found at: {sqlite3Path}. Please ensure native binaries are included.");
        }

        using var process = new Process();
        process.StartInfo.FileName = sqlite3Path;
        process.StartInfo.Arguments = $"\"{sqliteFilePath}\" \".read {sqlPath}\" \".quit\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        
        // Read output streams immediately to prevent deadlock
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        // Close stdin immediately to prevent hanging
        process.StandardInput.Close();
        
        // Add timeout to prevent hanging - increased for large SQL files
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Kill the process if it's hanging
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync(); // Wait for kill to complete
            }
            throw new InvalidOperationException("sqlite3 process timed out after 5 minutes - very large SQL file");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"sqlite3 failed with exit code {process.ExitCode}. Error: {error}");
        }

        // Check if the SQLite file was created and has reasonable size
        if (!File.Exists(sqliteFilePath))
        {
            throw new InvalidOperationException("SQLite file was not created");
        }

        var sqliteSize = new FileInfo(sqliteFilePath).Length;
        var sqlSize = new FileInfo(sqlPath).Length;
        
        // If SQLite file is much smaller than SQL file, something probably went wrong
        if (sqliteSize < sqlSize / 100) // SQLite should be at least 1% of SQL size
        {
            throw new InvalidOperationException($"SQLite file ({sqliteSize} bytes) is suspiciously small compared to SQL file ({sqlSize} bytes). Import may have failed. Check temp.sql file for issues.");
        }
    }

}