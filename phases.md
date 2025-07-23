# Rack Type Consolidation Project

## Overview
Eliminate `Hardware` and `AdjustableShelves` rack types, merge them with `DoorsAndDrawerFronts` into a single "Special" rack type. Result: 3 rack types (Standard, Special, Mobile).

## Phase 1: Model Updates

### Phase 1.1: Update Enums
**Target Files:**
- `src/ShopBoss.Web/Models/StorageRack.cs`
- `src/ShopBoss.Web/Models/Part.cs`

**Tasks:**
1. Update `RackType` enum: Remove `Hardware(3)` and `AdjustableShelves(2)`, rename `DoorsAndDrawerFronts(1)` to `Special(1)`
2. Keep `Standard(0)` and `Cart(4)` unchanged
3. Update `PartCategory` enum: Remove `Hardware` and `AdjustableShelves`, rename `DoorsAndDrawerFronts` to `Special`
4. Keep `Standard` unchanged

**Validation:**
- [ ] Build succeeds without errors
- [ ] All enum references updated correctly

## Phase 2: Part Filtering Logic Updates

### Phase 2.1: Merge Classification Logic
**Target Files:**
- `src/ShopBoss.Web/Services/PartFilteringService.cs`

**Tasks:**
1. Merge two category keyword dictionaries (`DoorsAndDrawerFronts`, `AdjustableShelves`) into single `Special` category
2. Remove `Hardware` category completely (hardware parts are not sortable/scannable)
3. Update classification logic to return `PartCategory.Special` for doors, drawer fronts, and adjustable shelves
4. Update `PreferredRackType` assignments to use `RackType.Special`

**Validation:**
- [ ] Build succeeds without errors
- [ ] Part classification accurately identifies doors, fronts, and adjustable shelves as Special
- [ ] Standard parts remain unaffected
- [ ] No references to Hardware category remain

## Phase 3: Sorting Logic Updates

### Phase 3.1: Update Progress Calculations
**Target Files:**
- `src/ShopBoss.Web/Controllers/SortingController.cs`

**Tasks:**
1. Update `CalculateBinProgressAsync()` switch statement: Remove `AdjustableShelves` and `Hardware` cases
2. Rename `DoorsAndDrawerFronts` case to `Special`
3. Update progress calculation to track all special parts (doors, fronts, adjustable shelves) together
4. Update progress type description to "special parts"

**Validation:**
- [ ] Build succeeds without errors
- [ ] Bin progress calculations work correctly for Special rack type
- [ ] Progress percentages accurately reflect special parts completion

### Phase 3.2: Update Sorting Rules
**Target Files:**
- `src/ShopBoss.Web/Services/SortingRuleService.cs`

**Tasks:**
1. Update rack filtering logic: Change references from `RackType.DoorsAndDrawerFronts` to `RackType.Special`
2. Ensure standard parts still avoid Special racks with updated exclusion logic
3. Test rack selection logic with new enum values

**Validation:**
- [ ] Build succeeds without errors
- [ ] Standard parts are not assigned to Special racks
- [ ] Special parts are correctly assigned to Special racks

## Phase 4: UI Updates

### Phase 4.1: Update Sorting Station UI
**Target Files:**
- `src/ShopBoss.Web/Views/Sorting/Index.cshtml`

**Tasks:**
1. Update icon mappings: Remove `Hardware` and `AdjustableShelves`, rename `DoorsAndDrawerFronts` to `Special`
2. Choose appropriate icon for Special type (suggest `fas fa-star` or `fas fa-magic`)
3. Update color mappings: Use single color for Special type
4. Update all dropdown and button logic
5. Update switch statements for rack type display

**Validation:**
- [ ] Sorting station displays correctly with 3 rack types
- [ ] Icons and colors are appropriate for Special rack type
- [ ] Dropdown functionality works correctly

### Phase 4.2: Update Admin UI
**Target Files:**
- `src/ShopBoss.Web/Views/Admin/CreateRack.cshtml`
- `src/ShopBoss.Web/Views/Admin/EditRack.cshtml`

**Tasks:**
1. Update rack type selection dropdowns to show only 3 options
2. Update validation and labels
3. Update any help text or descriptions
4. Test rack creation and editing workflow

**Validation:**
- [ ] Rack creation shows only Standard, Special, and Cart options
- [ ] Rack editing works correctly with new enum values
- [ ] Form validation works properly

## Phase 5: Update Seeding Service

### Phase 5.1: Update Default Racks
**Target Files:**
- `src/ShopBoss.Web/Services/StorageRackSeedService.cs`

**Tasks:**
1. Remove `AdjustableShelves` rack creation from seed data
2. Remove any `Hardware` rack creation from seed data  
3. Update existing `DoorsAndDrawerFronts` rack to use `Special` type
4. Update rack name from "Doors & Fronts Rack" to "Special Parts Rack"
5. Update rack description to reflect combined purpose

**Validation:**
- [ ] Seed data creates only Standard, Special, and Cart racks
- [ ] New installations work correctly with 3 rack types
- [ ] Rack names and descriptions are appropriate
- [ ] No references to old rack types remain

## Phase 6: Additional Updates

### Phase 6.1: Update All References
**Target Files:**
- All files with switch statements on RackType or PartCategory
- All UI text and labels
- Documentation files

**Tasks:**
1. Search for all references to old enum values
2. Update switch statements throughout codebase
3. Update UI text mentioning "Doors & Fronts", "Hardware", or "Adjustable Shelves"
4. Update any documentation or help text
5. Update statistical reporting or analytics code

**Validation:**
- [ ] No compiler errors or warnings
- [ ] All UI text is consistent and appropriate
- [ ] All functionality works with new enum values

## Phase 7: Testing and Validation

### Phase 7.1: Comprehensive Testing
**Tasks:**
1. Test part classification accuracy for doors, fronts, and adjustable shelves
2. Test sorting workflow with new Special rack type
3. Confirm progress calculations work correctly
4. Validate UI shows correct rack types and icons
5. Test seeding service creates correct default racks
6. Test with various work order scenarios

**Validation:**
- [ ] All parts are classified correctly
- [ ] Sorting workflow functions properly
- [ ] Progress indicators are accurate
- [ ] UI is intuitive and functional
- [ ] Seeding creates appropriate default racks
- [ ] Performance is not degraded

## Key Benefits
- **Simplified sorting**: All "finish assembly" parts go to same bins
- **Reduced complexity**: 3 rack types instead of 5
- **Better workflow**: Parts that are assembled together are stored together
- **Elimination of unused Hardware rack type**

## Risk Mitigation
- **Test with sample data** to ensure part classification still works correctly  
- **Verify existing development data** works with new enum values
- **Incremental rollout** with ability to rollback if issues arise
- **Clear testing plan** to validate all functionality

## Dependencies
- Phase 1 must complete before Phase 2
- Phase 2 must complete before Phase 3  
- Phase 4 can run in parallel with Phase 3
- Phase 5 (seeding) can run in parallel with Phase 4
- Phase 6 (additional updates) should run after Phases 1-5
- Phase 7 (testing) validates all previous phases