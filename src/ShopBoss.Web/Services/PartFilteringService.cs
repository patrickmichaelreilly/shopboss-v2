using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

/// <summary>
/// Extensible service for filtering parts based on configurable rules.
/// Designed to support future expansion with additional keywords, regex patterns, or database-driven configuration.
/// </summary>
public class PartFilteringService
{
    private readonly ILogger<PartFilteringService> _logger;
    
    // Configurable keyword rules - can be easily extended or moved to database/config in future
    private readonly Dictionary<PartCategory, List<string>> _categoryKeywords = new()
    {
        {
            PartCategory.DoorsAndDrawerFronts, 
            new List<string>
            {
                "door",
                "drawer front",
                "panel" // when combined with door/front keywords
            }
        },
        {
            PartCategory.AdjustableShelves,
            new List<string>
            {
                "adjustable shelf",
                "adjustable",
                "shelf"
            }
        },
        {
            PartCategory.Hardware,
            new List<string>
            {
                "hinge",
                "handle", 
                "knob",
                "screw",
                "bracket"
            }
        }
    };

    public PartFilteringService(ILogger<PartFilteringService> logger)
    {
        _logger = logger;
    }


    /// <summary>
    /// Classifies a part into its appropriate category based on configurable keyword rules.
    /// Enhanced version of the existing classification logic with better extensibility.
    /// </summary>
    public PartCategory ClassifyPart(Part part)
    {
        var partName = part.Name.ToLowerInvariant();
        
        _logger.LogDebug("Classifying part: '{PartName}' -> '{NormalizedName}'", part.Name, partName);
        
        // Check for doors and drawer fronts
        var doorKeywords = _categoryKeywords[PartCategory.DoorsAndDrawerFronts];
        var containsDoorKeywords = ContainsKeywords(partName, doorKeywords);
        
        _logger.LogDebug("Part '{PartName}' contains door keywords: {ContainsDoorKeywords} (keywords: {Keywords})", 
            partName, containsDoorKeywords, string.Join(", ", doorKeywords));
        
        if (containsDoorKeywords)
        {
            // Special logic for "panel" - only if combined with door/front context
            if (partName.Contains("panel"))
            {
                if (partName.Contains("door") || partName.Contains("front"))
                {
                    _logger.LogDebug("Part '{PartName}' classified as DoorsAndDrawerFronts (panel with door/front)", partName);
                    return PartCategory.DoorsAndDrawerFronts;
                }
            }
            else
            {
                _logger.LogDebug("Part '{PartName}' classified as DoorsAndDrawerFronts (direct match)", partName);
                return PartCategory.DoorsAndDrawerFronts;
            }
        }

        // Check for adjustable shelves
        if (ContainsKeywords(partName, _categoryKeywords[PartCategory.AdjustableShelves]))
        {
            // Enhanced logic for adjustable shelves
            if (partName.Contains("adjustable") || 
                (partName.Contains("shelf") && partName.Contains("adj")))
            {
                return PartCategory.AdjustableShelves;
            }
        }

        // Check for hardware
        if (ContainsKeywords(partName, _categoryKeywords[PartCategory.Hardware]))
        {
            return PartCategory.Hardware;
        }

        // Default to standard parts (sides, backs, tops, bottoms, etc.)
        _logger.LogDebug("Part '{PartName}' classified as Standard (default)", partName);
        return PartCategory.Standard;
    }



    
    private bool ContainsKeywords(string text, List<string> keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Detailed information about part filtering and routing decisions.
/// </summary>
public class PartFilterInfo
{
    public string PartId { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public PartCategory Category { get; set; }
    public RackType PreferredRackType { get; set; }
    public bool IsFiltered { get; set; }
    public string ProcessingStream { get; set; } = string.Empty;
}