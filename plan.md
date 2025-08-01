# Project Extension Plan: PurchaseOrder and CustomWorkOrder

## CRITICAL: NO DATABASE MIGRATIONS NEEDED
**This project has NO existing data to preserve. The entire database will be created fresh when launched. DO NOT create or execute any migrations - simply update the models and DbContext. The database schema will be created automatically via EnsureCreated().**

## Overview
Add two new child entities to Projects:
1. **PurchaseOrder** - A separate entity for managing purchase orders within projects
2. **CustomWorkOrder** - An additional work order entity for non-manufacturing work (Layup, Finishing, Custom Fabrication, etc.)

Both entities will appear in the expanded Project view, with PurchaseOrders appearing before Work Orders and CustomWorkOrders appearing in the same Work Orders section as existing WorkOrders.

## Current Architecture Analysis

**Existing WorkOrder Entity:**
- Complex manufacturing-focused entity with Products, Parts, Subassemblies, Hardware, DetachedProducts, NestSheets
- Has ProjectId foreign key relationship to Project
- Has ImportedDate (comes from SDF file imports)
- Used for CNC cutting/assembly workflow - should remain untouched

**Project Entity Structure:**
- Has navigation property `List<WorkOrder> WorkOrders`
- Has navigation property `List<ProjectAttachment> Attachments`
- Uses standard GUID Id pattern
- Follows established service/controller patterns

## Phase 1: Create PurchaseOrder Entity and Infrastructure

### Phase 1A: Entity and Model Creation
**Objective:** Create PurchaseOrder entity following Project architecture patterns

**Target Files:**
- Backend: `Models/PurchaseOrder.cs`, `Data/ShopBossDbContext.cs`

**Tasks:**
1. Create `PurchaseOrder.cs` model with properties:
   - Id (string, GUID pattern like Project)
   - ProjectId (string, foreign key)
   - Project (navigation property)
   - PurchaseOrderNumber (string)
   - VendorName (string)
   - VendorContact (string, optional)
   - VendorPhone (string, optional)
   - VendorEmail (string, optional)
   - Description (string)
   - OrderDate (DateTime)
   - ExpectedDeliveryDate (DateTime, optional)  
   - ActualDeliveryDate (DateTime, optional)
   - TotalAmount (decimal, optional)
   - Status (enum: Pending, Ordered, Received, Cancelled)
   - Notes (string, optional)
   - CreatedDate (DateTime)

2. Add PurchaseOrderStatus enum with values: Pending, Ordered, Received, Cancelled

3. Update Project.cs to include `List<PurchaseOrder> PurchaseOrders` navigation property

4. Update ShopBossDbContext.cs:
   - Add `DbSet<PurchaseOrder> PurchaseOrders`
   - Add entity configuration in OnModelCreating

**Validation:**
- [ ] Build succeeds without errors
- [ ] Database schema creates correctly via EnsureCreated()
- [ ] Project → PurchaseOrder relationship established

**Dependencies:** None

### Phase 1B: Service Layer for PurchaseOrders
**Objective:** Create service layer following existing WorkOrderService patterns

**Target Files:**
- Backend: `Services/PurchaseOrderService.cs`, `Program.cs`

**Tasks:**
1. Create `PurchaseOrderService.cs` with methods:
   - `GetPurchaseOrdersByProjectIdAsync(string projectId)`
   - `CreatePurchaseOrderAsync(PurchaseOrder purchaseOrder)`
   - `UpdatePurchaseOrderAsync(PurchaseOrder purchaseOrder)`
   - `DeletePurchaseOrderAsync(string id)`

2. Register PurchaseOrderService in Program.cs dependency injection

3. Follow existing service patterns for logging and error handling

**Validation:**
- [ ] Service methods work correctly
- [ ] Dependency injection registration successful
- [ ] Error handling consistent with existing services

**Dependencies:** Phase 1A complete

### Phase 1C: Controller and API Endpoints
**Objective:** Create controller following ProjectController patterns

**Target Files:**
- Backend: `Controllers/PurchaseOrderController.cs` or add to `Controllers/ProjectController.cs`

**Tasks:**
1. Add PurchaseOrder endpoints to ProjectController (following existing WorkOrder attachment pattern):
   - `CreatePurchaseOrder` (POST)
   - `UpdatePurchaseOrder` (POST) 
   - `DeletePurchaseOrder` (POST)

2. Return JSON responses matching existing Project endpoint patterns

3. Add audit trail integration for PurchaseOrder operations

**Validation:**
- [ ] API endpoints return correct JSON responses
- [ ] Error handling matches existing patterns
- [ ] Audit trail records PurchaseOrder changes

**Dependencies:** Phase 1B complete

## Phase 2: Create CustomWorkOrder Entity

### Phase 2A: Entity Creation for CustomWorkOrder
**Objective:** Create simplified CustomWorkOrder entity for non-manufacturing work

**Target Files:**
- Backend: `Models/CustomWorkOrder.cs`, `Models/CustomWorkOrderType.cs`, `Data/ShopBossDbContext.cs`

