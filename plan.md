# Bin Management & Sorting Rules Refactoring Plan

## Goals
1. Eliminate toxic string parsing for bin addressing
2. Enable custom bin naming for shop floor legibility
3. Create flexible, database-driven sorting rules
4. Remove unnecessary complexity (MaxCapacity, Row/Column)
5. Build new system in parallel, then swap it in

## Phase 1: Remove MaxCapacity Cruft
**Objective:** Strip out all capacity-related code that has no real-world meaning

**Changes:**
1. Remove `MaxCapacity` property from Bin model
2. Remove all capacity calculations and `capacityPercentage` logic
3. Update SortingController responses - remove maxCapacity/capacityPercentage
4. Update Views - remove progress bars and capacity-based UI elements
5. Clean up seeding code in StorageRackSeedService and AdminController

**Benefits:**
- Simplifies refactoring by removing unnecessary complexity
- Bins can't be "overfilled" in reality - this was artificial

**Risk:** Low - Removing unused functionality
**Time Estimate:** 1-2 hours

## Phase 2: Convert BinLabel to Direct Addressing
**Objective:** Make BinLabel a simple string property instead of computed from Row/Column

**Changes:**
1. Change `BinLabel` from computed property to regular string property
2. Keep Row/Column temporarily for backwards compatibility
3. Update seeding to populate BinLabel with auto-generated values like "A01", "B02"
4. Add simple text inputs in rack configuration to edit bin labels
5. Ensure BinLabel is unique within each rack

**Key Insight:** BinLabel serves dual purpose - unique identifier AND display name

**Note:** No need for separate DisplayName property - BinLabel is the display name

**Risk:** Medium - Core addressing system change, but keeping Row/Column as fallback
**Time Estimate:** 3-4 hours

## Phase 3: Keyword-Based Sorting Rules
**Objective:** Replace hard-coded sorting logic with simple keyword rules

**Database Schema:**
```csharp
public class SortingRule
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Priority { get; set; } // Lower = higher priority
    public string Keywords { get; set; } // Comma-separated keywords
    public RackType TargetRackType { get; set; }
    public bool IsActive { get; set; }
}
```

**Implementation:**
1. Create SortingRule model and DbSet
2. Update SortingRuleService to check if part name contains any keyword
3. Keep detached product detection separate (check Parts.Count == 1)
4. Seed default rules for current behavior

**Example Rules:**
- Priority 1: Keywords = "DOOR,DRAWER FRONT" → DoorsAndDrawerFronts
- Priority 2: Keywords = "ADJ SHELF,ADJUSTABLE" → AdjustableShelves
- Priority 3: Keywords = "HARDWARE,HINGE,SLIDE" → Hardware
- Default: No match → Standard

**Note:** Detached products (single-part) go to Cart rack - handled separately in code

**Risk:** Medium - New system architecture, but runs parallel to existing
**Time Estimate:** 4-5 hours

## Phase 4: Update Part Location Format
**Objective:** Store bin ID directly instead of location strings

**Changes:**
1. Add `BinId` foreign key to Part model (nullable string)
2. Update SortingRuleService: Set `part.BinId = bin.Id` (instead of Location)
3. Update AssemblyController: Use `part.BinId` to find bins directly
4. Keep `Location` property for legacy/display purposes only

**Before:**
```csharp
// Toxic string parsing
var locationParts = location.Split(':');
var rackName = locationParts[0];
var binCode = locationParts[1];
// Parse row/column from binCode...
```

**After:**
```csharp
// Direct bin lookup - no parsing, no helper methods
var bin = await _context.Bins
    .Include(b => b.StorageRack)
    .FindAsync(part.BinId);
```

**Risk:** High - Touches core part tracking logic, but easy to test
**Time Estimate:** 2-3 hours

## Phase 5: UI Improvements
**Objective:** Improve shop floor visibility

**Changes:**
1. Update Sorting Station grid to show BinLabel prominently
2. Increase font size for bin labels in grid display
3. Add tooltip showing full custom name if truncated
4. Update bin details modal to show custom label

**Risk:** Low - UI only changes
**Time Estimate:** 1-2 hours

## Phase 6: Clean Up & Delete Old Code
**Objective:** Remove all vestiges of the old system

**Final Cleanup:**
1. Remove Row/Column properties from Bin model
2. Remove Rows/Columns from StorageRack model
3. Delete all string parsing logic from AssemblyController
4. Remove computed BinLabel logic (now it's just a string)
5. Update any remaining references

**Risk:** Low - Removing dead code after new system is proven
**Time Estimate:** 1 hour


## Implementation Strategy

### Parallel Development Approach:
1. Keep existing system working throughout
2. Build new components alongside old ones
3. Test each phase independently
4. Only remove old code after new system is proven
5. No data migration needed (no existing production data)

### Testing Checklist:
- [ ] Sorting parts to bins works with new addressing
- [ ] Assembly station can find and clear bins
- [ ] Custom bin names display correctly
- [ ] Sorting rules evaluate correctly
- [ ] All audit trails maintain integrity
- [ ] SignalR updates work with new bin labels

## Benefits Summary
1. **No String Parsing:** Direct bin lookups by label
2. **Custom Naming:** User-defined bin labels for shop floor
3. **Flexible Rules:** Change sorting behavior without code changes
4. **Cleaner Code:** Single source of truth for bin addressing
5. **Better UX:** Large, readable bin labels on shop floor displays
6. **Future-Proof:** Easy to add new filter types or rule logic

## Total Estimated Time: 12-17 hours
- Can be implemented phase by phase
- Each phase can be tested independently
- Risk is managed by parallel development approach