---

## Development Guidelines for Claude Code

### Key Technical Requirements:
1. **Maintain existing architecture** - Use established ASP.NET Core patterns and project structure
2. **Database integration** - Leverage existing EF Core 9.0.0 and SQLite setup
3. **Real-time updates** - Integrate with existing SignalR infrastructure (`/hubs/status`)
4. **Consistent UI** - Follow established Bootstrap 5 design patterns and navigation structure  
5. **Audit trails** - Ensure all scan operations and status changes are properly logged
6. **Active Work Order** - Respect system-wide active work order selection across all stations

### Coding Standards:
- Use async/await patterns for all database operations
- Implement proper error handling and user-friendly validation messages
- Follow existing naming conventions and project structure (see `/src/ShopFloorTracker.Web/`)
- Maintain responsive design optimized for shop floor tablets
- Ensure barcode scanning integration points are clearly defined and testable
- Update `Program.cs` for any new route additions using existing patterns

### Testing Approach:
- Test each phase incrementally before moving to next phase
- Verify real-time updates work across all stations using SignalR
- Validate proper status transitions: Pending → Cut → Sorted → Assembled → Shipped
- Ensure audit trail completeness in database
- Test Active Work Order switching scenarios between stations
- Verify mobile responsiveness on tablet-sized screens

### Integration Points:
- **Database Context:** Use existing `ShopFloorDbContext` for all operations
- **SignalR Hub:** Connect to existing `/hubs/status` for real-time updates
- **Navigation:** Follow existing header navigation pattern with station links
- **Styling:** Use existing CSS classes and Bootstrap 5 utilities
- **Active Work Order:** Integrate with existing admin station work order selection

---# ShopBoss-v2 Development Roadmap
## Interface Implementation Phases

**Current Status:** Phase 3-B1 Complete (~95% core functionality complete)  
**Target:** Complete remaining interface implementations for production-ready system

---

## Project Overview & Context

### What We're Building
ShopBoss-v2 is a modern web-based shop floor tracking system replacing the discontinued Production Coach software. The system manages millwork manufacturing workflow from CNC cutting through assembly and shipping, supporting hierarchical data import from Microvellum.

### Current Technical Foundation (SOLID ✅)
- **ASP.NET Core 8.0** application with Clean Architecture
- **Entity Framework Core 9.0.0** with SQLite database
- **SignalR** for real-time updates across stations
- **Bootstrap 5** responsive UI for shop floor terminals
- **Complete database schema** with audit trails and hierarchical data support
- **Working admin station** with work order management

### What This Roadmap Completes
The core infrastructure is complete. This roadmap implements the remaining **user-facing interfaces** that make the system production-ready:
1. **Enhanced Admin Station** - Advanced work order management
2. **CNC Station Interface** - Nest sheet tracking and barcode scanning
3. **Sorting Station Interface** - Smart rack assignment and visualization  
4. **Assembly Station Interface** - Product completion workflow
5. **Shipping Station Interface** - Final verification and tracking
6. **Configuration Management** - Storage rack setup and system settings

### Before Starting Any Phase
**MANDATORY:** Each agent must read this entire document to understand the complete project scope and how their specific phase fits into the larger system.

---

## Work Tracking Guidelines for All Phases

### Required Documentation Process (EVERY PHASE)

#### Before Starting:
1. **Read Complete Context:**
   - This entire roadmap document to understand project scope
   - `Worklog.md` (bottom-up chronological order) for recent changes
   - `PROJECT_STATUS.md` for current completion status
   - `AGENT_HANDOFF_LOG.md` for any blockers or notes

2. **Update Worklog:**
   - Add new section: `## Phase X: [Description] - [Agent Name] - [Date]`
   - Document your planned approach and any discoveries

#### During Development:
3. **Follow Git Standards:**
   ```bash
   git add .
   git commit -m "Phase X: [Description] - Claude Code"
   git push origin main
   ```

