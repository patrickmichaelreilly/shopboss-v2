# ShopBoss v2 Production Phases - Path to Beta

**Current State:** Phase M1 in progress, core functionality complete  
**Strategy:** Complete current work, fix critical bugs, polish for beta, defer major refactoring

---

## **Phase M: Manual Override Completion (3-4 hours + station migrations)**
*Finish what we started - includes targeted refactoring*

### **M1: Complete ModifyWorkOrder Interface with TreeViewApi (2-3 hours)**
**Replace old ModifyWorkOrder with the current StatusManagement panel (renamed to ModifyWorkOrder)**

**Context:** The work-in-progress StatusManagement panel will become the new ModifyWorkOrder interface. This involves renaming StatusManagement to ModifyWorkOrder while completing its implementation with TreeViewApi, proper UI, and audit visualization.

**Tasks:**
1. **Complete TreeViewApi Integration**
   - Keep Claude Code's TreeViewApi integration (no modifications to TreeViewApi)
   - Ensure individual status dropdowns work on each tree item
   - Remove any lingering checkbox-related code
   - Remove the entire yellow bulk operations block

2. **Migrate UI from Old ModifyWorkOrder**
   - Add Work Order header section (Name, ID, Import Date, total counts)
   - Bring over Statistics Cards (Products, Parts, Hardware, Detached Products, Nest Sheets)
   - Match styling and layout from current Modify Work Order interface
   - Create new WorkOrderStatisticsController API to reduce AdminController bloat

3. **Implement Audit Trail Visualization**
   - Replace bulk operations block with "Audit History" section
   - Display ALL audit records for current work order
   - Show: Timestamp, Entity name/type, Property changed, Old→New values, User/Station
   - Format JSON values into human-readable display
   - Structure for future undo capability (but no undo implementation yet)

4. **Clean Integration with Preferred Naming**
   - Rename StatusManagement.cshtml → ModifyWorkOrder.cshtml
   - Keep ModifyWorkOrder action name in AdminController
   - Maintain familiar /Admin/ModifyWorkOrder/{id} URLs
   - Remove old ModifyWorkOrder implementation completely
   - Update ModifyWorkOrderUnified redirect to point to new ModifyWorkOrder action

**Deliverables:**
- ✅ ModifyWorkOrder interface with full UI (header, stats cards, tree, audit history)
- ✅ Working individual status management via TreeViewApi
- ✅ Comprehensive audit trail visualization
- ✅ New WorkOrderStatisticsController reducing AdminController size
- ✅ Clean naming: "Modify Work Order" throughout (not "Status Management")
- ✅ Foundation ready for M2 business logic and future undo

### **M1.5: Database Migration for Status Unification (1 hour)**
**Add Status fields to all entities, migrate data, but keep old properties temporarily**

**Context:** First step of status unification - database preparation without breaking existing code.

**Tasks:**
1. **Database Migration Only**
   - Add Status and StatusUpdatedDate columns to Hardware, DetachedProducts, NestSheets
   - Add StatusUpdatedDate to Products (already has Status)
   - Migrate existing data:
     - Hardware/DetachedProducts: IsShipped → Status
     - Products: IsCompleted → Status (ensure consistency)
     - NestSheets: IsProcessed → Status
   - Keep all old columns for now (IsShipped, IsCompleted, IsProcessed)

2. **Model Updates**
   - Add Status properties to models but keep old properties
   - Add computed properties for transition:
     ```csharp
     public string Status { get; set; } = "Pending";
     public bool IsShipped => Status == "Shipped"; // Keep for compatibility
     ```

**Deliverables:**
- ✅ Database ready with Status columns for all entities
- ✅ Data migrated to new Status fields
- ✅ Old code continues to work via computed properties
- ✅ Foundation ready for station-by-station migration

### **M1.6: CNC Station Status Migration (1 hour)**
**Migrate CNC station from IsProcessed to Status**

