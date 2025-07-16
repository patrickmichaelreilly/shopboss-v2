using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

/// <summary>
/// Standalone service for consolidating hardware items by name/description with quantity aggregation.
/// Handles both quantity patterns: duplicated entities (Qty=1, multiple records) and single entities (Qty>1).
/// </summary>
public class HardwareGroupingService
{
    private readonly ILogger<HardwareGroupingService> _logger;

    public HardwareGroupingService(ILogger<HardwareGroupingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Groups hardware items by name/description and aggregates quantities.
    /// Pure data transformation with no side effects.
    /// </summary>
    /// <param name="hardwareItems">Raw hardware items from work order</param>
    /// <returns>Grouped hardware with consolidated quantities</returns>
    public List<HardwareGroup> GroupHardwareByName(List<Hardware> hardwareItems)
    {
        try
        {
            if (!hardwareItems.Any())
            {
                return new List<HardwareGroup>();
            }

            // Group by Name - can easily change this line during testing iteration
            var groupedItems = hardwareItems
                .GroupBy(h => h.Name)
                .Select(group => new HardwareGroup
                {
                    Name = group.Key,
                    MicrovellumId = group.First().MicrovellumId,
                    TotalQuantity = group.Sum(h => h.Qty),
                    IndividualItems = group.ToList(),
                    Status = group.All(h => h.Status == PartStatus.Shipped) ? PartStatus.Shipped : PartStatus.Pending,
                    WorkOrderId = group.First().WorkOrderId
                })
                .OrderBy(g => g.Name)
                .ToList();

            _logger.LogDebug("Grouped {OriginalCount} hardware items into {GroupCount} groups", 
                hardwareItems.Count, groupedItems.Count);

            return groupedItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grouping hardware items");
            return new List<HardwareGroup>();
        }
    }


    /// <summary>
    /// Analyzes hardware quantity patterns for diagnostics.
    /// Useful for understanding data structure in work orders.
    /// </summary>
    /// <param name="hardwareItems">Hardware items to analyze</param>
    /// <returns>Pattern analysis results</returns>
    public HardwareQuantityPatternAnalysis AnalyzeQuantityPatterns(List<Hardware> hardwareItems)
    {
        try
        {
            var analysis = new HardwareQuantityPatternAnalysis();

            if (!hardwareItems.Any())
            {
                return analysis;
            }

            // Group by name to identify patterns
            var nameGroups = hardwareItems.GroupBy(h => h.Name);

            foreach (var nameGroup in nameGroups)
            {
                var items = nameGroup.ToList();
                
                if (items.Count > 1 && items.All(i => i.Qty == 1))
                {
                    // Duplicated entities pattern
                    analysis.DuplicatedEntityPatterns.Add(new HardwarePatternInfo
                    {
                        Name = nameGroup.Key,
                        Count = items.Count,
                        TotalQuantity = items.Sum(i => i.Qty),
                        Pattern = "Duplicated Entities (Qty=1, multiple records)"
                    });
                }
                else if (items.Count == 1 && items.First().Qty > 1)
                {
                    // Single entity pattern
                    analysis.SingleEntityPatterns.Add(new HardwarePatternInfo
                    {
                        Name = nameGroup.Key,
                        Count = items.Count,
                        TotalQuantity = items.First().Qty,
                        Pattern = "Single Entity (Qty>1)"
                    });
                }
                else
                {
                    // Mixed or other patterns
                    analysis.MixedPatterns.Add(new HardwarePatternInfo
                    {
                        Name = nameGroup.Key,
                        Count = items.Count,
                        TotalQuantity = items.Sum(i => i.Qty),
                        Pattern = "Mixed Pattern"
                    });
                }
            }

            analysis.TotalUniqueNames = nameGroups.Count();
            analysis.TotalHardwareItems = hardwareItems.Count;

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing hardware quantity patterns");
            return new HardwareQuantityPatternAnalysis();
        }
    }
}

/// <summary>
/// Represents a group of hardware items consolidated by name/description.
/// Contains both individual items and aggregated data.
/// </summary>
public class HardwareGroup
{
    public string Name { get; set; } = string.Empty;
    public string MicrovellumId { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public List<Hardware> IndividualItems { get; set; } = new();
    public PartStatus Status { get; set; } = PartStatus.Pending;
    public string WorkOrderId { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this group contains multiple individual items (duplicated entity pattern)
    /// </summary>
    public bool HasMultipleItems => IndividualItems.Count > 1;

    /// <summary>
    /// Indicates if this group uses single entity pattern (one item with Qty > 1)
    /// </summary>
    public bool IsSingleEntityPattern => IndividualItems.Count == 1 && IndividualItems.First().Qty > 1;

    /// <summary>
    /// Gets the primary hardware item for scanning purposes
    /// </summary>
    public Hardware PrimaryItem => IndividualItems.First();

    /// <summary>
    /// Gets all hardware IDs in this group for bulk operations
    /// </summary>
    public List<string> AllHardwareIds => IndividualItems.Select(i => i.Id).ToList();
}

/// <summary>
/// Analysis results for hardware quantity patterns in a work order.
/// Useful for understanding data structure and debugging.
/// </summary>
public class HardwareQuantityPatternAnalysis
{
    public List<HardwarePatternInfo> DuplicatedEntityPatterns { get; set; } = new();
    public List<HardwarePatternInfo> SingleEntityPatterns { get; set; } = new();
    public List<HardwarePatternInfo> MixedPatterns { get; set; } = new();
    public int TotalUniqueNames { get; set; }
    public int TotalHardwareItems { get; set; }
}

/// <summary>
/// Information about a specific hardware quantity pattern.
/// </summary>
public class HardwarePatternInfo
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int TotalQuantity { get; set; }
    public string Pattern { get; set; } = string.Empty;
}