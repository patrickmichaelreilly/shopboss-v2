1. SDF File → ExportSqlCe40.exe → SQL Script (with BLOBs)
2. SQL Script → SQL Cleaning → Clean SQL (BLOBs removed)
3. Clean SQL → sqlite3.exe → SQLite DB
4. SQLite DB → JSON Export → ImportData (JSON)
5. ImportData → WorkOrderImportService → WorkOrder Entities
6. WorkOrder Entities → TreeDataResponse → Work Order Tree View    