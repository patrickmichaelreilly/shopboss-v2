using System.Text.RegularExpressions;

namespace ShopBoss.Web.Services;

/// <summary>
/// Service to parse Microvellum HTML label files
/// </summary>
public class LabelParserService
{
    private readonly ILogger<LabelParserService> _logger;
    
    public LabelParserService(ILogger<LabelParserService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Parse HTML label file and extract individual labels with their barcodes
    /// </summary>
    /// <param name="html">Complete HTML content from label file</param>
    /// <returns>Dictionary of barcode -> label HTML</returns>
    public Dictionary<string, string> ParseLabels(string html)
    {
        var labels = new Dictionary<string, string>();
        
        if (string.IsNullOrWhiteSpace(html))
        {
            _logger.LogWarning("Empty HTML provided to ParseLabels");
            return labels;
        }
        
        // Split by page separator
        var sections = html.Split("<!-- end page -->", StringSplitOptions.RemoveEmptyEntries);
        
        _logger.LogInformation("Found {Count} label sections in HTML", sections.Length);
        
        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section))
                continue;
                
            try
            {
                // Extract barcode from class="s82eeba28" elements
                var barcode = ExtractBarcode(section);
                
                if (!string.IsNullOrEmpty(barcode))
                {
                    // Clean the barcode (remove asterisks used in barcode font)
                    barcode = barcode.Trim('*').Trim();
                    
                    if (!labels.ContainsKey(barcode))
                    {
                        labels[barcode] = section.Trim();
                        _logger.LogDebug("Extracted label for barcode: {Barcode}", barcode);
                    }
                    else
                    {
                        _logger.LogWarning("Duplicate barcode found: {Barcode}", barcode);
                    }
                }
                else
                {
                    _logger.LogWarning("No barcode found in label section");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing label section");
            }
        }
        
        _logger.LogInformation("Successfully parsed {Count} labels", labels.Count);
        return labels;
    }
    
    /// <summary>
    /// Extract barcode from a label HTML section
    /// </summary>
    private string? ExtractBarcode(string labelHtml)
    {
        // Look for the barcode in class="s82eeba28" divs
        // Example: <div class="s82eeba28" ...>*258DN4UU4OJG1A2*</div>
        var match = Regex.Match(labelHtml, 
            @"class=""s82eeba28""[^>]*>([^<]+)</div>",
            RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        // Fallback: try to find any asterisk-wrapped text that looks like a barcode
        match = Regex.Match(labelHtml, @"\*([A-Z0-9]+)\*");
        if (match.Success)
        {
            return match.Value; // Return with asterisks for cleaning
        }
        
        return null;
    }
    
    /// <summary>
    /// Wrap individual label HTML with necessary document structure for standalone display
    /// </summary>
    public string WrapLabelForDisplay(string labelHtml, string originalDocumentStyles)
    {
        // Extract styles from original document if provided
        var styles = "";
        if (!string.IsNullOrEmpty(originalDocumentStyles))
        {
            var styleMatch = Regex.Match(originalDocumentStyles, 
                @"<style[^>]*>(.*?)</style>", 
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (styleMatch.Success)
            {
                styles = styleMatch.Groups[1].Value;
            }
        }
        
        // Normalize absolute positioning to start from top of container
        var normalizedLabelHtml = NormalizeAbsolutePositioning(labelHtml);
        
        // Build complete HTML document for individual label
        return $@"<!DOCTYPE HTML>
<html>
<head>
    <meta charset='utf-8'>
    <title>Part Label</title>
    <style type='text/css'>
        {styles}
        @media print {{
            body {{ margin: 0; }}
            .no-print {{ display: none; }}
        }}
    </style>
</head>
<body style='margin:0;'>
    <div class='StiPageContainer' style='width:290.62pt;height:75pt;position:relative;background-color:White;'>
        {normalizedLabelHtml}
    </div>
</body>
</html>";
    }
    
    /// <summary>
    /// Normalize absolute positioning by finding the minimum top value and subtracting it from all elements
    /// This ensures the label content starts at the top of the container regardless of original position
    /// </summary>
    private string NormalizeAbsolutePositioning(string labelHtml)
    {
        try
        {
            // Find all top values in style attributes using regex
            var topMatches = Regex.Matches(labelHtml, @"top\s*:\s*([0-9]+(?:\.[0-9]+)?)pt", RegexOptions.IgnoreCase);
            
            if (topMatches.Count == 0)
            {
                _logger.LogDebug("No top positioning found in label HTML");
                return labelHtml;
            }
            
            // Extract all top values and find the minimum
            var topValues = new List<decimal>();
            foreach (Match match in topMatches)
            {
                if (decimal.TryParse(match.Groups[1].Value, out decimal topValue))
                {
                    topValues.Add(topValue);
                }
            }
            
            if (topValues.Count == 0)
            {
                _logger.LogWarning("Could not parse any top values from label HTML");
                return labelHtml;
            }
            
            var minTop = topValues.Min();
            _logger.LogDebug("Found {Count} top values, minimum: {MinTop}pt", topValues.Count, minTop);
            
            // Replace all top values by subtracting the minimum offset
            var normalizedHtml = Regex.Replace(labelHtml, 
                @"top\s*:\s*([0-9]+(?:\.[0-9]+)?)pt", 
                match =>
                {
                    if (decimal.TryParse(match.Groups[1].Value, out decimal originalTop))
                    {
                        var normalizedTop = originalTop - minTop;
                        return $"top:{normalizedTop}pt";
                    }
                    return match.Value; // Return unchanged if parsing fails
                }, 
                RegexOptions.IgnoreCase);
                
            _logger.LogDebug("Successfully normalized absolute positioning with offset: {Offset}pt", minTop);
            return normalizedHtml;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error normalizing absolute positioning in label HTML");
            // Return original HTML if normalization fails
            return labelHtml;
        }
    }
}