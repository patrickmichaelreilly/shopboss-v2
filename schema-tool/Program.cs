using System;
using Microsoft.Data.Sqlite;
using System.IO;

string dbPath = "../shopboss.db";
if (!File.Exists(dbPath))
{
    Console.WriteLine($"Database file {dbPath} not found!");
    return;
}

string connectionString = $"Data Source={dbPath};";

using var connection = new SqliteConnection(connectionString);
connection.Open();

Console.WriteLine("=== TABLES AND SCHEMA ===");

// Get all tables
using var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;", connection);
using var reader = cmd.ExecuteReader();

var tableNames = new List<string>();
while (reader.Read())
{
    tableNames.Add(reader.GetString(0));
}

foreach (var tableName in tableNames)
{
    Console.WriteLine($"\n--- TABLE: {tableName} ---");
    
    // Get schema for this table
    using var schemaCmd = new SqliteCommand($"SELECT sql FROM sqlite_master WHERE type='table' AND name='{tableName}';", connection);
    var schema = schemaCmd.ExecuteScalar()?.ToString();
    Console.WriteLine(schema);
    
    // Get row count
    try
    {
        using var countCmd = new SqliteCommand($"SELECT COUNT(*) FROM [{tableName}];", connection);
        var count = countCmd.ExecuteScalar();
        Console.WriteLine($"-- Rows: {count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"-- Could not count rows: {ex.Message}");
    }
}

Console.WriteLine("\n=== INDEXES ===");
using var indexCmd = new SqliteCommand("SELECT name, sql FROM sqlite_master WHERE type='index' AND sql IS NOT NULL ORDER BY name;", connection);
using var indexReader = indexCmd.ExecuteReader();

while (indexReader.Read())
{
    string indexName = indexReader.GetString(0);
    string indexSql = indexReader.GetString(1);
    Console.WriteLine($"\n{indexName}:");
    Console.WriteLine(indexSql);
}
