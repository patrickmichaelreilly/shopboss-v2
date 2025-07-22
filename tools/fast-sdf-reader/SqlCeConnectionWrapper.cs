using System.Data;
using System.Data.OleDb;

namespace FastSdfReader;

public class SqlCeConnectionWrapper : IDisposable
{
    private readonly string _connectionString;
    private OleDbConnection? _connection;
    private bool _disposed = false;

    public SqlCeConnectionWrapper(string sdfFilePath)
    {
        // Use SQL CE OLE DB provider connection string
        _connectionString = $"Provider=Microsoft.SQLSERVER.CE.OLEDB.4.0; Data Source={sdfFilePath};";
    }

    public async Task OpenAsync()
    {
        try
        {
            _connection = new OleDbConnection(_connectionString);
            await _connection.OpenAsync();
            
            Console.WriteLine("Connected to SDF file via OLE DB");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to SDF: {ex.Message}", ex);
        }
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string query)
    {
        if (_connection?.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection not open");

        var results = new List<Dictionary<string, object?>>();

        try
        {
            using var command = new OleDbCommand(query, _connection);
            using var reader = await command.ExecuteReaderAsync();
            
            var columnNames = new string[reader.FieldCount];
            var columnTypes = new Type[reader.FieldCount];
            var skipColumns = new bool[reader.FieldCount];
            
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames[i] = reader.GetName(i);
                columnTypes[i] = reader.GetFieldType(i);
                
                // Skip binary/blob columns that cause conversion errors
                skipColumns[i] = columnTypes[i] == typeof(byte[]) || 
                                columnNames[i].ToLower().Contains("blob") ||
                                columnNames[i].ToLower().Contains("image") ||
                                columnNames[i].ToLower().Contains("binary");
            }

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (skipColumns[i])
                    {
                        row[columnNames[i]] = "[BLOB_SKIPPED]";
                        continue;
                    }
                    
                    try
                    {
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[columnNames[i]] = value;
                    }
                    catch
                    {
                        // Skip problematic columns
                        row[columnNames[i]] = "[CONVERSION_ERROR]";
                    }
                }
                results.Add(row);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Query failed: {ex.Message}", ex);
        }

        return results;
    }

    public async Task<List<string>> GetTableColumnsAsync(string tableName)
    {
        if (_connection?.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection not open");

        var columns = new List<string>();

        try
        {
            // Query the schema to get column information
            var schemaTable = await _connection.GetSchemaAsync("Columns", new[] { null, null, tableName });
            
            foreach (System.Data.DataRow row in schemaTable.Rows)
            {
                var columnName = row["COLUMN_NAME"].ToString();
                var dataType = row["DATA_TYPE"].ToString();
                columns.Add($"{columnName} ({dataType})");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get schema for {tableName}: {ex.Message}", ex);
        }

        return columns;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Close();
            _connection?.Dispose();
            Console.WriteLine("SQL CE OLE DB connection closed");
            _disposed = true;
        }
    }
}