**Claude Code Instructions:** Search entire CNC station codebase for ALL occurrences of IsProcessed and ProcessedDate

**Known Locations to Update:**
1. **CncController.cs**
   - Line ~150: `nestSheet.IsProcessed = true;`
   - Line ~151: `nestSheet.ProcessedDate = DateTime.UtcNow;`
   - ValidateBarcode method checking `IsProcessed`
   - Change to: `nestSheet.Status = "Processed";`

2. **Views/Cnc/Index.cshtml**
   - Check for any `Model.NestSheets` displaying IsProcessed
   - Update progress calculations

3. **Views/Cnc/_NestSheetModal.cshtml** (if exists)
   - Update any IsProcessed displays

**Testing Checklist:**
- [ ] Scan nest sheet marks Status as "Processed"
- [ ] Already processed sheets show correct message
- [ ] Nest sheet list displays correct status
- [ ] Parts on sheet marked as "Cut"

### **M1.7: Assembly Station Status Migration (1.5 hours)**
**Migrate Assembly station from IsCompleted to Status for both Products and DetachedProducts**

**Claude Code Instructions:** Search ENTIRE Assembly station for IsCompleted, also check DetachedProduct handling

**Known Locations to Update:**
1. **AssemblyController.cs**
   - StartAssembly method: `product.IsCompleted = true;`
   - ScanPartForAssembly: Sets IsCompleted
   - GetProductDetails: Returns IsCompleted
   - Any progress calculations using IsCompleted
   - Change to check: `product.Status == "Shipped"`
   - Ensure DetachedProducts are NOT processed in Assembly (they skip this station)

2. **Views/Assembly/Index.cshtml**
   - Product cards showing completion status
   - JavaScript updating IsCompleted after scan
   - Progress calculations
   - Verify DetachedProducts don't appear

3. **SignalR Notifications**
   - ProductAssembledByScan sends IsCompleted
   - Update to send Status instead

**Testing Checklist:**
- [ ] Product assembly sets Status to "Shipped"
- [ ] DetachedProducts remain unaffected by Assembly
- [ ] UI shows correct completion status
- [ ] SignalR updates work correctly
- [ ] Progress calculations accurate

### **M1.8: Shipping Station Status Migration (2 hours)**
**Migrate Shipping station from IsShipped to Status - most complex**

**Claude Code Instructions:** Search for IsShipped, ShippedDate - this station has the most updates needed

**Known Locations to Update:**
1. **ShippingController.cs**
   - ScanProduct: Product shipping logic
   - ScanHardware: `hardware.IsShipped = true;`
   - ScanDetachedProduct: `detachedProduct.IsShipped = true;`
   - ScanPart: May check product completion
   - ShipProduct/ShipHardware/ShipDetachedProduct methods

2. **ShippingService.cs**
   - UpdateHardwareStatusAsync(bool isShipped)
   - UpdateDetachedProductStatusAsync(bool isShipped)
   - GetShippingStatusAsync checking IsShipped
   - Status classes with IsShipped properties

3. **Views/Shipping/Index.cshtml**
   - All `@if (item.IsShipped)` checks
   - JavaScript checking shipped status
   - Progress calculations

**Testing Checklist:**
- [ ] All scan methods update Status correctly
- [ ] UI displays shipped/ready status properly
- [ ] Progress circle shows accurate counts
- [ ] Manual ship buttons work

### **M1.9: Final Cleanup and Old Property Removal (1 hour)**
**Remove all obsolete properties after all stations migrated**

**Tasks:**
1. **Final Database Migration**
   - Drop columns: IsShipped, ShippedDate, IsCompleted, IsProcessed, ProcessedDate
   - Remove computed properties from models

2. **Global Search and Verify**
   - Search entire codebase for removed properties
   - Ensure no references remain
   - Update any missed locations

3. **Comprehensive Testing**
   - Test complete workflow: Import → CNC → Sorting → Assembly → Shipping
   - Verify WorkOrderStatistics API
   - Check ModifyWorkOrder status dropdowns

