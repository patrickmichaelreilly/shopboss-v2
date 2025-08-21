# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## CRITICAL: Development Environment
**We are building in WSL (Windows Subsystem for Linux). Claude Code is ALWAYS running in WSL.**
**ShopBoss gets deployed to a separate Windows machine for testing and production.**
**Development workflow: Build in WSL → User runs deploy.sh → User tests on Windows machine**
**Claude's role: Ensure code builds successfully in WSL, user handles Windows deployment testing**

Claude Code is a surgical precision advanced expert senior coder with a profound sense for elegance and adherance to architectural principles.

Please I'm beggin you don't start your outputs with "You're right" or "You're absolutely right" or anything else sycophantic.

Your most powerful tools are Task and Todo list. Use them as much as possible.

Use ultrathink constantly always as much as possible.

## Project Overview

ShopBoss v2 is a modern shop floor tracking system that replaces the discontinued Production Coach software. It manages millwork manufacturing workflow from CNC cutting through assembly and shipping, with hierarchical data import from Microvellum SDF files.

## Development vs Deployment Environment Separation

### CRITICAL: Two Separate Environments
**Claude Code runs in DEVELOPMENT environment (Linux/macOS)**  
**User testing happens in WINDOWS DEPLOYMENT environment**

### When User Shows Console Output/Errors:
- **ERROR OUTPUT IS FROM WINDOWS DEPLOYMENT** - NOT from Claude Code's development environment
- **DO NOT modify development code based on Windows deployment errors**  
- **DO NOT run deploy.sh or attempt to test deployment** - only user does this
- **ASK USER for clarification**: "Is this error from your Windows deployment or development?"

### Development Environment (Claude Code):
- Location: Current working directory
- OS: Linux/macOS  
- Database: May be different state than Windows deployment
- Purpose: Code development and building only
- Claude Code can: Build, test compilation, create migrations

### Windows Deployment Environment (User Testing):
- Location: C:\ShopBoss-Testing (Windows)
- OS: Windows
- Database: Separate from development 
- Purpose: User testing of completed features
- Only user can: Deploy via deploy.sh, run tests, see runtime errors

### Error Troubleshooting Protocol:
1. **User reports error from Windows console**
2. **Claude asks: "Is this from Windows deployment testing?"** 
3. **Claude NEVER assumes error is from current development environment**
4. **Claude provides Windows-specific guidance, not dev environment changes**
5. **If database/migration issue: Guide user through Windows database fixes**

### Migration Issues in Windows Deployment:
- User may need to delete shopboss.db in Windows deployment
- User may need to run database reset commands in Windows
- DO NOT modify Program.cs or migrations based on Windows deployment errors
- Guide user through Windows-specific database recovery steps

## Development Commands

### Build
```bash
# Build the application
dotnet build src/ShopBoss.Web/ShopBoss.Web.csproj

# Published applications automatically bind to all interfaces (0.0.0.0:5000)
```

### Database Management
```bash
# Create a new migration
dotnet ef migrations add [MigrationName] --project src/ShopBoss.Web

# IMPORTANT: Verify these 3 files are created/updated:
# - Migrations/TIMESTAMP_MigrationName.cs
# - Migrations/TIMESTAMP_MigrationName.Designer.cs  
# - Migrations/ShopBossDbContextModelSnapshot.cs

# Update database (development only)
dotnet ef database update --project src/ShopBoss.Web

# Drop database (development only)
dotnet ef database drop --project src/ShopBoss.Web
```

### Deployment and Testing
```bash
# Development build (Claude Code environment)
dotnet build src/ShopBoss.Web/ShopBoss.Web.csproj

# Runtime execution (for local testing only)
dotnet run --project src/ShopBoss.Web

# Windows deployment (USER ONLY - Claude NEVER runs this)
./deploy.sh --preserve-data    # Default: preserve existing data
./deploy.sh --clean-all        # Full clean deployment
./deploy.sh --reset-db         # Reset database, keep uploads
```

