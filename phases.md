# Fast Importer Development Phases

## Current Import Pipeline Analysis
- **Current Flow**: SDF → ExportSqlCe40.exe → SQL (28MB w/BLOBs) → Clean SQL → SQLite → JSON → WorkOrder Entities → TreeDataResponse
- **Performance**: 30s (Converting SDF) + 30s (Cleaning SQL) + 30s (Generating JSON) + 20s (Complete) = ~110s total
- **Key Bottleneck**: Processing unnecessary BLOB columns that are immediately discarded
- **x86 Constraint**: SQL CE 4.0 requires x86 architecture (current tool runs in win-x86)
- **Dev Environment**: WSL requires Windows exe testing via cmd.exe or PowerShell

## Phase 1: Build Custom SDF Reader (Week 1) 

### Problem Statement
ExportSqlCe40.exe is fundamentally slow and processes all data regardless of parameters. Need direct SDF access for real performance gains.

### Deliverables
- `tools/fast-sdf-reader/FastSdfReader.cs` - Direct SqlCeConnection-based reader
- `tools/fast-sdf-reader/Program.cs` - Console app for testing
- `test-fast-sdf.bat` - Windows batch script to test with real SDF files
- Console output validation of our 6 required tables

### Required Tables & Columns (from codebase analysis)
```
Products: Id, Name, ItemNumber, Quantity
Parts: Id, Name, Width, Height, Thickness, Material, ProductId, SubassemblyId  
PlacedSheets: Id, FileName, Material, Length, Width, Thickness
Hardware: Id, Name, Quantity, ProductId, SubassemblyId
Subassemblies: Id, Name, Quantity, ProductId, ParentSubassemblyId
OptimizationResults: LinkIDPart, LinkIDSheet
```

### Implementation Approach (Dirty & Simple)
1. **Direct SqlCeConnection** - No external process overhead
2. **Column-level selectivity** - Only SELECT columns we need, skip ALL BLOBs
3. **Console output first** - Verify we can read the data before formatting
4. **Windows batch test script** - Feed it SDF files from test-data

### Based on ErikEJ SqlCeToolbox Analysis
- Use DBRepository.cs patterns for connection management
- Leverage parameterized queries and data readers
- Apply selective table querying techniques
- Skip intermediate files entirely

### Success Metrics
- Console shows data from all 6 required tables
- No BLOB columns processed
- Faster execution than ExportSqlCe40.exe (minimal baseline)
- Stable connection to SDF files without crashes

## Stage 2: Skip JSON, Direct to Entities (Week 1-2)

### Deliverables
- Modified `ImporterService.cs` to bypass JSON generation
- Direct SQLite → WorkOrder entity transformation
- Reuse existing `WorkOrderImportService` transformation logic
- Stream data from SQLite reader to entities

### Architecture Changes
```csharp
// Current: SQLite → JSON → ImportData → WorkOrder
// New: SQLite → WorkOrder (skip JSON serialization/deserialization)
```

### Implementation Approach
- Modify `SdfImporter.ImportAsync()` to return WorkOrder directly
- Use SQLiteDataReader for streaming (don't load all data into memory)
- Apply transformations during read (quantity expansion, categorization)

### Success Metrics
- 30-40% faster by eliminating JSON stage
- Lower memory usage (streaming vs loading entire JSON)
- Same exact entity output as current system

## Stage 3: Advanced Optimizations (Week 2-3)

### Deliverables
- Investigate direct SDF access if Stage 1-2 gains aren't sufficient
- Consider caching frequently imported files by hash
- Parallel processing of independent tables
- Progress reporting based on actual work, not simulated timers

### Potential Approaches
- Use ErikEJ's SqlCeToolbox libraries directly (if available as NuGet)
- Memory-mapped file access for large SDF files
- Background pre-processing when file is selected

### Integration Strategy
- Keep all optimizations behind feature flags
- Maintain backward compatibility
- Progressive rollout to beta users

### Success Metrics
- Sub-5 second imports for typical files
- Real progress reporting (not simulated)
- Stable performance across file sizes

## Architecture Notes

### Current Data Flow (ImportController.cs:490-509)
```csharp
ImporterService.ImportSdfFileAsync() → ImportData (JSON)
WorkOrderImportService.TransformToWorkOrderAsync() → WorkOrder entities  
BuildTreeFromWorkOrderEntities() → TreeDataResponse
```

### New Data Flow
```csharp
FastImportService.ImportSdfFileAsync() → WorkOrder entities directly
BuildTreeFromWorkOrderEntities() → TreeDataResponse (unchanged)
```

### Critical Preservation
- TreeItem structure must remain identical (WorkOrderTreeView.js:126-148)
- Category nodes with expand/collapse functionality
- Status/Category dropdowns for modify mode
- Delete buttons with proper callbacks