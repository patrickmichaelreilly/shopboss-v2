# Project Entity Implementation Plan
# CRITICAL:::: THERE IS NO DATA TO MIGRATE. DON'T CREATE OR RUN MIGRATIONS. 

## Overview
Introduce a new top-level **Project** entity that contains Work Orders and file attachments, providing better organization for real-world job management.

## Phase 1: Database Schema & Models (High Priority)

### 1.1 Create Project Entity
**File:** `src/ShopBoss.Web/Models/Project.cs`
```
- Id (string, Primary Key) - GUID for internal use
- ProjectId (string) - User-entered project number (e.g., "J001", "J123")
- ProjectName (string)
- BidRequestDate (DateTime?, nullable)
- ProjectAddress (string, nullable) 
- ProjectContact (string, nullable)
- ProjectContactPhone (string, nullable)
- ProjectContactEmail (string, nullable)
- GeneralContractor (string, nullable)
- ProjectManager (string, nullable)
- TargetInstallDate (DateTime?, nullable)
- ProjectCategory (ProjectCategory enum)
- Installer (string, nullable)
- Notes (string, nullable)
- CreatedDate (DateTime)
- IsArchived (bool)
- ArchivedDate (DateTime?, nullable)
- List<WorkOrder> WorkOrders (Navigation)
- List<ProjectAttachment> Attachments (Navigation)
```

### 1.1.1 Create ProjectCategory Enum
**File:** `src/ShopBoss.Web/Models/ProjectCategory.cs`
```
public enum ProjectCategory
{
    StandardProducts,
    CustomProducts,
    SmallProject
}
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
- Add unique index on ProjectId to prevent duplicates

### 1.5 Migration
- No data migration required - all data will be wiped upon release of this feature.

## Phase 2: Backend Services & Controllers (High Priority)

### 2.1 Create ProjectService
**File:** `src/ShopBoss.Web/Services/ProjectService.cs`
```
- GetProjectSummariesAsync(search, includeArchived, projectCategory)
- GetProjectByIdAsync(id) 
- CreateProjectAsync(project)
- UpdateProjectAsync(project)
- ArchiveProjectAsync(id)
- UnarchiveProjectAsync(id)
- DeleteProjectAsync(id)
- AttachWorkOrdersToProjectAsync(workOrderIds, projectId)
- DetachWorkOrderFromProjectAsync(workOrderId)
```

### 2.2 Create ProjectController
**File:** `src/ShopBoss.Web/Controllers/ProjectController.cs`
- Index (GET) - Main expandable project list view
- Create (POST) - Create new project (AJAX)
- Update (POST) - Update project details (AJAX)
- Archive/Unarchive (POST) - Toggle archive status
- Delete (POST) - Delete project
- AttachWorkOrders (POST) - Attach multiple work orders (AJAX)
- DetachWorkOrder (POST) - Remove work order association
- UploadFile (POST) - Handle file uploads
- DownloadFile (GET) - Serve file downloads

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

### 3.1 Create Unified Project Management View
**File:** `src/ShopBoss.Web/Views/Project/Index.cshtml`
- Tree-structured project list with expandable rows (based on Admin/Index.cshtml)
  - Collapsed: Shows Project ID, Project Name, Project Category, Target Install Date, Work Order Count, File Count
  - Expanded: Shows inline details card with three sections:
    1. **Project Info**: All Smart Sheet fields in editable form (bid date, project address, contacts, contractor, PM, install date, category, installer, notes)
    2. **Work Orders**: List of associated orders + \"Associate Work Orders\" button
    3. **Files**: Attachment list + upload area with category dropdown
- Search projects by Project ID, name, project address, contact, contractor
- Filter by Project Category and Archive status
- Actions: Create New (modal), Save (when editing inline), Archive, Delete

### 3.2 Create Shared Project Form Component
**File:** `src/ShopBoss.Web/Views/Project/_ProjectForm.cshtml`
- Reusable form partial for both Create modal and inline editing
- All Smart Sheet fields: Project ID, Project Name, Bid Request Date, Project Address, Project Contact (name/phone/email), General Contractor, Project Manager, Target Install Date, Project Category dropdown, Installer, Notes
- Form layout modeled after Import Preview/Modify pattern
- Date pickers for Bid Request Date and Target Install Date
- Project Category as dropdown with Standard Products/Custom Products/Small Project options

### 3.3 Work Order Association (Inline)
- Simple dropdown/modal within expanded project card
- List unassigned work orders with checkbox selection
- One-click association without leaving the page

### 3.4 File Attachments (Inline)
- Upload area within expanded project card
- Simple file list with download links
- Direct file system access via logical folder structure

## Phase 4: File Management & Storage (Medium Priority)

### 4.1 Create File Storage Structure
- Directory: `{AppRoot}/ProjectFiles/{ProjectId}/{Category}/`
- Logical folder structure accessible via network file shares
- Simple file naming (preserve original names where possible)  
- No file size limits (internal tool)
- Users can browse/copy files directly via Windows Explorer or network drive

### 4.2 Implement File Operations
- Simple upload endpoint (leverage LAN speed)
- Direct file serving for downloads (utilize file:// links if possible)
- Basic file deletion
- Folder cleanup on project deletion

## Implementation Order
1. **Database & Models** (Phase 1) - Add Project and ProjectAttachment entities with Smart Sheet fields
2. **Core Services** (Phase 2) - ProjectService and ProjectAttachmentService  
3. **Single View UI** (Phase 3) - Expandable Project Index with inline everything
4. **File Storage** (Phase 4) - Simple folder-based storage

## Dependencies
- Current Admin Work Order List implementation (template)
- File upload/management patterns from Import system
- Existing WorkOrder and AdminController patterns
- Bootstrap 5 UI components

## Key Design Principles
- **Minimal Changes**: Confine changes to new files only (except WorkOrder model)
- **Single Page Experience**: Everything happens in the expandable Project Index view
- **Component Reuse**: Share form components between Create/Edit (like Import Preview/Modify pattern)
- **Simple File Access**: Direct file system folders for easy external access
- **LAN Optimized**: Leverage local network for fast file transfers
- **MVP Focus**: Skip error handling, edge cases, animations - just core functionality

## File Storage Strategy
- Store in accessible folder structure: `ProjectFiles/{ProjectId}/{Category}/`
- Users can browse files directly via network shares
- Simple file serving without complex access controls (internal tool)
- Leverage LAN for fast transfers

This streamlined plan focuses on delivering core Project functionality with minimal disruption to existing code, using a single expandable view pattern to reduce complexity.

## What We're NOT Doing (MVP Approach)
- No separate Details/Edit/Create views - everything inline
- No complex error handling or edge cases
- No animations or fancy UI transitions  
- No file preview or versioning
- No complex access controls (internal LAN tool)
- No Work Order changes except adding ProjectId field
- No modifications to Import or Modify Work Order interfaces (we'll integrate after MVP)
- No separate integration phase - jump straight to full Work Order integration once MVP works