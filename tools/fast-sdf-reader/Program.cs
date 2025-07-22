using FastSdfReader;
using System.Text.Json;

namespace FastSdfReader;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            return 1;
        }

        try
        {
            var reader = new FastSdfReader(args[0]);
            var data = await reader.ReadAsync();
            
            // Output JSON for FastImportService consumption
            Console.WriteLine(JsonSerializer.Serialize(data));
            return 0;
        }
        catch
        {
            return 1;
        }
    }
}