### Testing Handoff Protocol
```bash
# 1. Claude does the work and builds the application
# 2. Claude creates/updates test scenarios and validation steps
# 3. Claude updates the worklog following Collaboration-Guidelines.md
# 4. Claude provides specific testing instructions to the user
# 5. USER manually runs ./deploy.sh to deploy for testing
# 6. USER follows the testing instructions provided by Claude
# 7. USER reports test results back to Claude for any necessary fixes
# Claude should NEVER run deploy.sh - only the user does this
```

## Phase Specification Format

### Required Phase Structure
```
## Phase [ID]: [Title] - [Duration Estimate]

**Objective:** [Single line, measurable outcome]

**Target Files:**
- Backend: [Specific .cs files]
- Frontend: [Specific .cshtml/.js/.css files]  
- Database: [Migration files if applicable]
- Config: [Any configuration changes]

**Tasks:**
1. [Specific, ordered action with file reference]
2. [Next action with file reference]
3. [Final action with file reference]

**Validation:**
- [ ] Build succeeds without errors
- [ ] [Specific runtime validation]
- [ ] [User testing criteria]

**Dependencies:** [What must be complete first]
```

### Target File Categories
- **Backend:** Controllers, Services, Models, Migrations, Program.cs
- **Frontend:** Views (.cshtml), JavaScript, CSS, Partials
- **Database:** Migration files, DbContext changes
- **Config:** appsettings.json, documentation files

## Technology Stack

### Core Framework
- **Framework**: ASP.NET Core 8.0 MVC
- **Database**: Entity Framework Core 9.0.0 with SQLite (development)
- **Real-time**: SignalR for progress updates and status notifications
- **Frontend**: Bootstrap 5 with vanilla JavaScript
- **Import Processing**: External x86 process execution for SDF file conversion

### Data Architecture
- **Hierarchical Structure**: WorkOrder → Products → Parts/Subassemblies → Parts
- **Status Tracking**: Pending → Cut → Sorted → Assembled → Shipped
- **Storage Management**: StorageRack → Bins with capacity tracking
- **Audit System**: Complete audit trail for all operations

## Core Architecture Patterns

### Service Layer Architecture
- **Dependency Injection**: All services registered as scoped in Program.cs
- **Service Categories**: Domain services (WorkOrderService), Import services (FastImportService), System services (BackupService)
- **Async/Await**: All service methods use proper async patterns
- **Constructor Injection**: Services follow consistent injection pattern with DbContext and ILogger

### SignalR Hub Patterns
- **StatusHub**: Station-specific group management (`cnc-station`, `sorting-station`, etc.)
- **ImportProgressHub**: Dedicated import progress tracking
- **Group Management**: Station groups and work order groups for targeted updates
- **Hub Integration**: Controllers inject `IHubContext<StatusHub>` for real-time notifications

### Controller Patterns
- **Thin Controllers**: Business logic delegated to services
- **Session Management**: Active work order stored in session across stations
- **Error Handling**: Consistent try-catch blocks with logging and TempData
- **API Separation**: API controllers in Controllers/Api/ namespace

### Frontend JavaScript Architecture
- **Universal Scanner System**: Event-driven scanner component with station detection
- **Component-Based Design**: Each station has dedicated JS files
- **SignalR Integration**: Real-time updates with connection management
- **Session Recovery**: Client-side session restoration with localStorage backup

## Critical Development Rules

### Database Migration Rules
- **Migration Strategy**: Create proper EF migrations for all database schema changes
- **Data Protection**: ShopBoss now contains production data that must be preserved
- **Development Environment**: Claude creates migrations using `dotnet ef migrations add`
- **Production Testing Protocol**: 
  1. User copies production database to development machine
  2. User runs migration manually on copy: `dotnet ef database update`
  3. User tests migrated database with local deployment
  4. User replaces production database with tested migrated version
  5. User deploys new build to production server
