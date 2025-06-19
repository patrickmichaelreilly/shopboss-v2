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

- **Current State**: Phase 2 infrastructure complete - import workflow functional but data mapping incorrect
- **Core Issue**: SDF column names don't match expected names (ProductId, SubassemblyId, etc.), causing hierarchy problems
- **Evidence**: 946 raw CSV rows producing inflated counts in preview (duplicates/wrong relationships)
- **External Tool**: x86 Importer wrapper (Erik's converter + Sqlite3) triggered via web UI
- **SDF Analysis Available**: `SDF Data Analysis.csv` shows actual table structure and required columns
- **Helpful notes**: All the SDFs will be well-formed with the same tables and columns.
---

## Work Plan - 5 Focused Phases (~1 hour each)

### Phase 1: Data Structure Analysis & Column Mapping Discovery
**Duration**: ~1 hour  
**Deliverable**: Column mapping translation layer and updated transformation logic

#### Detailed Tasks:
1. **Analyze SDF Data Analysis.csv**
   - Map actual column names from each table (Products, Parts, Subassemblies, Hardware, etc.)
   - Identify relationship columns (LinkID, LinkIDProduct, LinkIDSubAssembly, etc.)
   - Document column name translation requirements

2. **Create Column Mapping Service**
   - Build ColumnMappingService.cs to translate actual SDF columns to expected model properties
   - Handle variations in column naming across different SDF versions
   - Provide fallback logic for optional columns

3. **Update ImportDataTransformService**
   - Replace hardcoded column names (ProductId, SubassemblyId) with actual SDF column names
   - Update GetStringValue calls to use real column names from mapping service
   - Fix hierarchy construction logic to use correct parent-child relationship columns

4. **Test Updated Transformation**
   - Run import with sample SDF file to verify correct data extraction
   - Validate that hierarchy relationships are now properly constructed
   - Confirm preview counts are reasonable and match expectations

#### Success Criteria:
- [ ] All actual SDF column names documented and mapped
- [ ] ImportDataTransformService uses real column names
- [ ] No more "empty column" warnings in transformation logs
- [ ] Preview data shows realistic counts and proper hierarchy

---

### Phase 2: Hierarchy Logic Refinement & Validation
**Duration**: ~1 hour  
**Deliverable**: Correct parent-child relationships and accurate preview counts

#### Detailed Tasks:
1. **Implement Systematic Parsing Order**
   - Products first (root level items using Products table)
   - Subassemblies linked to products via LinkIDProduct/LinkIDParentProduct
   - Nested subassemblies differentiated from regular subassemblies
   - Parts and hardware associated with correct parents using LinkID fields

2. **Fix Relationship Linking Logic**
   - Use actual LinkID column names from SDF analysis
   - Implement proper parent-child association using real relationship fields
   - Handle cases where relationship fields may be null or empty

3. **Add Data Validation**
   - Verify no orphaned records (children without valid parents)
   - Ensure all parent-child associations are valid
   - Add warnings for incomplete or suspicious data structures

4. **Resolve Count Inflation Issue**
   - Debug why 946 raw rows become inflated counts in preview
   - Fix duplicate creation in transformation logic
   - Ensure each SDF record maps to exactly one preview item

#### Success Criteria:
- [ ] Preview counts match actual SDF data (no inflation)
- [ ] Tree view displays correct hierarchical relationships
- [ ] No orphaned records or invalid parent-child links
- [ ] Transformation logs show clean data processing

---

### Phase 3: Selective Import Implementation
**Duration**: ~1 hour  
**Deliverable**: Tree view with selection logic and database persistence preparation

#### Detailed Tasks:
1. **Implement Tree View Selection Logic**
   - Add checkbox functionality to tree view nodes
   - Implement smart selection: parent selection automatically includes children
   - Ensure child nodes cannot be selected without parent selection

2. **Add Selection Validation**
   - Validate that all parent dependencies are met
   - Warn users about incomplete product imports
   - Handle independent selections for hardware and detached products

3. **Create ImportSelectionService**
   - Build service to track selected items across tree hierarchy
   - Convert selected import models to database entity models
   - Maintain selection state and provide validation feedback

4. **Prepare Database Persistence Logic**
   - Design atomic transaction approach for selected item import
   - Handle conversion from import models to ShopBoss database entities
   - Implement rollback logic for failed imports

#### Success Criteria:
- [ ] Tree view selection works with parent-child validation
- [ ] Selection state is properly maintained and tracked
- [ ] Import models can be converted to database entities
- [ ] Validation prevents invalid selection combinations

---

### Phase 4: Import Confirmation & Database Integration
**Duration**: ~1 hour  
**Deliverable**: Complete import workflow with database storage

#### Detailed Tasks:
1. **Implement Confirm Import Functionality**
   - Convert selected import models to ShopBoss database entities
   - Execute database transaction with all selected items
   - Handle foreign key relationships and entity dependencies

2. **Add Duplicate Detection**
   - Check existing Microvellum IDs in database before import
   - Warn users about potential duplicates
   - Provide options for handling duplicate scenarios

3. **Create Import Summary Reports**
   - Generate detailed report of what was successfully imported
   - Include statistics: products, parts, subassemblies, hardware counts
   - Show any warnings or errors encountered during import

4. **Implement Audit Trail**
   - Track what was imported, when, and by whom
   - Store import session details and results
   - Enable import history review and troubleshooting

#### Success Criteria:
- [ ] Selected items are successfully saved to database
- [ ] Duplicate detection prevents data conflicts
- [ ] Import summary provides comprehensive feedback
- [ ] Audit trail captures all import activity

---

### Phase 5: Testing, Validation & Polish
**Duration**: ~1 hour  
**Deliverable**: Production-ready import system with comprehensive error handling

#### Detailed Tasks:
1. **Comprehensive Testing**
   - Test with various SDF file sizes and structures
   - Verify import workflow under different scenarios
   - Test error conditions and recovery mechanisms

2. **Performance Optimization**
   - Optimize transformation logic for large SDF files
   - Improve tree view rendering for complex hierarchies
   - Ensure acceptable response times for typical imports

3. **Enhanced Error Handling**
   - Improve user feedback for all error scenarios
   - Add detailed logging for troubleshooting
   - Implement graceful degradation for partial failures

4. **Documentation and Code Cleanup**
   - Update inline documentation for new services
   - Clean up debugging code and temporary implementations
   - Ensure code follows project standards and conventions

5. **Final Requirements Verification**
   - Verify all Phase 2 requirements are met
   - Test complete workflow from file upload to database storage
   - Confirm system is ready for production use

#### Success Criteria:
- [ ] All error scenarios handled gracefully
- [ ] Performance is acceptable for production use
- [ ] Code is clean, documented, and maintainable
- [ ] Complete workflow tested and verified

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

- [ ] Preview counts match actual SDF data (no inflation)
- [ ] Tree view displays correct hierarchical relationships
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