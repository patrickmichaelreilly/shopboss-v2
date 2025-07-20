# Monday Tasks - Cross-Work Order Bin Details Fix

## Issue Summary
Orphaned parts in bins cause inconsistency in Bin Details Modal:
- Progress shows "N/50" (indicating parts in bin)
- Parts list shows "This bin is empty" 
- Clear Bin button was hidden (✅ FIXED - now always visible)

**Root Cause:** Parts list query filters by active work order only, but progress count includes all work orders.

## Simple Solution Plan ✅

### Task 1: Remove Work Order Filtering from Parts List
**File:** `src/ShopBoss.Web/Controllers/SortingController.cs` - `GetBinDetails` method

**Current Code (Line ~460):**
```csharp
var sortedParts = await _context.Parts
    .Include(p => p.NestSheet)
    .Where(p => p.Location == binLocation && 
               p.Status == PartStatus.Sorted &&
               p.NestSheet!.WorkOrderId == activeWorkOrderId)  // ← REMOVE THIS LINE
    .ToListAsync();
```

**Change To:**
```csharp
var sortedParts = await _context.Parts
    .Include(p => p.NestSheet)
    .Where(p => p.Location == binLocation && 
               p.Status == PartStatus.Sorted)
    .ToListAsync();
```

### Task 2: Add Cross-Work Order Detection to Bin Details
**File:** `src/ShopBoss.Web/Controllers/SortingController.cs` - `GetBinDetails` method

**Add after progress calculation (~line 480):**
```csharp
// Apply same cross-work order logic as rack view
var binStatus = bin.Status.ToString().ToLower();
var statusText = bin.Status.ToString();

if (!string.IsNullOrEmpty(bin.WorkOrderId) && 
    !string.IsNullOrEmpty(activeWorkOrderId) && 
    bin.WorkOrderId != activeWorkOrderId)
{
    binStatus = "blocked";
    statusText = "Blocked - Different Work Order";
}
```

**Update bin details response to use these values:**
```csharp
status = binStatus,         // instead of bin.Status.ToString().ToLower()
statusText = statusText,    // instead of bin.Status.ToString()
```

### Task 3: Add Red Background for Blocked Bins
**File:** `src/ShopBoss.Web/Views/Sorting/Index.cshtml` - `displayBinDetails` function

**Add CSS class application based on status:**
```javascript
// In displayBinDetails function, add after bin info display:
const modal = document.getElementById('binDetailModal');
if (bin.status === 'blocked' && bin.statusText === 'Blocked - Different Work Order') {
    modal.querySelector('.modal-content').style.backgroundColor = '#f8d7da'; // Light red
} else {
    modal.querySelector('.modal-content').style.backgroundColor = ''; // Reset
}
```

## Expected Results
- ✅ Parts list shows ALL parts in bin regardless of work order
- ✅ Progress count matches parts list count
- ✅ Modal background turns red for cross-work order bins (matching rack view blocked color)
- ✅ Clear Bin button always visible (already implemented)
- ✅ No additional complexity or new indicators

## Files to Modify
1. `src/ShopBoss.Web/Controllers/SortingController.cs` (GetBinDetails method)
2. `src/ShopBoss.Web/Views/Sorting/Index.cshtml` (displayBinDetails function)

## Estimated Time
1-2 hours - Very straightforward changes, minimal testing needed.

---

## Enum Refactoring Discussion - PartStatus → ProcessStatus

**Issue:** `PartStatus` enum is semantically incorrect - used by Products, Hardware, DetachedProducts, and NestSheets, not just Parts.

**Better Name:** `ProcessStatus` (captures manufacturing process flow)

**Decision:** **DEFERRED** - Medium-high risk refactoring for low value. Touches entire codebase with potential namespace conflicts. Save for future maintenance window.

**Related Fix Completed:** ✅ Shipping Station display issue resolved - shipped products now correctly show green status after page refresh via backend fixes to `MarkProductAsShippedAsync()`.