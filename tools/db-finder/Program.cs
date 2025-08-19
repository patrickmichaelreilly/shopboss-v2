using Microsoft.Data.Sqlite;

namespace DatabaseFinder;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("🔍 SQLite Database Finder for ShopBoss");
        Console.WriteLine(new string('=', 50));
        
        var searchPaths = new[]
        {
            @"C:\ShopBoss",
            @"C:\ShopBoss-Testing",
            Environment.CurrentDirectory,
            Path.Combine(Environment.CurrentDirectory, "temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShopBoss"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopBoss")
        };

        foreach (var searchPath in searchPaths)
        {
            Console.WriteLine($"\n📁 Searching: {searchPath}");
            
            if (!Directory.Exists(searchPath))
            {
                Console.WriteLine("   Directory not found");
                continue;
            }

            try
            {
                // Look for common SQLite file patterns
                var patterns = new[] { "*.db", "*.sqlite", "*.sqlite3", "*.db3", "shopboss*" };
                
                foreach (var pattern in patterns)
                {
                    var files = Directory.GetFiles(searchPath, pattern, SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        Console.WriteLine($"   📄 Found: {Path.GetFileName(file)}");
                        Console.WriteLine($"      Full path: {file}");
                        Console.WriteLine($"      Size: {new FileInfo(file).Length:N0} bytes");
                        Console.WriteLine($"      Modified: {File.GetLastWriteTime(file)}");
                        
                        // Try to validate if it's a SQLite database
                        if (IsSqliteDatabase(file))
                        {
                            Console.WriteLine($"      ✅ Valid SQLite database");
                            try
                            {
                                var tableCount = GetTableCount(file);
                                Console.WriteLine($"      📊 Tables: {tableCount}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"      ⚠️  Could not read tables: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"      ❌ Not a valid SQLite database");
                        }
                        Console.WriteLine();
                    }
                }
                
                // Also list all files to see what's actually there
                var allFiles = Directory.GetFiles(searchPath, "*", SearchOption.TopDirectoryOnly);
                if (allFiles.Length > 0)
                {
                    Console.WriteLine($"   📋 All files in directory:");
                    foreach (var file in allFiles.Take(20)) // Limit to first 20 files
                    {
                        var fileInfo = new FileInfo(file);
                        Console.WriteLine($"      {Path.GetFileName(file)} ({fileInfo.Length:N0} bytes)");
                    }
                    if (allFiles.Length > 20)
                    {
                        Console.WriteLine($"      ... and {allFiles.Length - 20} more files");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Error searching directory: {ex.Message}");
            }
        }

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("🎯 RECOMMENDATIONS:");
        Console.WriteLine("1. If no SQLite files found, the service might not have started yet");
        Console.WriteLine("2. Check Windows Event Log for service startup errors");
        Console.WriteLine("3. Look for appsettings.json to see configured database path");
        Console.WriteLine("4. The database is created on first run - try starting the service");
        Console.WriteLine(new string('=', 50));
    }

    private static bool IsSqliteDatabase(string filePath)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' LIMIT 1";
            command.ExecuteScalar();
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetTableCount(string filePath)
    {
        using var connection = new SqliteConnection($"Data Source={filePath};Mode=ReadOnly");
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        
        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }
}