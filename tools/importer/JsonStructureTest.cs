using System.Text.Json;

namespace SdfImporter;

public static class JsonStructureTest
{
    private static readonly string[] RequiredKeys = 
    {
        "products",
        "parts", 
        "placedSheets",
        "hardware",
        "subassemblies",
        "optimizationResults"
    };
    
    public static bool VerifyJsonStructure(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            if (root.ValueKind != JsonValueKind.Object)
            {
                Console.Error.WriteLine("ERROR: JSON root is not an object");
                return false;
            }
            
            var missingKeys = new List<string>();
            
            foreach (var requiredKey in RequiredKeys)
            {
                if (!root.TryGetProperty(requiredKey, out _))
                {
                    missingKeys.Add(requiredKey);
                }
            }
            
            if (missingKeys.Count > 0)
            {
                Console.Error.WriteLine($"ERROR: Missing required keys: {string.Join(", ", missingKeys)}");
                return false;
            }
            
            // JSON structure validation passed (silent success)
            return true;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"ERROR: Invalid JSON format: {ex.Message}");
            return false;
        }
    }
    
    public static void RunSelfCheck()
    {
        // Test with a minimal valid structure
        var testJson = """
        {
            "products": [],
            "parts": [],
            "placedSheets": [],
            "hardware": [],
            "subassemblies": [],
            "optimizationResults": []
        }
        """;
        
        Console.WriteLine("Running self-check test...");
        bool result = VerifyJsonStructure(testJson);
        Console.WriteLine($"Self-check result: {(result ? "PASS" : "FAIL")}");
    }
}