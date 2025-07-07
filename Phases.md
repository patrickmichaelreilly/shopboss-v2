# ShopBoss V2 Refactoring Plan: Phase 6 Implementation

**Date:** July 7, 2025  
**Objective:** Rebuild Modify Work Order interface using Import Preview foundation

## Design Reference
**CRITICAL:** The unified interface must visually match the current Import Preview interface. Reference the attached screen.jpg for exact layout, styling, and component placement. The Import Preview design is proven and should be preserved with only minimal adjustments for status management features.

## Unified Interface Modes

### Import Mode (Import Preview - Existing Functionality)
- **Purpose:** Select products/parts for import into new work order
- **Tree Interaction:** Checkboxes for selection (Select All, Clear All)
- **Data State:** Preview data from SDF file (not yet saved to database)
- **Actions:** Import selected items
- **Statistics:** Show counts of selected vs available items
- **Navigation:** Return to import workflow after confirmation
- **REMOVE:** CSV Export button and all related export functionality

### Modify Mode (New Implementation)
- **Purpose:** Manage existing work order items and their statuses
- **Tree Interaction:** Status dropdowns on each item (Pending, Cut, Sorted, etc.)
- **Data State:** Live work order data from database
- **Actions:** Bulk status updates, individual status changes, real-time SignalR updates
- **Statistics:** Show counts by status (Cut, Sorted, Assembled, etc.)
- **Navigation:** Integrate with existing work order management workflow

### Shared Foundation
- **Visual Design:** Identical layout, styling, tree structure, and statistics bar
- **Tree Component:** Same JavaScript component with mode parameter
- **API Backend:** Same data loading with mode-specific fields
- **Performance:** Same optimization for large datasets (1000+ items)

## Core Principles
- Maximum 2-3 files per step
- Side-by-side validation before migration
- Clear rollback plans
- Performance preservation
- Maintain Import Preview visual design

---

## Phase 6A: API Architecture Setup
**Risk:** LOW - No user-facing changes

**Tasks:**
1. Create `Controllers/Api/WorkOrderTreeApiController.cs`
2. Create `Models/Api/TreeDataModels.cs` 
3. Implement `GetTreeData(workOrderId, includeStatus)` endpoint using existing WorkOrderService

**Success Criteria:**
- API returns data identical to Import Preview structure
- Performance matches existing endpoints
- No impact on current functionality

**Testing Instructions:**
- Build and deploy using deploy-to-windows.sh
- Test API endpoint directly (provide URL)
- Verify JSON structure matches Import Preview data

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6A completion notes and any issues discovered

---

## Phase 6B: JavaScript Component Foundation  
**Risk:** LOW - Standalone component

**Tasks:**
1. Extract Import Preview tree logic to `wwwroot/js/WorkOrderTreeView.js`
2. Create `wwwroot/css/tree-view.css` with generalized styling
3. Build test harness page for component validation

**Success Criteria:**
- Test page renders identically to Import Preview
- Component handles 1000+ items smoothly
- All visual elements preserved

**Testing Instructions:**
- Access test harness page (provide URL)
- Compare visual output with Import Preview
- Test with large dataset

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6B completion notes and any issues discovered

---

## Phase 6B2: Subassembly Quantity Normalization Fix
**Risk:** MEDIUM - Core data processing changes

**Tasks:**
1. Implement subassembly quantity normalization in `ImportDataTransformService.cs`
2. Apply two-phase processing to subassemblies in `ImportSelectionService.cs`
3. Add recursive multiplication for nested subassemblies and their contents

**Success Criteria:**
- Subassembly with Qty=2 in Product with Qty=3 creates 6 total subassembly instances
- Parts within subassemblies multiply correctly (part qty × subassembly qty × product qty)
- Hardware within subassemblies multiply correctly 
- Nested subassemblies handle multi-level quantity multiplication

**Testing Instructions:**
- Import SDF with multi-quantity products containing multi-quantity subassemblies
- Verify subassembly counts in tree component test harness
- Check that subassembly parts/hardware show correct total quantities
- Test nested subassembly scenarios

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6B2 completion notes and any issues discovered

---

## Phase 6C: Parallel Interface Creation
**Risk:** MEDIUM - New interface alongside existing

**Tasks:**
1. Add `ModifyWorkOrderUnified(string id)` action to AdminController
2. Create `Views/Admin/ModifyWorkOrderUnified.cshtml`
3. Implement status management and SignalR integration

**Success Criteria:**
- Functional parity with existing Modify interface
- All status management features work
- Real-time updates function

**Testing Instructions:**
- Navigate to new unified interface (provide URL)
- Test all status change operations
- Verify SignalR real-time updates work

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6C completion notes and any issues discovered

---

## Phase 6C2: Hardware Tree Integration & Breadcrumb Fix
**Risk:** MEDIUM - Tree structure expansion

**Tasks:**
1. [DONE] Fix work order name loading issue in breadcrumb navigation
2. Modify `WorkOrderTreeApiController` to include hardware in product nodes
3. Update tree data models to handle hardware children under products
4. Enhance `WorkOrderTreeView.js` to render and manage hardware nodes
5. Implement consistent PartStatus handling for hardware (same enum, different valid transitions)

**Success Criteria:**
- Breadcrumb shows correct work order name
- Hardware items appear nested under their parent products in tree
- Hardware uses PartStatus enum with appropriate workflow transitions
- Both Import and Modify modes display hardware properly
- No regression in existing parts/subassemblies functionality

