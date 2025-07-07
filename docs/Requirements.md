# ShopBoss v2 Requirements Document

**Document Version:** 2.0  
**Date:** 2025-06-18  
**Project:** Shop Floor Part Tracking System  
**Status:** Active Development

---

## Executive Summary

This document outlines the requirements for ShopBoss v2, a modern shop floor part tracking system designed to replace the discontinued Production Coach software. The system will manage millwork manufacturing workflow from CNC cutting through assembly and shipping, supporting hierarchical data import from Microvellum and enabling selective data management through an intuitive web-based interface.

---

## Business Problem Statement

### Current System Limitations

* **Discontinued Support:** The legacy Production Coach system is no longer supported by its vendor
* **Reliability Issues:** Frequent bugs and crashes disrupt production workflow 
* **Language Barriers:** Poor English translations impair usability on shop floor
* **Configuration Constraints:** Inflexible process flows cannot adapt to operational changes
* **Storage Integration Gaps:** Only 6 out of dozens of physical storage racks are configured
* **Import Complexity:** Manual data entry required; no automated Microvellum integration
* **Maintenance Difficulties:** No ability to customize or update system behavior

### Business Impact

* Production delays due to system crashes requiring manual restarts
* Inefficient parts storage and retrieval (90%+ rack capacity unused)
* Manual workarounds required for basic tracking operations
* Increased labor costs due to system inefficiencies
* Risk of complete production halt if unsupported system fails
* **Data integrity issues** from manual entry of complex work order hierarchies

### Success Criteria for ShopBoss v2

1. **Near-100% Uptime** - Reliable web-based operation during production hours
2. **Complete Storage Integration** - All physical racks configured and managed
3. **Automated Data Import** - Direct Microvellum SDF file processing with selective import
4. **Flexible Process Configuration** - Adaptable to changing manufacturing workflows
5. **User-Friendly Interface** - Intuitive operation requiring minimal training
6. **Modern Web Architecture** - Browser-based access from any shop floor terminal

---

## Data Structure Requirements

### Hierarchical Data Model

ShopBoss v2 must preserve the complete Microvellum data hierarchy:

```
Work Order (Name, ImportedDate)
├── Products (ProductNumber, Name, Qty, Dimensions)
│   ├── Parts (Name, Qty, Material, Dimensions, Edgebanding)
│   └── Subassemblies (Name, Qty, Dimensions)
│       ├── Parts (Name, Qty, Material, Dimensions, Edgebanding)
│       └── Nested Subassemblies (max 2 levels)
│           └── Parts (Name, Qty, Material, Dimensions, Edgebanding)
├── Hardware (Name, Qty)
└── Detached Products (ProductNumber, Name, Qty, Material, Dimensions, Edgebanding)
```

### Data Integrity Requirements

* **Preserve Microvellum IDs** - All unique identifiers maintained exactly as imported
* **Maintain Parent-Child Links** - Hierarchical relationships preserved throughout workflow
* **Dimensional Accuracy** - All measurements stored in millimeters with decimal precision
* **Product Number Separation** - Product numbers (e.g., "1.13", "1.25") as separate fields
* **Material Properties** - Complete edgebanding specifications for each edge
* **Recursive Structure Support** - Maximum 2 levels of subassembly nesting

---

## Functional Requirements

### FR-001: SDF Data Import and Processing

**Priority:** Critical | **Complexity:** High

**Description:** Import Microvellum SDF (SQL Server Compact) files with full data processing pipeline.

**Detailed Requirements:**
* **SDF File Processing** - Execute x86 importer tool as separate process (2-3 minute duration)
* **Real-time Progress Tracking** - SignalR-based progress updates with cancellation capability
* **Hierarchical Data Extraction** - Parse complete work order structure preserving all relationships
* **Data Validation** - Verify parent-child integrity and detect missing components
* **Error Handling** - Graceful failure with detailed error reporting and recovery suggestions

**Acceptance Criteria:**
* AC-001.1: SDF file upload and validation within 30 seconds
* AC-001.2: Complete hierarchical data extraction with preserved relationships
* AC-001.3: All Microvellum unique identifiers preserved exactly
* AC-001.4: Duplicate work order detection and rejection
* AC-001.5: Graceful error handling for corrupted files with detailed diagnostics

### FR-002: Interactive Tree View Visualization

**Priority:** High | **Complexity:** Medium

**Description:** Display imported work order data in expandable hierarchical tree structure.

**Detailed Requirements:**
* **Multi-level Display** - Support 2 levels of subassembly nesting with clear visual hierarchy
* **Entity Type Differentiation** - Color coding and icons for Products, Parts, Subassemblies, Hardware
* **Detail Tooltips** - Hover information showing dimensions, materials, and edgebanding details
* **Search and Filter** - Real-time filtering by name, material, or entity type
* **Expand/Collapse Controls** - Individual node control and master expand/collapse functionality