**Tasks:**
1. Create `CustomWorkOrderType.cs` enum with values:
   - Layup
   - Finishing  
   - CustomFabrication
   - Welding
   - Other

2. Create `CustomWorkOrder.cs` model with properties:
   - Id (string, GUID pattern)
   - ProjectId (string, foreign key)
   - Project (navigation property)
   - Name (string)
   - WorkOrderType (CustomWorkOrderType enum)
   - Description (string)
   - AssignedTo (string, optional)
   - EstimatedHours (decimal, optional)
   - ActualHours (decimal, optional)
   - Status (enum: Pending, InProgress, Completed, Cancelled)
   - StartDate (DateTime, optional)
   - CompletedDate (DateTime, optional)
   - Notes (string, optional)
   - CreatedDate (DateTime)

3. Create CustomWorkOrderStatus enum: Pending, InProgress, Completed, Cancelled

4. Update Project.cs to include `List<CustomWorkOrder> CustomWorkOrders` navigation property

5. Update ShopBossDbContext.cs for CustomWorkOrder entity configuration

**Validation:**
- [ ] Build succeeds without errors
- [ ] Database schema creates correctly via EnsureCreated()
- [ ] Project → CustomWorkOrder relationship established

**Dependencies:** Phase 1 complete (to avoid migration conflicts)

### Phase 2B: Service Layer for CustomWorkOrders
**Objective:** Create CustomWorkOrderService following established patterns

**Target Files:**
- Backend: `Services/CustomWorkOrderService.cs`, `Program.cs`

**Tasks:**
1. Create `CustomWorkOrderService.cs` with CRUD methods matching PurchaseOrderService patterns

2. Register service in Program.cs dependency injection

3. Follow existing logging and error handling patterns

**Validation:**
- [ ] Service methods work correctly
- [ ] Dependency injection registration successful
- [ ] Error handling consistent with existing services

**Dependencies:** Phase 2A complete

### Phase 2C: Controller Endpoints for CustomWorkOrder
**Objective:** Add CustomWorkOrder endpoints to ProjectController

**Target Files:**
- Backend: `Controllers/ProjectController.cs`

**Tasks:**
1. Add CustomWorkOrder endpoints to ProjectController:
   - `CreateCustomWorkOrder` (POST)
   - `UpdateCustomWorkOrder` (POST)
   - `DeleteCustomWorkOrder` (POST)

2. Return JSON responses matching existing patterns

3. Add audit trail integration

**Validation:**
- [ ] API endpoints return correct JSON responses
- [ ] Error handling matches existing patterns
- [ ] Audit trail records CustomWorkOrder changes

**Dependencies:** Phase 2B complete

## Phase 3: Frontend Integration

### Phase 3A: Update Project Index View
**Objective:** Add PurchaseOrders and CustomWorkOrders sections to expanded Project view

**Target Files:**
- Frontend: `Views/Project/Index.cshtml`

**Tasks:**
1. Add PurchaseOrders section after Files section and before Work Orders section with:
   - Header showing count `Purchase Orders (X)`
   - Add button for creating new PurchaseOrders
   - List display showing PO Number, Vendor, Status, Order Date
   - Edit/Delete buttons for each PurchaseOrder
   - Follow same DOM manipulation patterns (no page refreshes)

2. Update Work Orders section to include CustomWorkOrders:
   - Modify existing Work Orders display to show both WorkOrders and CustomWorkOrders
   - Add visual distinction (icon or badge) to differentiate CustomWorkOrders
   - Include CustomWorkOrder Type in display
   - Add button for creating new CustomWorkOrders

3. Follow existing card layout patterns established in Files and Work Orders sections

**Validation:**
- [ ] PurchaseOrders section displays correctly
- [ ] CustomWorkOrders appear in Work Orders section
- [ ] Visual layout matches existing sections
- [ ] All buttons and displays functional

**Dependencies:** Phase 2C complete

### Phase 3B: Create Forms and Modals
**Objective:** Create forms for PurchaseOrder and CustomWorkOrder creation/editing

**Target Files:**
- Frontend: `Views/Project/_PurchaseOrderForm.cshtml`, `Views/Project/_CustomWorkOrderForm.cshtml`
- Frontend: Update `Views/Project/Index.cshtml` for modals

**Tasks:**
1. Create `_PurchaseOrderForm.cshtml` partial with:
   - All PurchaseOrder fields in organized layout
   - Dropdown for Status selection
   - Date pickers for order/delivery dates
   - Validation attributes

2. Create `_CustomWorkOrderForm.cshtml` partial with:
   - All CustomWorkOrder fields
   - Dropdown for WorkOrderType selection
   - Dropdown for Status selection
   - Date pickers where needed

3. Add modals to Project Index for:
   - Create PurchaseOrder (using _PurchaseOrderForm.cshtml partial)
   - Edit PurchaseOrder (using _PurchaseOrderForm.cshtml partial)
   - Create CustomWorkOrder (using _CustomWorkOrderForm.cshtml partial)
   - Edit CustomWorkOrder (using _CustomWorkOrderForm.cshtml partial)

