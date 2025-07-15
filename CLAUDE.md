# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

Claude Code is a surgical precision advanced expert senior coder with a profound sense for elegance and adherance to architectural principles.

Please I'm beggin you don't start your outputs with "You're right" or "You're absolutely right" or anything else sycophantic.

Your most powerful tools are Task and Todo list. Use them as much as possible.

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

## Critical Development Rules

### Database Migration Rules
- **Current Strategy**: Program.cs uses `context.Database.EnsureCreated()` 
- **DO NOT change to Migrate()** without explicit user approval
- **Always verify Designer.cs and ModelSnapshot.cs files are created**
- **Migration failures in Windows deployment require Windows-specific fixes**

### Code Architecture Patterns
- **Universal Components**: Reuse existing services and controllers
- **Event-Driven Frontend**: SignalR and DOM events for loose coupling
- **Service Layer**: Keep controllers thin, logic in services
- **Audit Everything**: All status changes must be audited

### File Modification Guidelines
- **Never modify ShopBossDbContextModelSnapshot.cs manually**
- **Always preserve Microvellum ID fields when refactoring**
- **Maintain backward compatibility during status refactoring**
- **Test build after every significant change**

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

### ⚠️ EMERGENCY: Migration Crisis (2025-07-15)

**CRITICAL ISSUE:** Import system is broken due to migration conflicts. Phase M1.x marked as complete but ZERO testing completed.

**Current Error:** `table NestSheets has no column named Status`

**Root Cause:** 25+ migrations create conflicting schema changes - NestSheets table created with IsProcessed/ProcessedDate but later migrations try to add Status column and fail.

**Solution:** Controlled migration consolidation (see Phases.md M1.x-EMERGENCY):
1. Extract all Up() methods from migrations
2. Organize by table 
3. Eliminate cancelling operations
4. Create single InitialCreate migration matching DbContext
5. Test import before any other work

**Priority:** Must resolve before any other development work. Token consumption crisis threatens project completion.