**Acceptance Criteria:**
* AC-002.1: Tree view displays complete hierarchy with proper nesting
* AC-002.2: All entity types render with appropriate visual indicators
* AC-002.3: Expand/collapse functionality works at all hierarchy levels
* AC-002.4: Search filtering updates tree view in real-time
* AC-002.5: Tooltips display complete entity information on hover

### FR-003: Selective Data Import

**Priority:** High | **Complexity:** High

**Description:** Allow administrators to select specific items for import into ShopBoss database.

**Detailed Requirements:**
* **Smart Selection Logic** - Selecting parent automatically selects all children
* **Hierarchy Validation** - Prevent orphaned child selections; enforce parent dependency
* **Independent Selections** - Hardware and Detached Products selectable independently
* **Business Rule Validation** - Warn about incomplete product imports and missing dependencies
* **Batch Processing** - Import selected items as single database transaction

**Acceptance Criteria:**
* AC-003.1: Parent selection automatically selects all child items
* AC-003.2: Cannot select children without selecting parent items
* AC-003.3: Hardware and Detached Products can be selected independently
* AC-003.4: Validation warnings for incomplete selections before import
* AC-003.5: All-or-nothing import transaction with rollback on failure

### FR-004: Import Progress and Status Management

**Priority:** Medium | **Complexity:** Medium

**Description:** Provide real-time feedback during long-running import operations.

**Detailed Requirements:**
* **Progress Tracking** - Multi-stage progress: SDF conversion (30%), SQL cleanup (60%), JSON generation (90%)
* **Time Estimation** - Display estimated time remaining based on current progress
* **Cancellation Support** - Allow users to cancel long-running imports with proper cleanup
* **Status Notifications** - Real-time updates via SignalR with success/failure messaging
* **Import History** - Log of all import attempts with timestamps and results

**Acceptance Criteria:**
* AC-004.1: Progress bar updates reflect actual import stages
* AC-004.2: Time estimation accuracy within 30 seconds for typical files
* AC-004.3: Import cancellation properly cleans up temporary files
* AC-004.4: Real-time status updates visible to user without page refresh
* AC-004.5: Complete import history accessible with search and filtering

### FR-005: Data Persistence and Management

**Priority:** High | **Complexity:** Medium

**Description:** Store imported data in ShopBoss database with full audit capabilities.

**Detailed Requirements:**
* **Entity Framework Integration** - Full ORM support with SQLite for development
* **Audit Trail** - Track what was imported when and by whom
* **Duplicate Prevention** - Check existing Microvellum IDs before import
* **Data Relationships** - Maintain foreign key constraints and referential integrity
* **Import Summaries** - Generate detailed reports of import results

**Acceptance Criteria:**
* AC-005.1: All imported data persisted with correct relationships
* AC-005.2: Audit trail records import details for compliance
* AC-005.3: Duplicate Microvellum IDs rejected with clear messaging
* AC-005.4: Database integrity maintained through all operations
* AC-005.5: Import summary reports available for download

### FR-006: Administrative Dashboard

**Priority:** Medium | **Complexity:** Low

**Description:** Provide administrators with oversight and management capabilities.

**Detailed Requirements:**
* **Import Statistics** - Count of work orders, products, parts by status
* **Recent Activity** - List of recent imports with success/failure status
* **Search Capabilities** - Find work orders by ID, name, or import date
* **Data Management** - View and manage imported work orders
* **System Health** - Basic system status and performance metrics

**Acceptance Criteria:**
* AC-006.1: Dashboard displays current system statistics accurately
* AC-006.2: Recent import activity visible with status indicators
* AC-006.3: Search functionality returns relevant results quickly
* AC-006.4: Work order management functions operate correctly
* AC-006.5: System health indicators reflect actual system status

---

## Non-Functional Requirements

### NFR-001: Performance Requirements

* **Import Processing** - SDF files up to 100MB processed within 5 minutes
* **Tree View Rendering** - Hierarchies up to 1000 items render within 2 seconds
* **Database Operations** - All CRUD operations complete within 1 second
* **Concurrent Users** - Support 10 simultaneous admin users without degradation
* **Memory Usage** - Application memory footprint under 512MB during normal operation

### NFR-002: Reliability Requirements

* **Uptime** - 99.9% availability during production hours (7 AM - 6 PM)
* **Data Integrity** - Zero data loss during import operations
* **Error Recovery** - Automatic recovery from temporary failures
* **Backup Strategy** - Database backup capability for disaster recovery
* **Import Resilience** - Handle corrupted or incomplete SDF files gracefully

