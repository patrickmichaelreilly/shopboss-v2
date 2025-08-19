-- ShopBoss v2 Production Database Migration
-- Migrates production database to match current schema
-- Run this against the production database BEFORE deploying new build

BEGIN TRANSACTION;

-- Add ProjectId column to existing WorkOrders table (nullable for existing records)
ALTER TABLE WorkOrders ADD COLUMN ProjectId TEXT;

-- Create Projects table
CREATE TABLE Projects (
    Id TEXT PRIMARY KEY NOT NULL,
    ProjectId TEXT NOT NULL,
    ProjectName TEXT NOT NULL,
    BidRequestDate TEXT,
    ProjectAddress TEXT,
    ProjectContact TEXT,
    ProjectContactPhone TEXT,
    ProjectContactEmail TEXT,
    GeneralContractor TEXT,
    ProjectManager TEXT,
    TargetInstallDate TEXT,
    ProjectCategory INTEGER NOT NULL,
    Installer TEXT,
    Notes TEXT,
    CreatedDate TEXT NOT NULL,
    IsArchived INTEGER NOT NULL,
    ArchivedDate TEXT
);

-- Create CustomWorkOrders table (optional foreign key to Projects)
CREATE TABLE CustomWorkOrders (
    Id TEXT PRIMARY KEY NOT NULL,
    ProjectId TEXT,
    Name TEXT NOT NULL,
    WorkOrderType INTEGER NOT NULL,
    Description TEXT NOT NULL,
    AssignedTo TEXT,
    EstimatedHours TEXT,
    ActualHours TEXT,
    Status INTEGER NOT NULL,
    StartDate TEXT,
    CompletedDate TEXT,
    Notes TEXT,
    CreatedDate TEXT NOT NULL,
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id)
);

-- Create PurchaseOrders table (optional foreign key to Projects)
CREATE TABLE PurchaseOrders (
    Id TEXT PRIMARY KEY NOT NULL,
    ProjectId TEXT,
    PurchaseOrderNumber TEXT NOT NULL,
    VendorName TEXT NOT NULL,
    VendorContact TEXT,
    VendorPhone TEXT,
    VendorEmail TEXT,
    Description TEXT NOT NULL,
    OrderDate TEXT NOT NULL,
    ExpectedDeliveryDate TEXT,
    ActualDeliveryDate TEXT,
    TotalAmount TEXT,
    Status INTEGER NOT NULL,
    Notes TEXT,
    CreatedDate TEXT NOT NULL,
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id)
);

-- Create ProjectAttachments table (foreign key to Projects)
CREATE TABLE ProjectAttachments (
    Id TEXT PRIMARY KEY NOT NULL,
    ProjectId TEXT NOT NULL,
    FileName TEXT NOT NULL,
    OriginalFileName TEXT NOT NULL,
    FileSize INTEGER NOT NULL,
    ContentType TEXT NOT NULL,
    Category TEXT NOT NULL,
    UploadedDate TEXT NOT NULL,
    UploadedBy TEXT,
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id)
);

-- Create ProjectEvents table (foreign key to Projects)
CREATE TABLE ProjectEvents (
    Id TEXT PRIMARY KEY NOT NULL,
    ProjectId TEXT NOT NULL,
    EventDate TEXT NOT NULL,
    EventType TEXT NOT NULL,
    Description TEXT NOT NULL,
    CreatedBy TEXT,
    RowNumber INTEGER,
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id)
);

-- Create PartLabels table (integrated with CNC station)
-- Links to existing Parts and WorkOrders
CREATE TABLE PartLabels (
    Id INTEGER PRIMARY KEY NOT NULL,
    PartId TEXT NOT NULL,
    WorkOrderId TEXT NOT NULL,
    NestSheetId TEXT,
    LabelHtml TEXT NOT NULL,
    ImportedDate TEXT NOT NULL,
    FOREIGN KEY (PartId) REFERENCES Parts(Id),
    FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id)
);

-- Add optional foreign key constraint to WorkOrders.ProjectId
-- Note: SQLite doesn't support adding foreign key constraints to existing tables
-- This would be enforced at the application level in the new build

COMMIT;

-- Verification queries (run these after migration to confirm success)
-- SELECT COUNT(*) as ProjectsCount FROM Projects;
-- SELECT COUNT(*) as CustomWorkOrdersCount FROM CustomWorkOrders;
-- SELECT COUNT(*) as PurchaseOrdersCount FROM PurchaseOrders;
-- SELECT COUNT(*) as ProjectAttachmentsCount FROM ProjectAttachments;
-- SELECT COUNT(*) as ProjectEventsCount FROM ProjectEvents;
-- SELECT COUNT(*) as PartLabelsCount FROM PartLabels;
-- SELECT COUNT(*) as WorkOrdersWithProjectId FROM WorkOrders WHERE ProjectId IS NOT NULL;
-- 
-- Check that new column was added:
-- PRAGMA table_info(WorkOrders);