4. **Test Integration:**
   - Verify your changes work with existing features
   - Test Active Work Order functionality across stations
   - Confirm SignalR real-time updates function properly

#### After Completion:
5. **Complete Documentation:**
   - Update `Worklog.md` with completion notes and any issues discovered
   - Update `PROJECT_STATUS.md` with new completion percentage
   - Add handoff notes in `AGENT_HANDOFF_LOG.md` if needed
   - Document any architectural changes or new patterns

6. **Verification Requirements:**
   - All functionality works as specified in deliverables
   - No breaking changes to existing features
   - Code builds and runs without errors
   - Changes are pushed to main branch and visible on GitHub

### Emergency Procedures:
- **If stuck:** Document the blocker in `Worklog.md` and `AGENT_HANDOFF_LOG.md`
- **If build fails:** Revert to last working commit and document the issue
- **If requirements unclear:** Stop and request clarification rather than guess

---

## Phase 4: Enhanced Admin Station (4-6 hours)

### Phase 4A: Work Order Preview Enhancement (1.5 hours)

**Prompt for Claude Code:**
> **PHASE 4A:** Enhance the Admin Station Work Order Preview to display three top-level nodes: Products, Hardware, and Detached Parts. Nest associated items under these appropriately. Combine identical hardware items with counts rather than showing individual entries. Focus on creating a readable and actionable list of hardware components and counts.

**Deliverables:**
- [ ] Modified preview display with three-node structure
- [ ] Hardware consolidation logic with quantity aggregation
- [ ] Improved readability for hardware components
- [ ] Clean separation of Products/Hardware/Detached Parts

### Phase 4B: Import/Modify Interface Unification (1.5 hours)

**Prompt for Claude Code:**
> **PHASE 4B:** Create unified interface patterns for Import Work Order, Modify Work Order, and Work Order Details. These should share similar functionality allowing admins to add/drop entities and modify work order metadata. Implement the ability to edit Work Order Name and other high-level properties during both import and modification workflows.

**Deliverables:**
- [ ] Unified interface pattern for work order manipulation
- [ ] Add/remove entities functionality
- [ ] Work Order metadata editing (name, dates, etc.)
- [ ] Consistent UX across import/modify/details views

### Phase 4C: Advanced Work Order Management (1 hour)
**Prompt for Claude Code:**
> Implement advanced work order management features including bulk operations, status management, and improved search/filtering. Add the ability to set which Work Order is "Active" in the system and ensure this status is respected across all station interfaces.

**Deliverables:**
- [ ] "Active Work Order" selection mechanism
- [ ] Bulk work order operations (delete, status change)
- [ ] Enhanced search and filtering
- [ ] Active work order status integration with other stations

---

## Phase 5: Shop Tab - CNC Station Interface (3-4 hours)

### Phase 5A: Nest Sheet Management (2 hours)

**Prompt for Claude Code:**
> **PHASE 5A:** Create the CNC View Sub-tab displaying a list of Nest Sheets associated with the Active Work Order. Show indicators for cut/uncut status, part counts per sheet, material specifications, and sheet dimensions. Implement barcode scanning functionality that marks all associated parts as "Cut" when a nest sheet barcode is scanned.

**Deliverables:**
- [ ] Nest Sheet list view with status indicators
- [ ] Part count metrics per sheet
- [ ] Material and dimension display
- [ ] Barcode scanning integration for batch part marking
- [ ] Real-time status updates when sheets are processed

### Phase 5B: CNC Operation Workflow (1.5 hours)
**Prompt for Claude Code:**
> Implement the CNC operator workflow including scan validation, error handling, and status reporting. Add visual feedback for successful scans and integration with the shop floor tracking system. Ensure all part status changes are logged in the audit trail.

**Deliverables:**
- [ ] Barcode scan validation and processing
- [ ] Visual feedback for scan operations
- [ ] Error handling for invalid/duplicate scans
- [ ] Audit trail integration
- [ ] Real-time dashboard updates

---