### NFR-003: Usability Requirements

* **Learning Curve** - New users productive within 30 minutes of training
* **Interface Responsiveness** - All interactions provide immediate visual feedback
* **Mobile Compatibility** - Responsive design works on tablets and mobile devices
* **Accessibility** - Interface usable with standard keyboard navigation
* **Error Messaging** - Clear, actionable error messages for all failure scenarios

### NFR-004: Compatibility Requirements

* **Operating System** - Windows 10/11 with .NET 8 runtime
* **Browsers** - Chrome, Firefox, Edge (latest 2 versions)
* **File Formats** - Microvellum SDF files (SQL Server Compact)
* **Database** - SQLite for development, SQL Server for production
* **Import Tool** - x86 executable compatibility with process isolation

### NFR-005: Security Requirements

* **Data Protection** - Imported data encrypted at rest
* **Access Control** - Role-based authentication for admin functions
* **Audit Logging** - Complete audit trail of all administrative actions
* **Input Validation** - All file uploads validated and sanitized
* **Process Isolation** - Import tool executed in isolated process space

---

## Technical Architecture Requirements

### Technology Stack

* **Framework** - ASP.NET Core 8.0 MVC
* **Database** - Entity Framework Core 9.0.0 with SQLite (development) / SQL Server (production)
* **Frontend** - Bootstrap 5 with vanilla JavaScript (no external dependencies)
* **Real-time** - SignalR for progress updates and status notifications
* **Import Processing** - External x86 process execution with timeout and cancellation

### Data Import Architecture

* **Import Tool Integration** - Execute existing x86 importer as separate process
* **Process Management** - Proper timeout handling, error capture, and cleanup
* **Temporary File Handling** - Secure temporary storage with automatic cleanup
* **Progress Communication** - SignalR hub for real-time progress updates
* **Error Handling** - Structured error responses with user-friendly messaging

### Database Design Requirements

* **Entity Framework Models** - Code-first approach with migrations
* **Hierarchical Relationships** - Support for recursive subassembly structures
* **Audit Capabilities** - Built-in audit trail for all data changes
* **Performance Optimization** - Proper indexing for search and filtering operations
* **Data Validation** - Model-level validation for business rules

---

## Integration Requirements

### Microvellum Integration

* **SDF File Compatibility** - Support current and historical SDF file formats
* **ID Preservation** - Maintain exact Microvellum identifiers throughout system
* **Dimensional Accuracy** - Preserve millimeter measurements with decimal precision
* **Material Specifications** - Complete edgebanding and material property support
* **Hierarchy Integrity** - Maintain complex parent-child relationships

### Future Integration Considerations

* **Barcode Integration** - Preparation for future barcode scanning capabilities
* **Shop Floor Stations** - Foundation for multi-station workflow tracking
* **Production Scheduling** - Data structure supports future scheduling integration
* **Quality Control** - Audit trail supports future QC workflow integration
* **Reporting Systems** - Data export capabilities for external reporting tools

---

## User Interface Requirements

### Import Workflow Interface

* **File Upload** - Drag-and-drop SDF file upload with validation
* **Progress Display** - Multi-stage progress bar with time estimation
* **Tree View** - Expandable hierarchical display of imported data
* **Selection Interface** - Checkbox-based selection with smart logic
* **Import Confirmation** - Summary screen before final import execution

### Administrative Interface

* **Dashboard** - Overview of system status and recent activity
* **Work Order Management** - Search, view, and manage imported work orders
* **Import History** - Complete log of all import operations
* **System Configuration** - Basic system settings and maintenance
* **User Management** - Role-based access control administration

### Responsive Design Requirements

* **Tablet Optimization** - Touch-friendly interface for shop floor tablets
* **Mobile Compatibility** - Essential functions accessible on mobile devices
* **Desktop Experience** - Full functionality on desktop browsers
* **Cross-Browser** - Consistent experience across supported browsers
* **Accessibility** - WCAG 2.1 AA compliance for accessibility standards

---

## Testing Requirements

### Import Testing

* **File Format Testing** - Test with various SDF file sizes and formats
* **Hierarchy Testing** - Verify complex nested structures import correctly
* **Error Scenario Testing** - Test corrupted, incomplete, and invalid files
* **Performance Testing** - Large file import within specified timeframes
* **Cancellation Testing** - Verify proper cleanup when imports are cancelled

### User Interface Testing

* **Browser Compatibility** - Test on all supported browsers and versions
* **Responsive Testing** - Verify functionality on various screen sizes
* **Accessibility Testing** - Screen reader and keyboard navigation testing
* **Usability Testing** - User acceptance testing with actual shop floor users
* **Performance Testing** - Tree view performance with large hierarchies

