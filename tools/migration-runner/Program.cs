using Microsoft.Data.Sqlite;

namespace MigrationRunner;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: MigrationRunner <database-path> <migration-script-path>");
            Console.WriteLine("Example: MigrationRunner production.db migration.sql");
            return;
        }

        string dbPath = args[0];
        string scriptPath = args[1];

        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"ERROR: Database not found: {dbPath}");
            return;
        }

        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"ERROR: Migration script not found: {scriptPath}");
            return;
        }

        try
        {
            Console.WriteLine("🔄 Running database migration...");
            Console.WriteLine($"   Database: {Path.GetFileName(dbPath)}");
            Console.WriteLine($"   Script: {Path.GetFileName(scriptPath)}");
            Console.WriteLine();

            // Read the migration script
            string migrationSql = await File.ReadAllTextAsync(scriptPath);

            // Connect to database and run migration
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            // Split the script into individual statements (simple approach)
            var statements = migrationSql.Split(new[] { ";\r\n", ";\n", ";" }, StringSplitOptions.RemoveEmptyEntries);

            Console.WriteLine($"   Found {statements.Length} potential statements in script");

            int statementCount = 0;
            foreach (var statement in statements)
            {
                var trimmedStatement = statement.Trim();
                
                // Skip empty statements and comments
                if (string.IsNullOrWhiteSpace(trimmedStatement) || 
                    trimmedStatement.StartsWith("--") ||
                    trimmedStatement.StartsWith("/*"))
                {
                    Console.WriteLine($"   Skipping: {trimmedStatement.Substring(0, Math.Min(50, trimmedStatement.Length))}...");
                    continue;
                }

                Console.WriteLine($"   Executing: {trimmedStatement.Substring(0, Math.Min(50, trimmedStatement.Length))}...");

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = trimmedStatement;
                    await command.ExecuteNonQueryAsync();
                    statementCount++;
                    
                    // Show progress for major operations
                    if (trimmedStatement.Contains("CREATE TABLE"))
                    {
                        var tableName = ExtractTableName(trimmedStatement);
                        Console.WriteLine($"   ✅ Created table: {tableName}");
                    }
                    else if (trimmedStatement.Contains("ALTER TABLE"))
                    {
                        var tableName = ExtractAlterTableName(trimmedStatement);
                        Console.WriteLine($"   ✅ Modified table: {tableName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Error executing statement: {ex.Message}");
                    Console.WriteLine($"      Statement: {trimmedStatement.Substring(0, Math.Min(100, trimmedStatement.Length))}...");
                    throw;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"✅ Migration completed successfully!");
            Console.WriteLine($"   Executed {statementCount} statements");

            // Run verification queries
            Console.WriteLine("\n🔍 Verifying migration...");
            await VerifyMigration(connection);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Migration failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static string ExtractTableName(string createStatement)
    {
        var parts = createStatement.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var tableIndex = Array.IndexOf(parts, "TABLE");
        if (tableIndex >= 0 && tableIndex + 1 < parts.Length)
        {
            return parts[tableIndex + 1].Trim('(', ')', ';');
        }
        return "Unknown";
    }

    private static string ExtractAlterTableName(string alterStatement)
    {
        var parts = alterStatement.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var tableIndex = Array.IndexOf(parts, "TABLE");
        if (tableIndex >= 0 && tableIndex + 1 < parts.Length)
        {
            return parts[tableIndex + 1];
        }
        return "Unknown";
    }

    private static async Task VerifyMigration(SqliteConnection connection)
    {
        var tablesToCheck = new[]
        {
            "Projects",
            "CustomWorkOrders", 
            "PurchaseOrders",
            "ProjectAttachments",
            "ProjectEvents",
            "PartLabels"
        };

        foreach (var tableName in tablesToCheck)
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM {tableName}";
                var count = await command.ExecuteScalarAsync();
                Console.WriteLine($"   ✅ {tableName}: {count} records");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ {tableName}: Error - {ex.Message}");
            }
        }

        // Check that ProjectId column was added to WorkOrders
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(WorkOrders)";
            using var reader = await command.ExecuteReaderAsync();
            
            bool foundProjectId = false;
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1); // column name is at index 1
                if (columnName == "ProjectId")
                {
                    foundProjectId = true;
                    break;
                }
            }

            if (foundProjectId)
            {
                Console.WriteLine($"   ✅ WorkOrders.ProjectId column added successfully");
            }
            else
            {
                Console.WriteLine($"   ❌ WorkOrders.ProjectId column not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ WorkOrders verification failed: {ex.Message}");
        }
    }
}