# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ShopBoss v2 is a modern shop floor tracking system that replaces the discontinued Production Coach software. It manages millwork manufacturing workflow from CNC cutting through assembly and shipping, with hierarchical data import from Microvellum SDF files.

## Development Commands

### Build and Run
```bash
# Build the application
dotnet build src/ShopBoss.Web/ShopBoss.Web.csproj

# Run the application (development: localhost only)
dotnet run --project src/ShopBoss.Web/ShopBoss.Web.csproj

# Run with watch for development (localhost only)
dotnet watch --project src/ShopBoss.Web/ShopBoss.Web.csproj

# Run for LAN access during development (if needed)
dotnet run --project src/ShopBoss.Web/ShopBoss.Web.csproj --launch-profile http-lan

# Published applications automatically bind to all interfaces (0.0.0.0:5000)
```

### Database Management
```bash
# Create a new migration
dotnet ef migrations add [MigrationName] --project src/ShopBoss.Web

# Update database
dotnet ef database update --project src/ShopBoss.Web

# Drop database (development only)
dotnet ef database drop --project src/ShopBoss.Web
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# IMPORTANT: Testing Procedure
# 1. Claude does the work and builds the application
# 2. Claude updates the worklog following Collaboration-Guidelines.md
# 3. Claude tells the user what to test for
# 4. USER manually runs ./deploy-to-windows.sh to deploy for testing
# Claude should NEVER run deploy-to-windows.sh - only the user does this
```

## Architecture Overview

### Technology Stack
- **Framework**: ASP.NET Core 8.0 MVC
- **Database**: Entity Framework Core 9.0.0 with SQLite (development)
- **Real-time**: SignalR for progress updates and status notifications
- **Frontend**: Bootstrap 5 with vanilla JavaScript
- **Import Processing**: External x86 process execution for SDF file conversion

### Core Components

#### Data Models
- **Hierarchical Structure**: WorkOrder → Products → Parts/Subassemblies → Parts
- **Status Tracking**: Pending → Cut → Sorted → Assembled → Shipped
- **Storage Management**: StorageRack → Bins with capacity tracking
- **Audit System**: Complete audit trail for all operations

#### Key Services
- `ImporterService`: Handles SDF file processing and data transformation
- `SortingRuleService`: Manages intelligent part placement in storage racks
- `PartFilteringService`: Routes doors/drawer fronts to specialized racks
- `AuditTrailService`: Tracks all system operations and status changes

#### Controllers and Views
- **AdminController**: Work order management and system configuration
- **CncController**: Nest sheet scanning and batch part marking
- **SortingController**: Rack visualization and part placement
- **AssemblyController**: Product completion and component guidance
- **HomeController**: Main dashboard and navigation

### Database Schema

#### Core Entities
- `WorkOrder`: Top-level container for manufacturing jobs
- `Product`: Individual products within a work order
- `Part`: Individual components with status tracking
- `Subassembly`: Nested component groupings (2 levels max)
- `Hardware`: Non-manufactured items (hinges, screws, etc.)
- `DetachedProduct`: Standalone products not part of main hierarchy
- `NestSheet`: CNC cutting sheets with barcode tracking

#### Support Entities
- `StorageRack`: Physical storage locations with configurable dimensions
- `Bin`: Individual storage compartments within racks
- `AuditLog`: Complete audit trail with timestamps and details
- `ScanHistory`: Barcode scan operations and results

### Import Architecture

#### SDF Processing Pipeline
1. **File Upload**: Drag-and-drop interface with validation
2. **Process Execution**: x86 importer tool runs as separate process
3. **Data Extraction**: Hierarchical data parsed from SQLite output
4. **Tree Visualization**: Interactive expandable tree display
5. **Selective Import**: Admin chooses specific items to import
6. **Database Persistence**: Transaction-based import with rollback

#### Progress Tracking
- Real-time updates via SignalR (`/importProgress` hub)
- Multi-stage progress: SDF conversion → SQL cleanup → JSON generation
- Cancellation support with proper cleanup
- Detailed error reporting and recovery suggestions

### Shop Floor Workflow

#### Station Types
1. **CNC Station**: Nest sheet scanning, batch part marking as "Cut"
2. **Sorting Station**: Intelligent rack assignment, part placement guidance
3. **Assembly Station**: Product completion tracking, component location guidance
4. **Shipping Station**: Final verification, work order completion

#### Status Transitions
- **Pending**: Initial state after import
- **Cut**: Marked when nest sheet is scanned at CNC station
- **Sorted**: Updated when part is placed in storage rack
- **Assembled**: Set when product assembly is completed
- **Shipped**: Final state when loaded for shipping

### Real-time Updates

#### SignalR Hubs
- `/importProgress`: Import operation progress and status
- `/hubs/status`: Cross-station status updates and notifications

#### Update Patterns
- Status changes broadcast to all connected clients
- Assembly readiness notifications to assembly station
- Inventory updates to sorting station displays
- Active work order changes reflected across all stations

## Development Patterns

### Coding Standards
- Use async/await patterns for all database operations
- Implement proper error handling with user-friendly messages
- Follow existing naming conventions and project structure
- Maintain responsive design optimized for shop floor tablets
- Ensure all scan operations are properly audited

### Service Integration
- All database operations go through `ShopBossDbContext`
- Status changes must be logged via `AuditTrailService`
- Real-time updates sent through appropriate SignalR hubs
- Active work order context maintained across all operations

### UI Patterns
- Bootstrap 5 components with custom CSS for shop floor optimization
- Responsive design with tablet-first approach
- Clear visual feedback for all user actions
- Consistent navigation header across all station interfaces

## Key Business Rules

### Data Integrity
- Preserve exact Microvellum identifiers throughout system
- Maintain parent-child relationships in hierarchical data
- Prevent orphaned selections during import process
- Ensure all status transitions are properly audited

### Smart Sorting Logic
- Doors, drawer fronts, and adjustable shelves route to specialized racks
- Carcass parts grouped by product for assembly efficiency
- Automatic bin assignment based on capacity and product grouping
- Assembly readiness calculated excluding filtered parts

### Import Validation
- Duplicate work order detection and rejection
- Complete hierarchy validation before import
- Business rule enforcement for incomplete selections
- Graceful handling of corrupted or invalid SDF files

## Testing Approach

### Integration Testing
- End-to-end workflow validation (CNC → Sorting → Assembly → Shipping)
- Real-time update verification across all stations
- Active work order consistency testing
- Database integrity validation

### Performance Testing
- Large hierarchy rendering (1000+ items)
- Concurrent user operations
- Import processing for large SDF files
- Real-time update performance under load

## Deployment Notes

### Environment Configuration
- Development: SQLite database with auto-migration
- Production: SQL Server with manual migration management
- Process isolation for x86 importer tool execution
- Secure temporary file handling with automatic cleanup

### Security Considerations
- Input validation for all file uploads
- Process isolation for external tool execution
- Audit trail for all administrative actions
- Role-based access control for admin functions

## Current Development Status

The system is in active development with core infrastructure complete. Recent phases have focused on:
- Complete admin station with work order management
- CNC station with nest sheet scanning and batch operations
- Sorting station with intelligent rack assignment and visualization
- Assembly station with product completion workflow
- Shipping station with final verification and tracking

Current development follows the phased approach outlined in `Phases.md` with detailed progress tracking in `Worklog.md`.