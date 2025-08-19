BEGIN TRANSACTION;

ALTER TABLE WorkOrders ADD COLUMN ProjectId TEXT;

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

COMMIT;