**Deliverables:**
- ✅ All boolean status properties removed
- ✅ Clean Status-based architecture
- ✅ All stations fully migrated and tested

### **M2: Status Management Business Logic (2 hours)**
**Add validation and cascading logic to Status Management Panel**

**Tasks:**
1. Implement status transition validation rules
2. Add cascade operations (un-process nest → revert parts)
3. Add confirmation dialogs for destructive operations
4. Test SignalR updates across all stations
5. Polish UI with proper icons and tooltips

**Deliverables:**
- ✅ Smart status management with validation
- ✅ Data integrity protection
- ✅ Professional user experience
- ✅ Real-time updates working

### **M3: Integration Testing (1 hour)**
**Ensure Manual Override system is production ready**

**Tasks:**
1. Test status changes cascade properly
2. Verify audit trail captures all changes
3. Test undo functionality (if implemented)
4. Ensure SignalR updates work across stations
5. Document any limitations or known issues

**Deliverables:**
- ✅ Manual override fully functional
- ✅ All edge cases handled
- ✅ Ready for production use

---

## **Phase B: Critical Bug Fixes (4-5 hours)**
*Address high-priority issues from Bugs.md*

### **B1: Station-Critical Fixes (2 hours)**
1. **Sorting Station**: Fix duplicate "Ready for Assembly" alerts
2. **Sorting Station**: Fix manual "Sort" buttons in Cut Parts Modal
3. **Assembly Station**: Remove Sorting Rack statistics box completely
4. **Universal Scanner**: Remove help button
5. **Rack Configuration**: Add cascade rules for non-empty rack deletion

### **B2: Import & Data Fixes (2 hours)**
1. **Import Process**: Handle duplicate Work Order Names/IDs properly
2. **Hardware Grouping**: Combine identical hardware in Assembly/Shipping stations
3. **Work Order List**: Fix star icon column layout (dedicated column, remove from name)
4. **Navigation**: Remove ALL Microvellum branding from footer and elsewhere

### **B3: UI Polish from Bugs.md (1 hour)**
1. **Sorting Station**: Add billboard area, improve grid visualization
2. **Sorting Station**: Increase rack grid size, remove labels, better color coding
3. **Assembly Station**: Decrease vertical size of list items
4. **Shipping Station**: Implement bundled hardware shipping

---

## **Phase E: Error Handling & Messaging (3-4 hours)**
*Production-ready error management*

### **E1: Error Matrix Development (1 hour)**
**Define all error scenarios and responses**

Create comprehensive error handling matrix for:
- Scanner errors (invalid barcode, network issues)
- Status transition violations
- Data integrity issues
- Concurrent user conflicts
- Part already scanned/sorted scenarios

### **E2: Billboard Implementation (2 hours)**
**Persistent error display for critical stations**

Implement billboard messaging for:
- Sorting Station (part already scanned, bin full, wrong rack)
- Assembly Station (missing parts, wrong sequence)
- CNC Station (nest sheet already processed)
- Clear recovery instructions for each error

### **E3: Toast & Notification Polish (1 hour)**
**Temporary notifications and success feedback**

- Success confirmations with appropriate duration
- Warning toasts for non-critical issues
- Info notifications for system events
- Consistent styling and positioning

---

## **Phase U: UI Polish & Production Readiness (4-5 hours)**
*Final interface refinements - partially complete*

### **U1: Complete Billboard Integration (1 hour)**
**Wire up the billboard messages that were created but not fully integrated**

**Tasks:**
1. **Sorting Station Billboard Integration**
   - Wire up "Part already scanned" errors to billboard
   - Show "Bin full" warnings in billboard
   - Display "Wrong rack selected" messages
   - Show "Ready for Assembly" notifications

2. **Assembly Station Billboard Integration**
   - Wire up "Missing parts" errors to billboard
   - Show "Wrong assembly sequence" warnings
   - Display "Product completed" success messages
   - Show location guidance in billboard when appropriate