## Phase 6: Shop Tab - Sorting Station Interface (4-5 hours)

### Phase 6A: Sorting Rack Visualization (2 hours)
**Prompt for Claude Code:**
> Create the Sorting View Sub-tab with rack-by-rack navigation. Display visual representation of sorting racks showing filled/empty bins with the ability to switch between different racks and carts. Implement intelligent part placement rules for doors, adjustable shelves, drawer fronts (to special racks) and carcass parts (grouped by product).

**Deliverables:**
- [ ] Visual rack display with bin status
- [ ] Rack/cart navigation interface
- [ ] Intelligent placement rule engine
- [ ] Special handling for doors/drawer fronts
- [ ] Product-based carcass part grouping

### Phase 6B: Smart Sorting Logic (1.5 hours)
**Prompt for Claude Code:**
> Implement the intelligent sorting system that determines placement based on part type and current rack occupancy. When operators scan cut parts, the system should automatically assign optimal bin locations and provide clear visual guidance. Update part status to "Sorted" and track bin completion.

**Deliverables:**
- [ ] Automatic bin assignment algorithm
- [ ] Visual placement guidance
- [ ] Part status updates ("Sorted")
- [ ] Bin completion detection
- [ ] Assembly readiness notifications

### Phase 6C: Real-time Sorting Interface (1 hour)
**Prompt for Claude Code:**
> Complete the sorting station with real-time updates, scan feedback, and integration with assembly readiness notifications. Ensure smooth operator experience with immediate visual confirmation of scan operations and clear next-step guidance.

**Deliverables:**
- [ ] Real-time scan feedback
- [ ] Assembly readiness indicators
- [ ] Clear operator guidance
- [ ] Integration with assembly station notifications

---

## Phase 7: Shop Tab - Assembly Station Interface (3-4 hours)

### Phase 7A: Assembly Readiness Display (1.5 hours)
**Prompt for Claude Code:**
> Create the Assembly View Sub-Tab showing sorting rack status with indicators for complete product assemblies. Display when all carcass parts for a product are available and ready for assembly. Provide clear visual indication of which products can be assembled.

**Deliverables:**
- [ ] Assembly readiness dashboard
- [ ] Product completion indicators
- [ ] Sorting rack status integration
- [ ] Clear visual assembly queue

### Phase 7B: Assembly Workflow (1.5 hours)
**Prompt for Claude Code:**
> Implement the assembly workflow where operators scan one part to mark entire products as "Assembled". After scanning, direct the operator to locations of doors, drawer fronts, and adjustable shelves for final installation. Update all associated part statuses simultaneously.

**Deliverables:**
- [ ] One-scan product completion
- [ ] Component location guidance
- [ ] Batch status updates for all product parts
- [ ] Clear next-step instructions for finishing

### Phase 7C: Assembly Completion Integration (1 hour)
**Prompt for Claude Code:**
> Complete assembly station integration with real-time updates to other stations, proper audit trail logging, and seamless transition to shipping-ready status. Ensure all assemblies are properly tracked and visible to shipping station.

**Deliverables:**
- [ ] Cross-station status updates
- [ ] Complete audit trail integration
- [ ] Shipping-ready status management
- [ ] Real-time dashboard updates

---

## Phase 8: Shop Tab - Shipping Station Interface (2-3 hours)

### Phase 8A: Shipping Dashboard (1.5 hours)
**Prompt for Claude Code:**
> Create the Shipping View Sub-Tab displaying all components of the Active Work Order: Products, Hardware, and Detached Products. Provide scanning interface for each item type with real-time tracking of what's been loaded versus what's outstanding. Show clear shipping checklist progress.

**Deliverables:**
- [ ] Comprehensive shipping checklist
- [ ] Multi-category item display (Products/Hardware/Detached)
- [ ] Scan-based loading confirmation
- [ ] Real-time progress tracking
- [ ] Outstanding items visibility

