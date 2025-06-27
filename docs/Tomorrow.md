# Plan to Fix Sorting Station UI Update Issues

## Issues Identified:

### 1. **Rack Count Updates Not Working**
- The rack tab badges show "occupiedBins/totalBins" but these are computed properties
- The `OccupiedBins` property depends on `Bins.Count(b => b.IsOccupied)` which isn't updating in real-time
- Need to refresh the entire page or rack list to see updated counts

### 2. **Cut Parts Count Not Decreasing**
- The "Cut Parts" button shows a static count from page load (`ViewBag.CutPartsCount`)
- This count doesn't update when parts are successfully sorted from Cut ’ Sorted status
- Need real-time updates via SignalR or page refresh triggers

### 3. **Inconsistent Bin Display Updates**
- First sort works because it triggers `loadRackDetails()` which refreshes the grid
- Subsequent sorts may not trigger updates if:
  - SignalR connection isn't working properly
  - The `currentRackId` comparison fails
  - The bin data isn't being properly updated in the backend

### 4. **SignalR Integration Issues**
- The SignalR `PartSorted` event should trigger UI refreshes
- Need to verify the rack ID matching logic
- Need to ensure both manual scan and SignalR events trigger the same updates

## Proposed Solutions:

### 1. **Fix Rack Tab Badge Updates**
- Add SignalR event handler to refresh rack badges when parts are sorted
- Update the rack occupancy counts in real-time
- Alternative: Trigger full page refresh or rack list reload

### 2. **Fix Cut Parts Count Updates**
- Add SignalR handler to decrement cut parts count when part is sorted
- Update the button text dynamically: "Cut Parts (X)" where X decreases
- Reload cut parts modal data when it's opened (ensure fresh data)

### 3. **Enhance Bin Display Consistency**
- Ensure `loadRackDetails()` is called reliably after successful sorts
- Add fallback refresh mechanisms if SignalR fails
- Verify the rack ID comparison logic in both manual and SignalR updates
- Add logging to track update flow

### 4. **Improve SignalR Reliability**
- Add error handling and retry logic for SignalR updates
- Ensure consistent data flow between manual scans and real-time events
- Add visual feedback when SignalR connection is lost/restored

## Implementation Steps:

1. **Update SignalR handlers** to refresh rack badges and cut parts count
2. **Add rack list refresh function** that updates occupancy badges
3. **Enhance cut parts count management** with real-time updates
4. **Improve error handling** and fallback mechanisms
5. **Add debugging/logging** to track update flow
6. **Test thoroughly** with multiple part scans to ensure consistency

## Expected Outcome:
- Rack badges update immediately when parts are sorted
- Cut parts count decreases in real-time
- Bin visualization consistently updates for all sorts
- Robust fallback mechanisms if SignalR fails

## Technical Details:

### Current Code Analysis:

**Issue in SortingController.Index():**
```csharp
// Static count calculated at page load only
ViewBag.CutPartsCount = cutParts.Count;
```

**Issue in Sorting View rack badges:**
```html
<!-- Static badges from page load -->
<span class="badge bg-@rackTypeColor ms-2">@rack.OccupiedBins/@rack.TotalBins</span>
```

**Issue in SignalR PartSorted handler:**
```javascript
// Only refreshes current rack, doesn't update badges or cut count
if (currentRackId === data.rackId) {
    loadRackDetails(data.rackId);
}
```

### Solutions to Implement:

**1. Add new SignalR event handlers:**
```javascript
connection.on("PartSorted", function (data) {
    // Existing rack refresh
    if (currentRackId === data.rackId) {
        loadRackDetails(data.rackId);
    }
    
    // NEW: Update rack badges
    updateRackBadge(data.rackId);
    
    // NEW: Decrement cut parts count
    decrementCutPartsCount();
    
    // Show success toast
    showToast(`Part '${data.partName}' sorted to ${data.binLabel}`, 'success');
});
```

**2. Add rack badge update function:**
```javascript
function updateRackBadge(rackId) {
    fetch(`/Sorting/GetRackOccupancy/${rackId}`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const badge = document.querySelector(`#rack-${rackId}-tab .badge`);
                if (badge) {
                    badge.textContent = `${data.occupiedBins}/${data.totalBins}`;
                }
            }
        });
}
```

**3. Add cut parts count management:**
```javascript
let currentCutPartsCount = @ViewBag.CutPartsCount;

function decrementCutPartsCount() {
    currentCutPartsCount = Math.max(0, currentCutPartsCount - 1);
    updateCutPartsButton();
}

function updateCutPartsButton() {
    const button = document.querySelector('[onclick="loadCutParts()"]');
    if (button) {
        button.innerHTML = `<i class="fas fa-list me-2"></i>Cut Parts (${currentCutPartsCount})`;
    }
}
```

**4. Add new controller endpoint:**
```csharp
public async Task<IActionResult> GetRackOccupancy(string id)
{
    var rack = await _sortingRules.GetRackWithBinsAsync(id);
    if (rack == null) {
        return Json(new { success = false });
    }
    
    return Json(new { 
        success = true, 
        occupiedBins = rack.OccupiedBins,
        totalBins = rack.TotalBins 
    });
}
```

## Testing Strategy:

1. **Test rack badge updates** - Sort parts to different racks and verify badges update
2. **Test cut parts count** - Verify count decreases with each successful sort
3. **Test bin visualization** - Ensure consistent bin updates across multiple sorts
4. **Test SignalR reliability** - Verify updates work even with network interruptions
5. **Test error scenarios** - Ensure fallback mechanisms work when SignalR fails

This comprehensive plan addresses all the identified UI update issues and provides specific implementation details for each fix.