**Testing Instructions:**
- Verify breadcrumb displays work order name correctly
- Navigate to unified interface and confirm hardware appears under products
- Test hardware status changes in modify mode
- Verify import mode still shows hardware for selection
- Confirm bulk operations work with mixed parts/hardware selection

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6C2 completion notes and any issues discovered

---

## Phase 6D: Migration & Cleanup
**Risk:** HIGH - User-facing changes

**Tasks:**
1. Update existing `ModifyWorkOrder` route to unified interface
2. Archive old view files with clear naming
3. Remove obsolete controller methods and unused view models

**Success Criteria:**
- Seamless transition for users
- No broken functionality
- Clean codebase

**Testing Instructions:**
- Verify all existing workflows still function
- Check all navigation links work
- Confirm no console errors
- Test with large work order for performance validation

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6D completion notes and any issues discovered

---

## Phase 6D2: Fix ModifyWorkOrderUnified Layout & API Consistency
**Risk:** MEDIUM - User-facing layout changes and API architecture corrections

**Tasks:**
1. Revert WorkOrderTreeApiController to simple TreeDataResponse format (remove complex statistics)
2. Add missing DetachedProducts as third top-level category alongside Products and Nest Sheets
3. Copy Import Preview layout structure to ModifyWorkOrderUnified (container, Work Order Info section, Bootstrap statistics cards)
4. Ensure both APIs return identical TreeDataResponse format for true unified architecture
5. Calculate simple statistics client-side like Import Preview does

**Success Criteria:**
- ModifyWorkOrderUnified uses identical layout/styling as Import Preview
- WorkOrderTreeApiController returns simple TreeDataResponse like ImportController
- DetachedProducts appear as separate category in tree
- Both APIs follow parallel architecture (session-based vs database-based)
- TreeView component handles mode differences transparently

**Testing Instructions:**
- Verify ModifyWorkOrderUnified has centered container and Work Order Info section
- Confirm statistics cards use same Bootstrap styling as Import Preview
- Check DetachedProducts appear in tree structure
- Test both import and modify modes use same TreeView component seamlessly
- Validate API responses have identical structure between Import and WorkOrder endpoints

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6D2 completion notes and any issues discovered

---

## Phase 6D3: Statistics UI Improvements & DetachedProducts Architecture Fix
**Risk:** MEDIUM - UI formatting improvements and import architecture changes

**Tasks:**
1. Fix Modify view statistics cards formatting by replacing complex grid layout with stacked list format
2. Move DetachedProducts filtering from ImportSelectionService to ImportDataTransformService 
3. Implement single-part product detection logic in TransformToImportWorkOrder() method
4. Update statistics calculation to reflect DetachedProducts moved during transformation
5. Remove duplicate DetachedProducts filtering logic from ImportSelectionService
6. Ensure Import Preview shows correct DetachedProducts count and category

**Success Criteria:**
- Modify view statistics cards display cleanly without text wrapping or layout issues
- Import Preview shows accurate DetachedProducts count in statistics card
- DetachedProducts category appears in Import tree view when items exist
- Single-part products are consistently filtered at transform time rather than selection time
- End-to-end Import → Preview → Conversion flow maintains DetachedProducts correctly

**Implementation Details:**
- Replace nested row/column grid in statistics cards with simple stacked divs
- Apply same stacked layout to all 5 statistics cards (Products, Parts, DetachedProducts, Hardware, NestSheets)
- Move `singlePartProducts.Where(p => p.Parts.Count == 1)` logic from ImportSelectionService to ImportDataTransformService
- Create ImportDetachedProduct instances and populate workOrder.DetachedProducts during transformation
- Update CalculateStatistics() to count DetachedProducts correctly
- Remove ProcessSinglePartProductsAsDetached() from ImportSelectionService

**Testing Instructions:**
- Verify Modify view statistics cards show status breakdowns cleanly on separate lines
- Import SDF file and confirm DetachedProducts count shows correctly in Import Preview
- Confirm DetachedProducts category appears in Import tree view
- Test complete import flow to ensure DetachedProducts work consistently through Preview → Conversion
- Verify no duplicate DetachedProducts are created during selection processing

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6D3 completion notes and any issues discovered

---

## Phase 6E: Station Performance Optimization
**Risk:** MEDIUM - Multiple station performance improvements

**Tasks:**
1. Optimize Admin work order list queries in `WorkOrderService.GetWorkOrderSummariesAsync()`
2. Migrate Assembly Station to unified API with 'view' mode
3. Migrate Shipping Station to unified API or optimized endpoints
4. Remove cartesian product Include chains from all station loading

**Success Criteria:**
- Admin work order list loads < 2 seconds with 50+ work orders
- Assembly Station loads < 3 seconds for large work orders
- Shipping Station loads < 3 seconds for large work orders
- All stations use optimized split-query architecture

**Testing Instructions:**
- Test Admin index with many work orders for fast loading
- Test Assembly Station with large work order (1000+ parts)
- Test Shipping Station with large work order
- Verify no memory leaks during extended sessions

**Post-Completion:**
- Follow Collaboration_Guidelines.md for git commit and documentation
- Update Worklog.md with Phase 6E completion notes and any issues discovered

---

## Rollback Procedures

**Immediate Rollback:**
1. Revert route changes to original interface
2. Restore archived files if needed

**Each Phase Includes:**
- Clear commit with phase identifier
- Worklog.md documentation
- Specific testing instructions for human validation