### Integration Testing

* **Database Integration** - Verify all data persistence operations
* **Process Integration** - Test x86 importer tool execution and communication
* **Real-time Communication** - SignalR functionality under various conditions
* **Error Handling** - End-to-end error scenarios and recovery
* **Data Integrity** - Verify imported data matches source SDF files exactly

---

## Implementation Phases

### Phase 1: Foundation (2-3 weeks)
* Repository setup with .NET 8 web application
* Entity Framework models and database configuration
* Basic administrative interface and authentication
* Git workflow and deployment automation

### Phase 2: Import Processing (3-4 weeks)
* SDF file upload and validation
* x86 importer tool integration with process management
* Real-time progress tracking with SignalR
* Interactive tree view visualization with vanilla JavaScript

### Phase 3: Selective Import (2-3 weeks)
* Selection logic with parent-child validation
* Database persistence with transaction management
* Import confirmation and status reporting
* Administrative dashboard integration

### Phase 4: Polish and Testing (1-2 weeks)
* Comprehensive testing across all requirements
* Performance optimization and error handling refinement
* Documentation completion and user training materials
* Production deployment preparation

---

## Success Metrics

### Technical Metrics
* **Import Success Rate** - 99%+ successful imports of valid SDF files
* **Performance Targets** - All performance requirements consistently met
* **Error Recovery** - 100% graceful handling of error scenarios
* **Data Accuracy** - Zero discrepancies between SDF source and imported data

### Business Metrics
* **User Adoption** - 100% of administrators trained and using system within 30 days
* **Operational Efficiency** - 50% reduction in data entry time vs manual methods
* **System Reliability** - Zero production downtime due to import system issues
* **Data Quality** - Elimination of manual data entry errors in work order processing

---

## User Stories

### Admin

* **US‑001 Admin: Import Work Orders** — \*As an \****Admin***, I need to import a work order from Microvellum in SQL CE (`.sdf`) format so that the system captures all relevant data (work‑order header, products, parts, cut sheets, nests, etc.) in its own database.
* **US‑002 Admin: Manage Work Orders** — \*As an \****Admin***, I need to view, delete, and modify work orders in the database so that production data remains accurate and up to date.
* **US‑003 Admin: Manage Sorting Racks** — \*As an \****Admin***, I need to add, delete, and define compartments (rows/columns) in each sorting rack so that digital storage matches the physical warehouse.
* **US‑004 Admin: Override Storage Locations** — \*As an \****Admin***, I need to manually override the contents of any storage location so that I can correct mistakes or handle exceptions.

### CNC Operator

* **US‑005 CNC: Batch Cut Scanning** — \*As a \****CNC operator***, I need to scan the barcode on a nest sheet and have the software automatically mark all related parts as **Cut** so that I avoid dozens of individual scans.

### Sorting Operator

* **US‑006 Sort: Slot Assignment** — \*As a \****Sorting operator***, I need the software to tell me where to store a part once I scan its barcode so that parts are organized efficiently.
* **US‑007 Sort: Rack Visualization** — \*As a \****Sorting operator***, I need a visual view of the current sorting rack showing which slots are filled/empty and highlighting where to place the scanned part.
* **US‑008 Sort: Product Grouping** — \*As a \****Sorting operator***, I need the software to recognize parts that belong to the same product and instruct me to store them together in preparation for assembly.
* **US‑009 Sort: Special Rack for Doors/Drawer Fronts** — \*As a \****Sorting operator***, I need the software to recognize when a part is a door or drawer front and direct me to the special rack reserved for those items.
* **US‑010 Sort: Status Update** — \*As a \****Sorting operator***, when I sort a part, the software must update that part’s status to **Sorted**.

### Assembly Operator

* **US‑011 Assembly: Carcass Readiness & Completion** — \*As an \****Assembly operator***, I need the software to alert me when all carcass parts of a product are sorted, and after assembly, allow me to scan any part to mark the entire product (and its parts) as **Assembled**, then direct me to the stored doors and drawer fronts for fitting.

### Shipping Operator

* **US‑012 Shipping: Work‑Order Verification** — \*As a \****Shipping operator***, I need to pull up a work order and scan each product, hardware item, and detached product as it is loaded so that the software tallies everything and prevents omissions. The system updates the status for each part and each product I handle to "Shipped".

*(Additional user stories for Production Managers, QA, and future enhancements remain in the Requirements for further elaboration.)*

---

*Document Status: This requirements document serves as the definitive specification for ShopBoss v2 development. All requirements should be validated with stakeholders and updated as needed during development cycles.*