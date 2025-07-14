# ShopBoss v2 Data Import Refinement - Multi-Prompt Work Plan
**Date**: Thursday, June 19, 2025  
**Project**: Shop Floor Part Tracking System  
**Focus**: Data mapping fixes and selective import implementation

---

ShopBoss Entity Structure Sketch:::


Workorder
   Product (All part properties)
      Hardware (All hardware properties)
      Parts (All part properties, what nest sheet did it come from?, what product does it belong to?)
      Subassemblies (All subassembly properties, is it nested or standard?, to what product does it belong?)
         Hardware (All hardware properties)
         Parts (All part properties, what nest sheet did it come from?, what subassembly does it belong to?)
         Nested Subassemblies (All subassembly properties, is it nested or standard?, to what parent subassembly does it belong?)
            Hardware (All hardware properties)
            Parts (All part properties, what nest sheet did it come from?, what subassembly does it belong to?)
   Nested sheets (what parts are associated?, material, dimensions, etc.)




## Project Context Summary

- **Current State**: Phase 3 partially complete - import workflow functional with correct data mapping
- **Phase 1 & 2 Complete**: Column mapping service implemented, hierarchy logic working, tree view displaying correct relationships
- **Recent Fix**: Product display names now show [ItemNumber] - [Product Name] - Qty. [Quantity] format correctly
- **External Tool**: x86 Importer wrapper (Erik's converter + Sqlite3) triggered via web UI
- **Data Quality**: No warnings in terminal, preview counts accurate, tree view hierarchy working
- **Helpful notes**: All the SDFs will be well-formed with the same tables and columns.
---

## Work Plan - Remaining Focused Phases (~1 hour each)

### ✅ COMPLETED: Phase 1 & 2 - Data Structure Analysis & Import Foundation
**Status**: COMPLETED  
**Achievement**: Column mapping service, hierarchy logic, tree view with correct product display

#### Completed Deliverables:
- ✅ Column mapping translation layer (ColumnMappingService.cs)
- ✅ Updated transformation logic with real SDF column names
- ✅ Correct parent-child relationships and accurate preview counts
- ✅ Tree view displaying proper hierarchical relationships
- ✅ Product display format: [ItemNumber] - [Product Name] - Qty. [Quantity]
- ✅ No terminal warnings, clean data processing

---

### Phase 3A: Tree Selection Logic Enhancement
**Duration**: ~1 hour  
**Deliverable**: Enhanced tree view with smart selection logic

#### Detailed Tasks:
1. **Enhance Tree Selection JavaScript**
   - Implement parent-child selection dependencies (selecting parent auto-selects children)
   - Add selection validation (prevent child selection without parent)
   - Implement visual feedback for selected/partially selected nodes
   - Add "Select All Products" and "Clear All" buttons

2. **Create Selection State Management**
   - Build JavaScript selection tracking for tree hierarchy
   - Maintain selected item counts by type (products, parts, subassemblies, hardware)
   - Update preview statistics to show selected vs. total counts
   - Validate selection completeness before allowing confirmation

3. **Add Selection Validation Feedback**
   - Show warnings for incomplete product selections
   - Highlight dependency issues in tree view
   - Provide clear messaging about selection requirements
   - Enable/disable confirm button based on selection validity

#### Success Criteria:
- [ ] Tree view selection works with parent-child validation
- [ ] Selection state properly tracked and displayed
- [ ] Clear user feedback for selection issues
- [ ] Confirm button enabled only for valid selections

---

### Phase 3B: Import Selection Service & Data Conversion
**Duration**: ~1 hour  
**Deliverable**: Backend service for processing selected items

#### Detailed Tasks:
1. **Create ImportSelectionService**
   - Build service to process selected tree items from frontend
   - Convert import models to ShopBoss database entities (WorkOrder, Product, Part, etc.)
   - Handle parent-child relationship mapping during conversion
   - Implement selection filtering (only process selected items)

2. **Add Controller Endpoint for Selection Processing**
   - Create POST endpoint to receive selected item data from frontend
   - Validate selection data and dependencies
   - Call ImportSelectionService to process conversion
   - Return success/error feedback to frontend

3. **Implement Entity Relationship Handling**
   - Ensure foreign key relationships are properly set during conversion
   - Handle WorkOrder → Product → Parts/Subassemblies → Hardware hierarchy
   - Preserve Microvellum IDs exactly as imported
   - Set up proper entity navigation properties

#### Success Criteria:
- [ ] ImportSelectionService converts import models to database entities
- [ ] Controller endpoint processes selection data correctly
- [ ] Entity relationships properly established
- [ ] Selected items ready for database persistence

---

### Phase 4: Database Persistence & Duplicate Detection
**Duration**: ~1 hour  
**Deliverable**: Complete database import with conflict resolution

#### Detailed Tasks:
1. **Implement Database Transaction Logic**
   - Execute atomic transaction for all selected entities
   - Handle rollback on any failure during import
   - Ensure data consistency across all related entities
   - Implement proper error handling and logging

2. **Add Duplicate Detection System**
   - Check existing Microvellum IDs in database before import
   - Identify potential conflicts with existing work orders
   - Provide user options: Skip, Replace, or Cancel on duplicates
   - Handle partial duplicates (some products exist, others don't)

3. **Create Import Confirmation UI**
   - Show final confirmation dialog with import summary
   - Display duplicate warnings and resolution options
   - Allow user to review what will be imported before final commit
   - Provide clear feedback during database save process

#### Success Criteria:
- [ ] Database transactions work atomically
- [ ] Duplicate detection prevents data conflicts
- [ ] Users can resolve duplicate scenarios
- [ ] Import confirmation provides clear feedback

---

### Phase 5: Import Summary & Audit Trail
**Duration**: ~1 hour  
**Deliverable**: Complete import tracking and reporting system

#### Detailed Tasks:
1. **Create Import Summary Reports**
   - Generate detailed report of what was successfully imported
   - Include statistics: products, parts, subassemblies, hardware counts
   - Show any warnings or errors encountered during import
   - Display import completion time and user information

2. **Implement Import History & Audit Trail**
   - Track what was imported, when, and by whom
   - Store import session details and results in database
   - Create import history view for administrators
   - Enable import session review and troubleshooting

3. **Add Post-Import Navigation**
   - Redirect to imported work order after successful import
   - Provide links to view imported work order details
   - Add breadcrumb navigation back to import process
   - Enable users to start new import or view work orders list

#### Success Criteria:
- [ ] Import summary provides comprehensive feedback
- [ ] Audit trail captures all import activity
- [ ] Import history accessible for review
- [ ] Post-import workflow is intuitive

---

### Phase 6: Testing, Validation & Polish
**Duration**: ~1 hour  
**Deliverable**: Production-ready import system

#### Detailed Tasks:
1. **End-to-End Testing**
   - Test complete workflow from upload to database storage
   - Verify all selection scenarios and validation rules
   - Test error conditions and recovery mechanisms
   - Confirm system works with various SDF file structures

2. **Performance & UX Optimization**
   - Optimize tree view rendering for large hierarchies
   - Ensure acceptable response times for typical imports
   - Improve user feedback and loading indicators
   - Add keyboard shortcuts and accessibility features

3. **Documentation & Code Quality**
   - Update inline documentation for all new services
   - Clean up debugging code and temporary implementations
   - Ensure code follows project standards and conventions
   - Create user guide for import process

#### Success Criteria:
- [ ] Complete workflow tested and verified
- [ ] Performance acceptable for production use
- [ ] Code is clean, documented, and maintainable
- [ ] System ready for production deployment

---

## Key Technical Focus Areas

### Data Mapping
- Translate actual SDF columns to expected model properties
- Handle column name variations and missing fields
- Provide robust fallback logic

### Hierarchy Construction
- Build correct parent-child relationships using real link IDs
- Handle complex nested structures (max 2 levels)
- Prevent circular references and orphaned records

### Data Validation
- Ensure counts match between SDF and preview
- Validate all parent-child relationships
- Detect and handle duplicate records

### Selection Logic
- Smart tree selection with parent-child dependencies
- Independent selection for hardware and detached products
- Clear validation feedback for users

### Database Persistence
- Atomic transactions with proper error handling
- Duplicate detection and conflict resolution
- Comprehensive audit trail and reporting

---

## Overall Success Criteria

- [x] Preview counts match actual SDF data (no inflation)
- [x] Tree view displays correct hierarchical relationships
- [x] Product display shows [ItemNumber] - [Product Name] - Qty. [Quantity] format
- [ ] Selection logic works with parent-child validation
- [ ] Database import completes successfully with audit trail
- [ ] All error scenarios handled gracefully
- [ ] Performance acceptable for large SDF files (up to 100MB)
- [ ] Complete documentation and handoff materials ready

---

## Notes for Implementation

### Column Mapping Priority
Based on SDF Data Analysis.csv, focus on these key columns:
- **Products**: LinkID, LinkIDWorkOrder, ItemNumber, WorkOrderName
- **Subassemblies**: LinkID, LinkIDParentProduct, LinkIDParentSubassembly, LinkIDWorkOrder
- **Parts**: LinkID, LinkIDProduct, LinkIDSubAssembly, LinkIDWorkOrder
- **Hardware**: LinkID, LinkIDProduct, LinkIDWorkOrder

### Performance Considerations
- Process only relevant columns (not all SDF columns)
- Use efficient data structures for hierarchy building
- Implement streaming for very large imports

### Error Handling Strategy
- Graceful degradation when optional data is missing
- Clear error messages for users
- Detailed logging for developers
- Rollback capability for failed transactions

---

*This plan chunks the work into focused 1-hour sessions to avoid timeouts while maintaining momentum toward a fully functional import system.*