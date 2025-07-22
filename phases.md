# Fast Importer Development Phases

## Original vs Actual Pipeline

**Key Insight:** ImportData class is unnecessary. FastSdfReader produces `List<Dictionary<string, object?>>` which WorkOrderImportService already consumes.

---

## Phase 1: Direct SDF Reader - COMPLETE

### **Delivered**
- `tools/fast-sdf-reader/FastSdfReader.cs` - OLE DB-based direct SDF reader
- `tools/fast-sdf-reader/SqlCeConnectionWrapper.cs` - Connection management  
- `test/FastSdfReader.exe` - Compiled x86 executable
- **Performance:** 0.16 seconds vs 110+ seconds (350x improvement)

### **Validated Data Extraction**

### **Architecture Decisions**
- **OLE DB over P/Invoke:** Simpler, more reliable connection
- **Column-level selectivity:** Skip BLOB columns causing conversion errors  
- **x86 required:** SQL CE 4.0 OLE DB provider limitation
- **Raw dictionary output:** Direct compatibility with existing WorkOrderImportService

---

## Phase 2: Build Parallel FastImportService

### **Objective**  
Build a parallel import pipeline alongside the existing system, with drop-in replacement at the `session.WorkOrderEntities` bolt-on point.

### **Current System Understanding**
```csharp
// Existing Pipeline (110+ seconds)
SDF → ImporterService → ImportData → WorkOrderImportService → session.WorkOrderEntities → Import Preview

// Bolt-on Point: session.WorkOrderEntities (in-memory WorkOrder entity)
// Import Preview TreeView reads from session.WorkOrderEntities after page refresh
```

### **New Parallel Pipeline**
```csharp
// New Pipeline (0.2 seconds)  
SDF → FastSdfReader → FastImportService → session.WorkOrderEntities → Same Import Preview
```

### **Deliverables**
- **Create `FastImportService.cs`** (parallel to ImporterService)  
  - Method: `ImportSdfFileAsync(string sdfPath, string workOrderName)` returns `WorkOrder`
  - Orchestrate FastSdfReader.exe execution and output parsing
  - Transform parsed data using existing `WorkOrderImportService.TransformToWorkOrderAsync()`
  - Handle progress reporting and error handling

- **Modify `FastSdfReader.cs`**
  - Change from console output to structured JSON output  
  - Return format: JSON with "Products", "Parts", "PlacedSheets", "Hardware", "Subassemblies", "OptimizationResults" arrays
  - Maintain same OLE DB extraction performance (~0.16s)

- **Add `POST /Import/FastStart` endpoint** (parallel to `/Import/Start`)
  - Use FastImportService instead of ImporterService
  - Set `session.WorkOrderEntities` identically to current system
  - Same SignalR progress reporting and UX flow

### **Parallel Development Benefits** 
- ✅ **Zero Risk:** Current system remains untouched during development
- ✅ **A/B Testing:** Can compare both pipelines side by side
- ✅ **Easy Rollback:** Route back to old endpoint if issues arise
- ✅ **Same UX:** Import Preview TreeView works identically

### **Success Metrics**
- ✅ `session.WorkOrderEntities` contains identical WorkOrder as current system
- ✅ Import Preview TreeView renders identically  
- ✅ Sub-0.5 second total import time vs 110+ seconds
- ✅ All existing WorkOrderImportService transformations preserved

---

## Phase 3: FastImportService Integration  

### **Objective**
Wire FastSdfReader into ImportController with proper progress reporting and error handling.

### **Deliverables**
- **Create `FastImportService.cs`**
  - Orchestrate FastSdfReader → WorkOrderImportService pipeline
  - Handle file validation and path security
  - Provide progress reporting for SignalR (simulated for 0.3s process)
  - Error handling with graceful fallback

- **Update `ImportController.cs`** 
  - Add `POST /Import/FastImport` endpoint  
  - Keep existing `/Import/Start` as fallback
  - Route selection based on file size or user preference
  - Maintain identical TreeDataResponse output

### **Integration Points**
- **SignalR Progress:** `ImportHub` notifications for fast import stages
- **Session Management:** `ImportSession` tracking for both pipelines
- **Error Handling:** Fallback to ExportSqlCe40.exe if FastSdfReader fails

### **Success Metrics**
- ✅ Sub-0.5 second end-to-end import including UI updates
- ✅ Identical TreeView rendering as existing import
- ✅ Proper SignalR progress notifications
- ✅ Graceful fallback on FastSdfReader failures

---

## Phase 4: Production Ready

### **Objective**  
Polish for production deployment with comprehensive error handling and monitoring.

### **Deliverables**
- **Error Handling**
  - Malformed SDF file detection
  - Missing table/column validation  
  - OLE DB connection failure recovery
  - Detailed error logging with context

- **Performance Monitoring**
  - Import time tracking and logging
  - Table extraction metrics
  - Memory usage monitoring
  - Success/failure rate tracking

- **User Experience** 
  - Import speed indicator in UI
  - File size-based pipeline recommendations
  - Clear error messages for users
  - Progress accuracy for fast imports

### **Success Metrics**
- ✅ 99.9% uptime with graceful error handling
- ✅ Clear user feedback on import progress and errors
- ✅ Performance regression detection
- ✅ Production logging and monitoring ready

---

## Architecture Notes

### **Data Flow Comparison**

#### **Old Pipeline (110+ seconds)**
```
SDF File → ExportSqlCe40.exe → SQL Script → SQLite DB → JSON String → ImportData → WorkOrder
```

#### **New Pipeline (0.2 seconds)**
```  
SDF File → FastSdfReader (OLE DB) → Raw Dictionaries → WorkOrder
```

### **Critical Preservation**
- TreeItem structure must remain identical (WorkOrderTreeView.js:126-148)
- Category nodes with expand/collapse functionality  
- Status/Category dropdowns for modify mode
- Delete buttons with proper callbacks
- All existing WorkOrderImportService transformations:
  - Product quantity expansion (Qty > 1 → multiple instances)
  - Part categorization and filtering
  - NestSheet relationship establishment
  - Single-part product handling as DetachedProducts

### **Technology Stack**
- **FastSdfReader:** C# .NET 8 console app (x86 for SQL CE compatibility)
- **OLE DB Provider:** Microsoft.SQLSERVER.CE.OLEDB.4.0  
- **Integration:** ASP.NET Core SignalR for progress reporting
- **Fallback:** Existing ExportSqlCe40.exe pipeline for error recovery

### **Performance Expectations**
- **Import Speed:** 110+ seconds → 0.2-0.5 seconds (220-550x improvement)
- **Memory Usage:** Lower (no JSON intermediate objects)
- **User Experience:** Near-instant imports with real progress reporting