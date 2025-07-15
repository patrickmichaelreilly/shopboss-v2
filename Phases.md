# ShopBoss v2 Production Phases - Path to Beta

**Current State:** Phase M Complete - Manual override system fully functional  
**Strategy:** Billboard visibility → Hardware grouping → Final polish → Beta release

---

## COMPLETE!!! **Phase M: Manual Override Completion** 
*Status management, TreeView fixes, audit trails, and cascade logic all working*

**✅ Phase M1:** ModifyWorkOrder interface with TreeViewApi complete
**✅ Phase M1.x:** Migration crisis resolution complete  
**✅ Phase M2:** Status management cascade logic complete
**✅ Phase M3:** Integration testing complete

**Success:** All workflows function with Status enum, manual override system production ready

---

## **Phase B: Billboard Implementation & Error Visibility (HIGH PRIORITY)**
**Objective:** Make existing billboards always visible on Assembly and Sorting stations

**Target Files:**
- Frontend: Assembly.cshtml, Sorting.cshtml (only these two)
- Backend: AssemblyController.cs, SortingController.cs (only these two)
- Component: _BillboardMessage.cshtml (already exists and well-implemented)

**Tasks:**
1. **Remove Auto-Hide from Existing Billboard Component (15 min)**
   - _BillboardMessage.cshtml: Remove auto-hide parameter and logic
   - Remove close button from billboard actions
   - Ensure messages persist until replaced by new ones

2. **Remove Auto-Hide from Station Views (15 min)**
   - Assembly.cshtml: Remove any auto-hide JavaScript calls
   - Sorting.cshtml: Remove any auto-hide JavaScript calls  
   - Ensure billboard containers are always visible (no display:none)
   - Move billboard to display at bottom of view right above footer for now

3. **Basic Message Framework Enhancement (30 min)**
   - AssemblyController: Ensure billboard messages are sent properly
   - SortingController: Ensure billboard messages are sent properly
   - Simple message passing - no complex validation logic yet
   - Focus on framework, not detailed error scenarios

4. **Testing Framework (15 min)**
   - Verify messages appear and persist
   - Test message replacement functionality
   - Confirm no auto-hide behavior or close buttons

**Validation:**
- [ ] Assembly and Sorting stations show persistent billboard messages
- [ ] Messages retain until replaced by new ones
- [ ] No close buttons or auto-hide functionality
- [ ] Framework ready for future detailed error messages

**Dependencies:** Phase M complete

---

## **Phase H: Hardware Grouping Implementation (MEDIUM PRIORITY)**
**Objective:** Implement hardware consolidation in Shipping Station with dual quantity pattern handling

**Target Files:**
- Backend: ShippingController.cs, Hardware entity logic
- Frontend: Shipping.cshtml
- Services: Hardware grouping service

**Tasks:**
1. **Analyze Hardware Quantity Patterns (45 min)**
   - Document duplicated entities (Quantity = 1, multiple records)
   - Document single entities with Quantity > 1 attribute
   - Identify current import normalization behavior
   - Create test cases for both patterns

2. **Implement Hardware Grouping Logic (1.5 hours)**
   - Create service to handle both quantity patterns gracefully
   - Group by hardware type/description
   - Calculate total quantities correctly for both patterns
   - Handle edge cases (mixed patterns for same hardware type)

3. **Create Bundled Hardware UI (1 hour)**
   - Design hardware group display in Shipping Station
   - Show grouped hardware with total quantities
   - Add "mark all as shipped" functionality for bundles
   - Implement individual item override if needed

4. **Integration and Testing (45 min)**
   - Test with both quantity patterns extensively
   - Verify shipping operations work correctly
   - Test edge cases and mixed scenarios
   - Document pattern for potential Assembly Station use

**Validation:**
- [ ] Hardware groups correctly regardless of quantity pattern
- [ ] Total quantities calculated accurately
- [ ] Bundle shipping works for all hardware types
- [ ] Edge cases handled gracefully
- [ ] Pattern documented for future use

**Dependencies:** Phase B complete (for error visibility during testing)

---

## **Phase F: Final Polish & Beta Preparation (LOW PRIORITY)**
**Objective:** View-by-view cleanup and production readiness