### Phase 8B: Shipping Workflow Completion (1 hour)
**Prompt for Claude Code:**
> Complete shipping station workflow with final status updates, work order completion handling, and comprehensive reporting. Ensure all scanned items are marked "Shipped" and provide final work order completion confirmation.

**Deliverables:**
- [ ] Final status updates ("Shipped")
- [ ] Work order completion processing
- [ ] Shipping confirmation reports
- [ ] Complete workflow finalization

---

## Phase 9: Configuration Tab - Storage Management (2-3 hours)

### Phase 9A: Rack Configuration Interface (2 hours)
**Prompt for Claude Code:**
> Create the Configuration Tab with storage rack management. Implement create/delete/modify functionality for storage racks with custom dimensions and layouts. Allow definition of rack rules and part filtering (doors/drawer fronts only, etc.). Provide visual rack layout editor.

**Deliverables:**
- [ ] Rack CRUD operations
- [ ] Custom dimension configuration
- [ ] Rule-based part filtering setup
- [ ] Visual layout editor
- [ ] Rack type and capacity management

### Phase 9B: Advanced Configuration (1 hour)
**Prompt for Claude Code:**
> Complete configuration interface with system-wide settings, user preferences, and integration testing. Ensure all rack configurations properly integrate with sorting and assembly station workflows.

**Deliverables:**
- [ ] System-wide configuration options
- [ ] Integration validation
- [ ] Configuration backup/restore
- [ ] Cross-station configuration testing

---

## Phase 10: Integration & Polish (2-3 hours)

### Phase 10A: Cross-Station Integration Testing (1.5 hours)
**Prompt for Claude Code:**
> Conduct comprehensive integration testing across all stations. Verify proper data flow from CNC → Sorting → Assembly → Shipping. Test Active Work Order changes, real-time updates, and audit trail completeness. Fix any integration issues discovered.

**Deliverables:**
- [ ] End-to-end workflow testing
- [ ] Real-time update verification
- [ ] Active Work Order consistency
- [ ] Integration issue resolution

### Phase 10B: Production Readiness Polish (1 hour)
**Prompt for Claude Code:**
> Final production polish including error handling improvements, user input validation, performance optimization, and mobile responsiveness verification. Ensure the system is ready for production deployment.

**Deliverables:**
- [ ] Enhanced error handling
- [ ] Input validation improvements
- [ ] Performance optimization
- [ ] Mobile responsiveness verification
- [ ] Production deployment readiness

---

## Development Guidelines for Claude Code

### Key Technical Requirements:
1. **Maintain existing architecture** - Use established ASP.NET Core patterns
2. **Database integration** - Leverage existing EF Core 9.0.0 and SQLite setup
3. **Real-time updates** - Integrate with existing SignalR infrastructure
4. **Consistent UI** - Follow established design patterns and navigation
5. **Audit trails** - Ensure all operations are properly logged
6. **Active Work Order** - Respect system-wide active work order selection

### Coding Standards:
- Use async/await patterns for all database operations
- Implement proper error handling and validation
- Follow existing naming conventions and project structure
- Maintain responsive design for shop floor terminals
- Ensure barcode scanning integration points are clearly defined

### Testing Approach:
- Test each phase incrementally
- Verify real-time updates across all stations
- Validate proper status transitions (Pending → Cut → Sorted → Assembled → Shipped)
- Ensure audit trail completeness
- Test Active Work Order switching scenarios

---

## Estimated Timeline
- **Phase 4 (Admin Enhancement):** 4-6 hours
- **Phase 5 (CNC Station):** 3-4 hours  
- **Phase 6 (Sorting Station):** 4-5 hours
- **Phase 7 (Assembly Station):** 3-4 hours
- **Phase 8 (Shipping Station):** 2-3 hours
- **Phase 9 (Configuration):** 2-3 hours
- **Phase 10 (Integration & Polish):** 2-3 hours

**Total Estimated Time:** 20-28 hours of focused development

This roadmap provides manageable 1-2 hour chunks that build systematically on your solid foundation toward a complete production-ready system.