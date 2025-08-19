using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace SchemaComparer;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: SchemaComparer <production-db-path> <local-db-path>");
            Console.WriteLine("Example: SchemaComparer production.db local.db");
            return;
        }

        string productionDbPath = args[0];
        string localDbPath = args[1];

        if (!File.Exists(productionDbPath))
        {
            Console.WriteLine($"ERROR: Production database not found: {productionDbPath}");
            return;
        }

        if (!File.Exists(localDbPath))
        {
            Console.WriteLine($"ERROR: Local database not found: {localDbPath}");
            return;
        }

        try
        {
            var productionSchema = await ExtractSchema(productionDbPath, "Production");
            var localSchema = await ExtractSchema(localDbPath, "Local/Current");

            CompareSchemas(productionSchema, localSchema);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }

    private static async Task<Dictionary<string, TableSchema>> ExtractSchema(string dbPath, string dbName)
    {
        var schema = new Dictionary<string, TableSchema>();
        
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        await connection.OpenAsync();

        Console.WriteLine($"🔍 Extracting schema from {dbName} database: {Path.GetFileName(dbPath)}");

        // Get all tables
        var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' AND name NOT LIKE 'sqlite_%' 
            ORDER BY name";

        using var tablesReader = await tablesCommand.ExecuteReaderAsync();
        var tableNames = new List<string>();
        
        while (await tablesReader.ReadAsync())
        {
            tableNames.Add(tablesReader.GetString(0));
        }

        // Get schema for each table
        foreach (var tableName in tableNames)
        {
            var columns = new List<ColumnInfo>();
            
            var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = $"PRAGMA table_info({tableName})";
            
            using var columnsReader = await columnsCommand.ExecuteReaderAsync();
            while (await columnsReader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = columnsReader.GetString(1), // name
                    Type = columnsReader.GetString(2), // type
                    NotNull = columnsReader.GetInt32(3) == 1, // notnull
                    DefaultValue = columnsReader.IsDBNull(4) ? null : columnsReader.GetString(4), // dflt_value
                    IsPrimaryKey = columnsReader.GetInt32(5) == 1 // pk
                });
            }
            
            schema[tableName] = new TableSchema
            {
                Name = tableName,
                Columns = columns
            };
        }

        Console.WriteLine($"   Found {schema.Count} tables");
        return schema;
    }

    private static void CompareSchemas(Dictionary<string, TableSchema> production, Dictionary<string, TableSchema> local)
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("📊 SCHEMA COMPARISON RESULTS");
        Console.WriteLine(new string('=', 80));

        // Find new tables (in local but not in production)
        var newTables = local.Keys.Except(production.Keys).OrderBy(x => x).ToList();
        if (newTables.Any())
        {
            Console.WriteLine("\n🆕 NEW TABLES (need to be created in production):");
            Console.WriteLine(new string('-', 50));
            foreach (var tableName in newTables)
            {
                var table = local[tableName];
                Console.WriteLine($"   📋 {tableName}");
                foreach (var column in table.Columns)
                {
                    var pkIndicator = column.IsPrimaryKey ? " [PK]" : "";
                    var nullIndicator = column.NotNull ? " NOT NULL" : "";
                    var defaultIndicator = !string.IsNullOrEmpty(column.DefaultValue) ? $" DEFAULT {column.DefaultValue}" : "";
                    Console.WriteLine($"      - {column.Name}: {column.Type}{pkIndicator}{nullIndicator}{defaultIndicator}");
                }
                Console.WriteLine();
            }
        }

        // Find removed tables (in production but not in local)
        var removedTables = production.Keys.Except(local.Keys).OrderBy(x => x).ToList();
        if (removedTables.Any())
        {
            Console.WriteLine("\n❌ REMOVED TABLES (exist in production but not in current):");
            Console.WriteLine(new string('-', 50));
            foreach (var tableName in removedTables)
            {
                Console.WriteLine($"   📋 {tableName}");
            }
            Console.WriteLine();
        }

        // Find modified tables
        var commonTables = production.Keys.Intersect(local.Keys).OrderBy(x => x).ToList();
        var modifiedTables = new List<string>();

        foreach (var tableName in commonTables)
        {
            var prodTable = production[tableName];
            var localTable = local[tableName];

            var prodColumns = prodTable.Columns.ToDictionary(c => c.Name);
            var localColumns = localTable.Columns.ToDictionary(c => c.Name);

            var newColumns = localColumns.Keys.Except(prodColumns.Keys).ToList();
            var removedColumns = prodColumns.Keys.Except(localColumns.Keys).ToList();
            var modifiedColumns = new List<string>();

            // Check for column type changes
            foreach (var commonColumn in prodColumns.Keys.Intersect(localColumns.Keys))
            {
                var prodCol = prodColumns[commonColumn];
                var localCol = localColumns[commonColumn];

                if (prodCol.Type != localCol.Type || prodCol.NotNull != localCol.NotNull)
                {
                    modifiedColumns.Add(commonColumn);
                }
            }

            if (newColumns.Any() || removedColumns.Any() || modifiedColumns.Any())
            {
                modifiedTables.Add(tableName);
                
                Console.WriteLine($"\n🔄 MODIFIED TABLE: {tableName}");
                Console.WriteLine(new string('-', 30));

                if (newColumns.Any())
                {
                    Console.WriteLine("   ➕ New columns:");
                    foreach (var columnName in newColumns)
                    {
                        var column = localColumns[columnName];
                        var nullIndicator = column.NotNull ? " NOT NULL" : "";
                        var defaultIndicator = !string.IsNullOrEmpty(column.DefaultValue) ? $" DEFAULT {column.DefaultValue}" : "";
                        Console.WriteLine($"      + {column.Name}: {column.Type}{nullIndicator}{defaultIndicator}");
                    }
                }

                if (removedColumns.Any())
                {
                    Console.WriteLine("   ➖ Removed columns:");
                    foreach (var columnName in removedColumns)
                    {
                        Console.WriteLine($"      - {columnName}");
                    }
                }

                if (modifiedColumns.Any())
                {
                    Console.WriteLine("   🔄 Modified columns:");
                    foreach (var columnName in modifiedColumns)
                    {
                        var prodCol = prodColumns[columnName];
                        var localCol = localColumns[columnName];
                        Console.WriteLine($"      ~ {columnName}:");
                        Console.WriteLine($"         Production: {prodCol.Type}{(prodCol.NotNull ? " NOT NULL" : "")}");
                        Console.WriteLine($"         Local:      {localCol.Type}{(localCol.NotNull ? " NOT NULL" : "")}");
                    }
                }
                Console.WriteLine();
            }
        }

        // Summary
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("📋 SUMMARY");
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"Production tables: {production.Count}");
        Console.WriteLine($"Local tables: {local.Count}");
        Console.WriteLine($"New tables: {newTables.Count}");
        Console.WriteLine($"Removed tables: {removedTables.Count}");
        Console.WriteLine($"Modified tables: {modifiedTables.Count}");
        Console.WriteLine($"Unchanged tables: {commonTables.Count - modifiedTables.Count}");

        if (newTables.Any() || modifiedTables.Any())
        {
            Console.WriteLine("\n⚠️  MIGRATION REQUIRED");
            Console.WriteLine("The production database needs to be updated to match the current schema.");
        }
        else if (removedTables.Any())
        {
            Console.WriteLine("\n⚠️  SCHEMA MISMATCH");
            Console.WriteLine("Production has tables that don't exist in current version.");
        }
        else
        {
            Console.WriteLine("\n✅ SCHEMAS MATCH");
            Console.WriteLine("No migration required - schemas are identical.");
        }

        Console.WriteLine(new string('=', 80));
    }
}

public class TableSchema
{
    public string Name { get; set; } = "";
    public List<ColumnInfo> Columns { get; set; } = new();
}

public class ColumnInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool NotNull { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }
}