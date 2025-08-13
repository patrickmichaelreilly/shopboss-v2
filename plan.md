# ShopBoss Part Labels - Fixes and Enhancements

## Issues Identified

### 1. Work Order Deletion Not Using Proper Cascading
- `AdminController.DeleteWorkOrder()` only deletes WorkOrder entity directly
- Does NOT use existing `WorkOrderDeletionService` that properly cascades to all children
- Results in orphaned Parts, PartLabels, and other child entities

### 2. Part Label Matching Fails Due to ID Suffixing  
- Labels stored with original barcode: `PartId = "PART123"`
- Parts created with suffixed IDs: `Id = "PART123_1"` (for quantity expansion)
- `GetPartLabel()` searches for exact match: `l.PartId == partId` 
- No match found → "Label not available" errors

### 3. Label Absolute Positioning Issue (FIXED but not yet tested by User)
- ✅ Labels displayed with excessive whitespace from original composite sheet positioning
- ✅ Fixed by normalizing absolute positioning in `LabelParserService.WrapLabelForDisplay()`

## Implementation Plan

### Phase 1: Fix Work Order Deletion
**Modify AdminController to use WorkOrderDeletionService:**
1. Add `WorkOrderDeletionService` to AdminController constructor
2. Replace direct EF deletion with comprehensive service-based deletion
3. Handle both individual and bulk delete operations
4. Ensure all child entities (Parts, PartLabels, Products, etc.) are properly removed

### Phase 2: Implement Elegant 3-Tier Label Assignment During Import
**Use pool consumption pattern (mirrors existing nest sheet assignment logic):**

**Key Insights:**
- Microvellum already generates individual labels for each part instance (natural 1:1 correspondence)
- Edge case: Multiple quantity product with same parts on same nest sheet
- Need pool consumption to prevent duplicate assignments

**3-Tier Assignment Strategy:**
1. **Easy Match**: Single part for barcode → Direct assignment
2. **NestSheet Disambiguation**: Multiple parts but NestSheetId narrows to 1 → Direct assignment  
3. **Pool Consumption**: Multiple parts with same barcode AND NestSheetId → Take first available, remove from pool

### Phase 3: Simplify Label Lookup (After Import Fix)
**Since each part gets its own PartLabel during import:**
1. Simple exact matching: `l.PartId == partId` (no more complex logic needed)
2. Optional NestSheetId validation for additional context

## Solution Code Snippets

### 3-Tier Label Assignment During Import (Pool Consumption Pattern)
```csharp
// In WorkOrderImportService.ImportLabelsAsync()
var assignedPartIds = new HashSet<string>(); // Track consumed parts

foreach (var label in parsedLabels)
{
    var barcode = label.Key.Trim('*'); // Clean barcode
    var labelHtml = label.Value;
    
    // Tier 1: Easy match - single part for barcode
    var matchingParts = await _context.Parts
        .Where(p => (p.Id == barcode || ExtractOriginalPartId(p.Id) == barcode) && 
                   p.WorkOrderId == workOrderId &&
                   !assignedPartIds.Contains(p.Id))
        .ToListAsync();
    
    if (matchingParts.Count == 1)
    {
        CreatePartLabel(matchingParts[0], labelHtml, workOrderId);
        assignedPartIds.Add(matchingParts[0].Id);
        continue;
    }
    
    // Tier 2: NestSheet disambiguation (if we can extract nest sheet context from label)
    if (TryExtractNestSheetFromLabel(labelHtml, out var nestSheetId))
    {
        var nestSheetMatch = matchingParts.FirstOrDefault(p => p.NestSheetId == nestSheetId);
        if (nestSheetMatch != null)
        {
            CreatePartLabel(nestSheetMatch, labelHtml, workOrderId);
            assignedPartIds.Add(nestSheetMatch.Id);
            continue;
        }
    }
    
    // Tier 3: Pool consumption - take first available
    var firstAvailable = matchingParts.FirstOrDefault();
    if (firstAvailable != null)
    {
        CreatePartLabel(firstAvailable, labelHtml, workOrderId);
        assignedPartIds.Add(firstAvailable.Id);
    }
}
```

### Simplified Label Lookup (After Import Fix)
```csharp
// In CncController.GetPartLabel() - Simple exact match since import creates 1:1
var label = await _context.PartLabels
    .FirstOrDefaultAsync(l => l.PartId == partId && l.WorkOrderId == activeWorkOrderId);
```

### Work Order Deletion Fix
```csharp
// Replace AdminController.DeleteWorkOrder() method
private readonly WorkOrderDeletionService _workOrderDeletionService;

public async Task<IActionResult> DeleteWorkOrder(string id)
{
    // Use comprehensive deletion service instead of direct EF removal
    var result = await _workOrderDeletionService.DeleteWorkOrderAsync(id);
    
    if (result.Success)
        TempData["SuccessMessage"] = result.Message;
    else
        TempData["ErrorMessage"] = result.Message;
        
    return RedirectToAction(nameof(Index));
}
```

## Files to Modify
1. `Controllers/AdminController.cs` - Use WorkOrderDeletionService for proper cascading
2. `Services/WorkOrderDeletionService.cs` - Add comprehensive work order deletion method
3. `Services/WorkOrderImportService.cs` - Implement 3-tier label assignment with pool consumption
4. `Models/PartLabel.cs` - Add optional NestSheetId foreign key for context
5. `Data/ShopBossDbContext.cs` - Add NestSheetId relationship configuration

## Expected Results
- ✅ Work orders fully delete all child entities when deleted
- ✅ Part labels display correctly for both original and suffixed part IDs  
- ✅ Reimporting work orders works cleanly without orphaned data
- ✅ Label buttons work for quantity-expanded parts