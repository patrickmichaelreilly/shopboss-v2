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
    /// Determines if a part should be filtered to specialized racks (Doors & Fronts or Adjustable Shelves).
    /// Returns true if part should be diverted from standard carcass processing.
    /// </summary>
    public bool ShouldFilterPart(Part part)
    {
        var category = ClassifyPart(part);
        return category == PartCategory.DoorsAndDrawerFronts || category == PartCategory.AdjustableShelves;
    }

    /// <summary>
    /// Classifies a part into its appropriate category based on configurable keyword rules.
    /// Enhanced version of the existing classification logic with better extensibility.
    /// </summary>
    public PartCategory ClassifyPart(Part part)
    {
        var partName = part.Name.ToLowerInvariant();
        
        // Check for doors and drawer fronts
        if (ContainsKeywords(partName, _categoryKeywords[PartCategory.DoorsAndDrawerFronts]))
        {
            // Special logic for "panel" - only if combined with door/front context
            if (partName.Contains("panel"))
            {
                if (partName.Contains("door") || partName.Contains("front"))
                {
                    return PartCategory.DoorsAndDrawerFronts;
                }
            }
            else
            {
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

        // Default to carcass parts (sides, backs, tops, bottoms, etc.)
        return PartCategory.Carcass;
    }

    /// <summary>
    /// Gets the preferred rack type for a given part category.
    /// </summary>
    public RackType GetPreferredRackType(PartCategory category)
    {
        return category switch
        {
            PartCategory.DoorsAndDrawerFronts => RackType.DoorsAndDrawerFronts,
            PartCategory.AdjustableShelves => RackType.AdjustableShelves,
            PartCategory.Hardware => RackType.Hardware,
            PartCategory.Carcass => RackType.Standard,
            _ => RackType.Standard
        };
    }

    /// <summary>
    /// Filters a list of parts to only include carcass parts (excludes filtered parts for assembly readiness).
    /// Used for calculating assembly readiness - only carcass parts determine if a product is ready.
    /// </summary>
    public List<Part> GetCarcassPartsOnly(IEnumerable<Part> parts)
    {
        return parts.Where(part => !ShouldFilterPart(part)).ToList();
    }

    /// <summary>
    /// Gets parts that should be routed to specialized racks (doors, drawer fronts, adjustable shelves).
    /// </summary>
    public List<Part> GetFilteredParts(IEnumerable<Part> parts)
    {
        return parts.Where(ShouldFilterPart).ToList();
    }

    /// <summary>
    /// Gets detailed filtering information for a part including category and routing destination.
    /// </summary>
    public PartFilterInfo GetPartFilterInfo(Part part)
    {
        var category = ClassifyPart(part);
        var rackType = GetPreferredRackType(category);
        var isFiltered = ShouldFilterPart(part);

        return new PartFilterInfo
        {
            PartId = part.Id,
            PartName = part.Name,
            Category = category,
            PreferredRackType = rackType,
            IsFiltered = isFiltered,
            ProcessingStream = isFiltered ? "Specialized" : "Standard Carcass"
        };
    }

    /// <summary>
    /// Future extension point: Add new filtering keywords programmatically.
    /// Can be enhanced to persist to database or configuration files.
    /// </summary>
    public void AddCategoryKeyword(PartCategory category, string keyword)
    {
        if (!_categoryKeywords.ContainsKey(category))
        {
            _categoryKeywords[category] = new List<string>();
        }
        
        if (!_categoryKeywords[category].Contains(keyword.ToLowerInvariant()))
        {
            _categoryKeywords[category].Add(keyword.ToLowerInvariant());
            _logger.LogInformation("Added keyword '{Keyword}' to category {Category}", keyword, category);
        }
    }

    /// <summary>
    /// Future extension point: Remove filtering keywords.
    /// </summary>
    public bool RemoveCategoryKeyword(PartCategory category, string keyword)
    {
        if (_categoryKeywords.ContainsKey(category))
        {
            var removed = _categoryKeywords[category].Remove(keyword.ToLowerInvariant());
            if (removed)
            {
                _logger.LogInformation("Removed keyword '{Keyword}' from category {Category}", keyword, category);
            }
            return removed;
        }
        return false;
    }

    /// <summary>
    /// Gets all configured keywords for a category (for future admin interface).
    /// </summary>
    public List<string> GetCategoryKeywords(PartCategory category)
    {
        return _categoryKeywords.ContainsKey(category) 
            ? new List<string>(_categoryKeywords[category]) 
            : new List<string>();
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