# Project Entity Implementation Plan

## Overview
Introduce a new top-level **Project** entity that contains Work Orders and file attachments, providing better organization for real-world job management.

## Phase 1: Database Schema & Models (High Priority)

### 1.1 Create Project Entity
**File:** `src/ShopBoss.Web/Models/Project.cs`
```
- Id (string, Primary Key)
- Name (string)
- JobNumber (string, nullable)
- Address (string, nullable) 
- ContactName (string, nullable)
- ContactPhone (string, nullable)
- ContactEmail (string, nullable)
- Description (string, nullable)
- CreatedDate (DateTime)
- IsArchived (bool)
- ArchivedDate (DateTime?, nullable)
- List<WorkOrder> WorkOrders (Navigation)
- List<ProjectAttachment> Attachments (Navigation)
```

### 1.2 Create ProjectAttachment Entity  
**File:** `src/ShopBoss.Web/Models/ProjectAttachment.cs`
```
- Id (string, Primary Key)
- ProjectId (string, Foreign Key)
- FileName (string)
- OriginalFileName (string)
- FileSize (long)
- ContentType (string)
- Category (string) // "Schematic", "Correspondence", "Invoice", "Proof", "Other"
- UploadedDate (DateTime)
- UploadedBy (string, nullable)
- Project (Navigation)
```

### 1.3 Update WorkOrder Entity
**File:** `src/ShopBoss.Web/Models/WorkOrder.cs`
- Add `ProjectId (string?, nullable, Foreign Key)`
- Add `Project (Navigation, nullable)`

### 1.4 Update DbContext
**File:** `src/ShopBoss.Web/Data/ShopBossDbContext.cs`
- Add `DbSet<Project> Projects`
- Add `DbSet<ProjectAttachment> ProjectAttachments`
- Configure relationships in OnModelCreating

### 1.5 Create Migration
- Generate migration for new Project tables and WorkOrder.ProjectId column
- Handle existing WorkOrders (leave ProjectId null initially)

## Phase 2: Backend Services & Controllers (High Priority)

### 2.1 Create ProjectService
**File:** `src/ShopBoss.Web/Services/ProjectService.cs`
```
- GetProjectSummariesAsync(search, includeArchived)
- GetProjectByIdAsync(id) 
- CreateProjectAsync(project)
- UpdateProjectAsync(project)
- ArchiveProjectAsync(id)
- UnarchiveProjectAsync(id)
- DeleteProjectAsync(id)
- AttachWorkOrderToProjectAsync(workOrderId, projectId)
- DetachWorkOrderFromProjectAsync(workOrderId)
```

### 2.2 Create ProjectController
**File:** `src/ShopBoss.Web/Controllers/ProjectController.cs`
- Index (GET) - Project list view
- Create (GET/POST) - Create new project
- Edit (GET/POST) - Edit project details
- Details (GET) - Project details with work orders
- Archive/Unarchive (POST)
- Delete (POST)
- BulkDelete (POST)
- AttachWorkOrder (POST)
- DetachWorkOrder (POST)

### 2.3 Create ProjectAttachmentService
**File:** `src/ShopBoss.Web/Services/ProjectAttachmentService.cs`
```
- UploadAttachmentAsync(projectId, file, category)
- GetAttachmentsAsync(projectId)
- DownloadAttachmentAsync(id)
- DeleteAttachmentAsync(id)
```

### 2.4 Update Navigation
- Add Project link to main navigation
- Update Admin menu structure

## Phase 3: Frontend Views & UI (High Priority)

### 3.1 Create Project Management Views
**Files:** `src/ShopBoss.Web/Views/Project/`

**Index.cshtml** - Main project list (based on Admin/Index.cshtml)
- Project table with expandable/collapsible Work Order rows
- Search projects by name, job number, contact
- Archive toggle, bulk operations
- Actions: Create, Edit, View Details, Archive, Delete

**Create.cshtml** - New project form
- Project details form
- Option to immediately attach existing unassigned work orders

**Edit.cshtml** - Edit project form  
- Update project details
- Manage work order associations
- File attachment management

**Details.cshtml** - Project details view
- Project information display
- Work Orders table (nested within project)
- File attachments section with upload/download

### 3.2 Create Work Order Association Interface
**Partial:** `src/ShopBoss.Web/Views/Shared/_WorkOrderAssociation.cshtml`
- Modal or inline interface for attaching/detaching work orders
- Search available unassigned work orders
- Current project work orders list

### 3.3 Create File Attachment Interface  
**Partial:** `src/ShopBoss.Web/Views/Shared/_ProjectAttachments.cshtml`
- File upload with drag-and-drop
- Category selection (dropdown)
- Attachments list with download/delete actions
- File type validation (PDF focus)

### 3.4 Update Work Order Views
- Show project association in work order details
- Add "Change Project" option in work order edit

## Phase 4: File Management & Storage (Medium Priority)

### 4.1 Create File Storage Structure
- Directory: `wwwroot/uploads/projects/{projectId}/`
- Secure file naming and validation
- File size limits and type restrictions

### 4.2 Implement File Operations
- Secure upload endpoint with validation
- Download endpoint with access control  
- File deletion with cascade to attachments
- Cleanup orphaned files

## Phase 5: Integration & Testing (Medium Priority)

### 5.1 Update Import Workflow
- Option to assign imported work orders to projects
- Auto-assignment based on naming patterns (optional)

### 5.2 Update Admin Workflows
- Project-based work order management
- Statistics and reporting by project
- Archive/delete with project considerations

### 5.3 Data Migration Support
- Tool to bulk-assign existing work orders to projects
- Import/export project data

## Phase 6: Advanced Features (Low Priority)

### 6.1 Enhanced Project Features
- Project templates
- Project status tracking  
- Due dates and milestones
- Project notes and comments

### 6.2 Advanced File Management
- File versioning
- Preview for certain file types
- File organization within projects

### 6.3 Reporting & Analytics
- Project completion reports
- File usage analytics
- Project timeline views

## Implementation Order
1. **Database & Models** (Phase 1) - Foundation
2. **Core Backend Services** (Phase 2.1-2.2) - Business logic
3. **Basic Project Views** (Phase 3.1) - UI foundation  
4. **Work Order Association** (Phase 2.4, 3.2) - Core functionality
5. **File Attachments** (Phase 2.3, 3.3, 4) - File management
6. **Integration** (Phase 5) - Polish and testing
7. **Advanced Features** (Phase 6) - Future enhancements

## Dependencies
- Current Admin Work Order List implementation (template)
- File upload/management patterns from Import system
- Existing WorkOrder and AdminController patterns
- Bootstrap 5 UI components

## Database Migration Strategy
- New tables: Projects, ProjectAttachments
- Add nullable ProjectId to WorkOrders
- Existing WorkOrders remain unassigned (ProjectId = null)
- Provide tools to assign work orders to projects post-migration

## File Storage Considerations
- Store files outside wwwroot for security
- Implement proper access control
- Consider cloud storage for production
- File cleanup on project deletion

This plan provides a comprehensive, phased approach to implementing the Project entity while maintaining system stability and following existing architectural patterns.