### **F1: Work Order Management Polish (1 hour)**
- Remove star icon from Work Order name, create dedicated column
- Remove Work Order ID column from list view
- Remove ALL Microvellum branding from footer and elsewhere
- Add Work Order grouping capability (project phases)
- Add Shop Drawings storage capability to database

### **F2: Station-Specific Polish (4-5 hours)**

**CNC Station (1 hour):**
- Add Nest Sheet image to detail modal
- Add label printing capabilities to modal
- Add un-ProcessNestSheet button in modal
- Group and sort Nests by material
- Verify live status updates from other stations

**Sorting Station (1.5 hours):**
- Fix "Ready for Assembly" duplicate alerts issue
- Fix manual "Sort" buttons in Cut Parts Modal
- Increase grid/rack display size, remove column/row labels
- Improve bin indication colors (Grey/Yellow/Red/Green per spec)
- Add Empty Bin and Empty Rack buttons
- Add configurable filtering rules interface (doors, drawer fronts)

**Assembly Station (1 hour):**
- Decrease vertical size of list elements to reduce length
- Move completed items to end of list
- Remove Sorting Rack statistics box completely
- Add location guidance for filtered parts (doors/drawer fronts)

**Shipping Station (1 hour):**
- Recreate packing list appearance
- Add packing list print capability
- (Hardware grouping implemented in Phase H)

### **F3: Universal Components (1 hour)**
- Remove help button from scanner partial
- Add scan events to audit trail
- Improve audit log display (make manual changes less verbose)
- Add NAV-SORTING-RACK-X navigation commands
- Create command barcode sheet generator

### **F4: Import & Data Management (1.5 hours)**
- Handle repeat Work Order Names and IDs properly
- Update Import Preview to work exactly like Modify (specialized mode)
- Add capability to merge additional SDF data into existing Work Order
- Fix TreeView API style info location (move from view to API)

### **F5: System Management (1 hour)**
- Add cascade rules for non-empty rack deletion
- Improve rack configuration interface
- Test backup and restore functionality
- Verify archive operations

### **F6: Development Process (1 hour)**
- Slice and refactor bloated AdminController
- Compress Worklog.md for better maintainability
- Implement data checkpoints
- Clean up and document development workflow

**Validation:**
- [ ] Professional interface across all views
- [ ] All critical bugs from Improvements.md addressed
- [ ] Import system handles edge cases
- [ ] Backup/restore fully functional
- [ ] Code quality improvements complete

**Dependencies:** Phase H complete

---

## **Success Metrics for Beta**

### Functional Completeness
- ✅ All stations operational with manual override (Phase M complete)
- ✅ Complete workflow from Import to Shipping
- 🎯 Billboard error handling for common scenarios (Phase B)
- 🎯 Hardware grouping working perfectly (Phase H)
- 🎯 Professional UI across all views (Phase F)

### Error Visibility & Handling
- 🎯 Always-visible billboards showing informative messages
- 🎯 Comprehensive error messaging per Scans.md specification
- 🎯 Edge cases discoverable through billboard feedback
- 🎯 Graceful handling of dual hardware quantity patterns

### Production Readiness
- 🎯 No workflow-blocking UI issues
- 🎯 Consistent behavior across all stations
- 🎯 Complete audit trails including scan events
- 🎯 Beta deployment package ready

---

## **Timeline Estimate**

**Phase B:** 1.25 hours (billboard persistence + basic messaging)  
**Phase H:** 4 hours (hardware grouping with dual quantity handling)  
**Phase F:** 10 hours (comprehensive view-by-view polish)

**Total to Beta:** ~15.25 hours over 2-3 weeks

**Parallel work:** User testing edge cases during Phase H implementation

---

## **Risk Mitigation**

1. **Billboard visibility first**: Exposes real issues before final polish
2. **Hardware grouping isolated**: Prove pattern in Shipping before expanding
3. **Incremental delivery**: Each phase builds on previous success
4. **User feedback integration**: Edge case testing parallel to development
5. **Preserve all existing functionality**: No regression in working systems

---

This phases document prioritizes visibility and feedback (billboards) to drive discovery of real issues, implements hardware grouping carefully with dual quantity pattern support, and finishes with comprehensive polish. The focus is on:

1. **Making problems visible** through persistent billboard messaging
2. **Solving complex grouping** with careful attention to quantity patterns  
3. **Professional polish** across all views for beta readiness
4. **Maintaining stability** of the working Phase M foundation