3. **Billboard Controller Methods**
   - Add billboard message parameters to existing error responses
   - Ensure proper message types (success, warning, danger, info)
   - Test persistent vs auto-hide scenarios

**Deliverables:**
- ✅ Billboard shows actual station messages (not empty)
- ✅ Errors and warnings appear prominently
- ✅ Success messages celebrate completions
- ✅ Messages persist appropriately based on importance

### **U2: Station-Specific Polish (2 hours)**
Complete the unfinished UI polish tasks:
- Fix CNC progress calculations
- Smart rack defaults in Sorting (never show empty rack)
- Polish assembly queue visualization
- Enhanced shipping checklist interface

### **U3: Scanner Navigation Commands (1 hour)**
- Implement NAV-SORTING-RACK-X pattern
- Create command barcode sheet generator
- Test navigation flows across all stations
- Document all scanner commands

### **U4: Final Visual Polish (1 hour)**
- Consistent button sizing across all stations
- Proper spacing and alignment
- Mobile/tablet optimization verification
- Loading states and progress indicators

---

## **Phase V: Validation & Beta Release (2-3 hours)**
*Final testing and deployment preparation*

### **V1: Integration Testing (1.5 hours)**
- Complete workflow testing (Import → Ship)
- Performance validation with 1000+ parts
- Multi-user concurrent testing
- Archive/backup operation verification
- Scanner command testing

### **V2: Beta Package Creation (1.5 hours)**
- Self-contained deployment package
- Installation documentation
- Scanner command reference sheets
- Quick start guides for each station
- Beta feedback collection setup

---

## **Success Metrics for Beta**

### Functional Completeness
- ✅ All stations operational with manual override
- ✅ Complete workflow from Import to Shipping
- ✅ Error handling for common scenarios
- ✅ Scanner-first navigation working

### Bug Resolution
- ✅ All critical bugs from Bugs.md fixed
- ✅ No workflow-blocking issues
- ✅ Consistent UI behavior
- ✅ No data integrity issues

### Production Readiness
- ✅ Backup/restore fully functional
- ✅ Health monitoring operational
- ✅ Complete audit trails
- ✅ Beta deployment package ready

---

## **Post-Beta Phase R: Architecture Refactoring**
*To be addressed after successful beta deployment*

### **Future R1: Controller Decomposition**
- Extract Status Management endpoints to dedicated controller
- Extract Backup/Archive endpoints to DataManagementController
- Split bloated SortingController
- Target: Controllers under 300 lines each

### **Future R2: Code Quality**
- Run dotnet format on entire codebase
- Add .editorconfig with standards
- Set up pre-commit hooks
- Compress Worklog.md

### **Future R3: Technical Debt**
- Enable proper EF migrations
- Add basic test infrastructure
- Improve error handling patterns
- Performance optimizations

---

## **Timeline Estimate**

**Week 1:** Complete Phase M + High Priority Bugs (Phase B1-B2)  
**Week 2:** Error Handling + Remaining Bugs (Phase E + B3)  
**Week 3:** UI Polish + Testing (Phase U + V1)  
**Week 4:** Beta Release Prep (Phase V2)

**Total to Beta:** ~18-22 hours over 4 weeks

**Post-Beta:** Architecture refactoring as time permits

---

## **Risk Mitigation**

1. **Focus on beta**: Defer non-critical refactoring
2. **Test thoroughly**: Each bug fix verified before moving on
3. **Incremental delivery**: Complete phases before starting new ones
4. **User feedback**: Early beta testing to catch issues

---

This phases document prioritizes getting to beta with a stable, functional system. Major refactoring is deferred until after beta success is confirmed. The focus is on:

1. **Completing current work** (Phase M with targeted AdminController cleanup)
2. **Fixing user-facing bugs** that would block beta adoption
3. **Adding essential error handling** for production use
4. **Polish and testing** for professional deployment
5. **Deferring major refactoring** until post-beta