- **Claude's Role**: Create migrations only, never test against production data
- **Always verify Designer.cs and ModelSnapshot.cs files are created**
- **Migration failures in Windows deployment require Windows-specific fixes**

### Code Architecture Patterns
- **Universal Components**: Reuse existing services and controllers
- **Event-Driven Frontend**: SignalR and DOM events for loose coupling
- **Service Layer**: Keep controllers thin, logic in services
- **Audit Everything**: All status changes must be audited

### Key Entity Relationships
- **WorkOrder**: Root aggregate containing Products, Hardware, DetachedProducts, NestSheets
- **Part.ProductId**: Flexible field supporting both Product and DetachedProduct relationships
- **Status Flow**: Parts flow through PartStatus enum (Pending → Cut → Sorted → Assembled → Shipped)
- **Storage System**: StorageRack → Bins with capacity management and Part associations

### File Modification Guidelines
- **Never modify ShopBossDbContextModelSnapshot.cs manually**
- **Always preserve Microvellum ID fields when refactoring**
- **Maintain backward compatibility during status refactoring**
- **Test build after every significant change**
- **Follow existing service registration patterns in Program.cs**
- **Maintain SignalR group naming conventions** (station-name, WorkOrder_{id})

## Quality Standards

### Build Requirements
- **Zero compilation errors**
- **Zero new warnings**
- **All Entity Framework relationships intact**
- **SignalR hub registration maintained**

### Testing Requirements
- **Successful dotnet build**
- **Database schema validation**
- **User provides deployment testing results**
- **No regression in existing functionality**

### Documentation Requirements
- **Update Worklog.md for all changes**
- **Include specific testing instructions**
- **Document any breaking changes**
- **Maintain architectural decision records**

## Work Execution Guidelines

### Maximum Work Chunk Strategy
- **Target full vertical slices** (database → service → controller → view)
- **Handle multiple related components** with clear dependencies
- **Maintain architectural consistency** throughout changes
- **Validate at each layer** before proceeding

### Context Management
- **Use precise file references** in all communications
- **Maintain clear task boundaries** within phases
- **Separate architectural decisions** from implementation details
- **Document assumptions** and dependencies clearly

### Error Resolution Protocol
- **Distinguish development vs deployment errors**
- **Provide Windows-specific guidance** for deployment issues
- **Never modify architecture** based on deployment-only problems
- **Ask for clarification** when error source is unclear

## Development Workflow Best Practices

### New Feature Development
1. **Understand Entity Relationships**: Study the hierarchical WorkOrder → Products → Parts structure
2. **Follow Service Patterns**: Create services following existing dependency injection patterns
3. **Add SignalR Integration**: Use StatusHub for real-time updates with proper group management
4. **Implement Audit Trail**: Use AuditTrailService for all data modifications
5. **Test Across Stations**: Verify functionality works in multi-station environment

### Debugging Guidelines
- **Use Audit Logs**: AuditTrailService tracks all entity changes with timestamps
- **SignalR Connection Issues**: Check group membership and hub registration in Program.cs
- **Database Issues**: Verify EnsureCreated vs Migration strategy in Program.cs:88
- **Import Issues**: Check FastSdfReader.exe tool and external process execution
- **Session Issues**: Verify session configuration and localStorage backup patterns

### Common Patterns to Follow
- **Service Registration**: Add new services as scoped in Program.cs services section
- **Controller Actions**: Follow async patterns with try-catch and TempData messaging
- **SignalR Usage**: Use hub context injection and appropriate group targeting
- **Entity Updates**: Always use services for data modifications to ensure audit trail
- **Status Changes**: Use PartStatus enum and trigger SignalR notifications

## Database Migrations and Data Management

### Migrations Strategy
- **Always create proper EF migrations for database schema changes**
- **Production data must be preserved during all schema updates**
- **Follow the Production Testing Protocol outlined in Database Migration Rules**
- **Create migrations during development, user handles production testing and deployment**