# SmartSheet Import Streamlining Plan

## Current State Analysis
- **SmartSheetService.cs**: 1,083 lines (OAuth, API calls, import/migration logic all mixed together)
  - NO caching code (already removed in previous phase)
  - Just needs to be split into logical services
- **SmartSheetMigrationController**: Working perfectly with multi-step UI
- **Modal implementation**: Half-working (auth detection fixed, but using wrong endpoints for job loading)

## Problems to Fix
1. Modal trying to call non-existent endpoint `/SmartSheet/GetWorkspaceJobs`
2. SmartSheetService is a monolithic 1,083 line file doing too many things
3. Unnecessary code duplication - we already have working endpoints

## Clean Solution

### Phase 1: Fix the Modal (Minimal Changes)
1. **Add one method to SmartSheetMigrationController**
   ```csharp
   [HttpGet]
   public async Task<IActionResult> GetActiveJobs()
   ```
   - Returns only Active Jobs workspace sheets as JSON
   - ~10 lines of code max
   - Reuses existing `GetAccessibleWorkspacesAsync()`

2. **Fix JavaScript in smartsheet.js**
   - Change `loadActiveJobs()` to call `/SmartSheetMigration/GetActiveJobs`
   - Keep using existing `/SmartSheetMigration/ImportProject` for import
   - Remove all the complex HTML parsing code I added

3. **Test the streamlined flow**
   - Button � Modal � Auth check � Load Active Jobs � One-click import

### Phase 2: Clean Up (After Modal Works)
1. **Delete old migration UI**
   - `/Views/SmartSheetMigration/Index.cshtml` (206 lines)
   - `/wwwroot/js/smartsheet-migration.js` (300+ lines)
   - Keep the controller - it has our endpoints

### Phase 3: Clean Up SmartSheetService (Current Task)
Goal: Remove ~283 lines of redundancy to get under 900 lines total

**Actions:**
1. **Remove duplicate/redundant methods**
2. **Remove unused code/commented code**  
3. **Remove overly verbose logging**
4. **Consolidate similar functionality**
5. **Target: Get from 1,083 lines to under 900 lines**

If we can trim the fat, SmartSheetService becomes manageable again - no need for complex refactoring.

## Implementation Order
1. Fix modal endpoints (10 minutes)
2. Test streamlined import flow
3. Clean up SmartSheetService redundancies (get under 900 lines)
4. Delete old UI files

## Success Criteria
-  One-click import from modal
-  No intermediate confirmation steps
-  Reuses all existing working code
-  Minimal new code added
-  Sets foundation for future refactoring