using FastSdfReader;
using System.Diagnostics;

namespace FastSdfReader;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("FastSdfReader v1.0 - Direct SDF File Reader");
        Console.WriteLine("============================================");

        if (args.Length != 1)
        {
            Console.WriteLine("Usage: FastSdfReader.exe <path-to-sdf-file>");
            Console.WriteLine("Example: FastSdfReader.exe \"C:\\temp\\ShopBossWorkOrder.sdf\"");
            return 1;
        }

        var sdfPath = args[0];
        
        if (!File.Exists(sdfPath))
        {
            Console.WriteLine($"Error: SDF file not found: {sdfPath}");
            return 1;
        }

        Console.WriteLine($"Reading SDF file: {sdfPath}");
        Console.WriteLine($"File size: {new FileInfo(sdfPath).Length / 1024 / 1024:F1} MB");
        Console.WriteLine();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var reader = new FastSdfReader(sdfPath);
            await reader.PrintSummaryAsync();
            
            stopwatch.Stop();
            Console.WriteLine($"\nTotal execution time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            
            return 0;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Console.WriteLine($"Error: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            Console.WriteLine($"Execution time before error: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            return 1;
        }
    }
}