**Validation:**
- [ ] Forms display correctly in modals
- [ ] All form fields work properly
- [ ] Dropdowns populate with correct options
- [ ] Form validation works

**Dependencies:** Phase 3A complete

### Phase 3C: JavaScript Integration
**Objective:** Add JavaScript functions for PurchaseOrder and CustomWorkOrder management

**Target Files:**
- Frontend: `wwwroot/js/project-management.js`

**Tasks:**
1. Add PurchaseOrder JavaScript functions:
   - `createPurchaseOrder(projectId)`
   - `editPurchaseOrder(purchaseOrderId, projectId)`
   - `savePurchaseOrder(purchaseOrderId)`
   - `deletePurchaseOrder(purchaseOrderId, projectId)`
   - DOM manipulation for adding/updating/removing PurchaseOrders

2. Add CustomWorkOrder JavaScript functions:
   - `createCustomWorkOrder(projectId)`
   - `editCustomWorkOrder(customWorkOrderId, projectId)`
   - `saveCustomWorkOrder(customWorkOrderId)`
   - `deleteCustomWorkOrder(customWorkOrderId, projectId)`
   - DOM manipulation for adding/updating/removing CustomWorkOrders in Work Orders list

3. Update count badges in table and section headers for both entity types

4. Follow existing patterns from WorkOrder and File management functions

**Validation:**
- [ ] All CRUD operations work via JavaScript
- [ ] DOM updates correctly without page refresh
- [ ] Count badges update properly
- [ ] Error handling displays notifications

**Dependencies:** Phase 3B complete

## Phase 4: Testing and Integration

### Phase 4A: Backend Testing
**Objective:** Verify all backend functionality works correctly

**Target Files:**
- Database: Test with actual data
- Backend: All services and controllers

**Tasks:**
1. Test PurchaseOrder CRUD operations through API endpoints
2. Test CustomWorkOrder CRUD operations through API endpoints  
3. Verify database relationships and cascade behaviors
4. Test audit trail integration
5. Verify no impact on existing WorkOrder functionality

**Validation:**
- [ ] All API endpoints work correctly
- [ ] Database operations complete successfully
- [ ] No regression in existing functionality
- [ ] Audit trail captures all changes

**Dependencies:** Phase 3C complete

### Phase 4B: Frontend Testing
**Objective:** Verify complete user experience works correctly

**Target Files:**
- Frontend: Complete Project management workflow

**Tasks:**
1. Test creating Projects with all child entity types
2. Test editing all entity types inline
3. Test DOM manipulation and real-time updates
4. Test modal forms and validation
5. Verify visual layout and user experience

**Validation:**
- [ ] Complete workflow functions properly
- [ ] No JavaScript errors in console
- [ ] Visual layout matches existing patterns
- [ ] User experience is intuitive

**Dependencies:** Phase 4A complete

## Phase 5: Documentation and Deployment

### Phase 5A: Update Documentation
**Objective:** Document new functionality and data model changes

**Target Files:**
- Documentation: CLAUDE.md updates, any README updates needed

**Tasks:**
1. Update CLAUDE.md with new entity information
2. Document new API endpoints
3. Document new service patterns  
4. Update entity relationship documentation

**Validation:**
- [ ] Documentation accurately reflects new functionality
- [ ] Future developers can understand the architecture

**Dependencies:** Phase 4B complete

### Phase 5B: Final Build and Deployment Preparation
**Objective:** Ensure everything builds and is ready for user testing

**Target Files:**
- Build: Complete application

**Tasks:**
1. Run final build verification
2. Verify all migrations are ready for deployment
3. Test complete workflow end-to-end
4. Prepare deployment notes for user

**Validation:**
- [ ] Build succeeds without errors or warnings
- [ ] Database migrations ready for deployment
- [ ] Complete functionality verified
- [ ] User testing instructions prepared

**Dependencies:** Phase 5A complete

## Implementation Strategy

### Entity Design Decisions:
- **PurchaseOrder**: Focused on vendor management and delivery tracking
- **CustomWorkOrder**: Lightweight work tracking without complex manufacturing relationships
- **Preserve WorkOrder**: Keep existing manufacturing WorkOrder completely unchanged
- **Unified Display**: CustomWorkOrders appear alongside WorkOrders in same UI section

### Database Strategy:
- NO MIGRATIONS NEEDED - Database will be created fresh via EnsureCreated()
- Use established Project relationship patterns (foreign keys, cascade behaviors)
- Follow existing naming and indexing conventions

### UI Strategy:
- Add PurchaseOrders as new card section (following Files pattern)
- Integrate CustomWorkOrders into existing Work Orders section
- Use existing modal/form patterns for consistency
- Maintain existing DOM manipulation patterns for real-time updates

This plan preserves all existing functionality while cleanly extending Projects to support the requested new child entity types.