# ShopBoss Server Management Module Implementation Plan

## Overview
Implementation plan for the Server Management Module that provides **monitoring-only** visibility into supporting infrastructure services and manages shop data backups. This module consolidates existing backup and health monitoring functionality into a clean, simplified interface focused on status visibility rather than configuration complexity.

## Core Philosophy
**Simplicity over configurability** - Services are hardcoded and auto-initialized. The interface focuses purely on monitoring status with minimal user interaction. We reduce complexity at every step and avoid feature bloat.

## Monitoring-Only Design Principles
- **No service management capabilities** - status visibility only, no start/stop/restart
- **No user configuration** - services are hardcoded in code, no configuration UI
- **No real-time updates** - data loads on page visit only, no SignalR complexity
- **No manual refresh buttons** - auto-load on page visit, no user interaction needed
- **Expandable details only** - technical information hidden until specifically requested
- **Read-only interface** - focus on information display, not control or modification

## ✅ Phase 0: COMPLETE - Architecture Restructuring 
**Objective:** Extract existing functionality from AdminController
- ✅ Moved backup management from AdminController to ServerManagementController
- ✅ Moved health monitoring from AdminController to ServerManagementController  
- ✅ Cleaned up AdminController to focus on core admin functions
- ✅ Maintained existing functionality during transition

## ✅ Phase 1: COMPLETE - Foundation & ShopBoss Application Monitoring
**Objective:** Create basic server management infrastructure with ShopBoss internal monitoring

**✅ Actual Implementation:**
- **Backend:** ServerManagementController.cs, SystemMonitoringService.cs (renamed)
- **Frontend:** Views/ServerManagement/Index.cshtml (unified tabbed interface)
- **Database:** MonitoredService.cs, ServiceHealthStatus.cs models with EF migrations
- **Architecture:** Eliminated SystemHealthMonitor, consolidated into SystemMonitoringService

**✅ Completed Tasks:**
1. ✅ Created ServerManagementController with unified tabbed dashboard
2. ✅ Moved BackupService integration from AdminController
3. ✅ **Eliminated** SystemHealthMonitor entirely (redundant)
4. ✅ Created SystemMonitoringService for centralized health checking
5. ✅ Created MonitoredService and ServiceHealthStatus models
6. ✅ Added models to ShopBossDbContext with proper migrations
7. ✅ Implemented **ShopBoss SQLite database** connectivity monitoring with response time
8. ✅ Created unified System Status + Backups tabbed interface
9. ✅ **Simplified approach** - removed manual buttons, auto-refresh, configuration UI
10. ✅ Auto-initialization of services on page load
11. ✅ Clean, dense UI with tabs acting as section headers
12. ✅ Added to admin navigation menu

**✅ Key Departures from Original Plan:**
- **No user configuration** - services are hardcoded and auto-managed
- **No real-time updates** - data loads on page visit only
- **No SignalR complexity** - simplified to page load updates
- **Two tabs only** - System Status and Backups (removed Services Config)
- **Monitoring-only philosophy** - no service management capabilities

**Dependencies:** None - refactoring existing code

## Phase 2: Service Monitoring Expansion - 1-2 days
**Objective:** Add monitoring for critical shop services with expandable detail views

**Target Files:**
- Backend: SystemMonitoringService.cs (enhance existing)
- Frontend: Views/ServerManagement/Index.cshtml (enhance System Status table)
- Database: Update InitializeDefaultServicesAsync with additional services

**Target Services to Add:**
- ShopBoss Application (✅ already implemented as SQLite monitoring - needs enhancement)
- **External SQL Servers** (Microvellum data sources)
- SpeedDial Service
- Time & Attendance Service  
- Polling Service

**Tasks:**
1. **Rename existing SQLite monitoring** from "SQL Server Database" to "ShopBoss Application" 
2. **Enhance ShopBoss Application monitoring** with app-specific metrics (version, uptime, database stats)
3. **Add external SQL Server monitoring** for Microvellum data sources using Microsoft.Data.SqlClient
4. **Add HTTP endpoint monitoring** for SpeedDial and Time & Attendance services
5. **Add Polling Service monitoring** (Windows service or HTTP endpoint)
6. **Implement expandable row pattern** in System Status table (following Project view pattern)
7. **Add service-specific detail views**:
   - Connection strings/endpoints  
   - Response times and error details
   - Service configuration paths
   - Technical specifications and troubleshooting info
8. **Add chevron expand/collapse** functionality with JavaScript
9. **Style expandable content** to match Project view density
10. **Test monitoring** for all services with build validation

**Monitoring-Only Approach:**
- **No service management** (start/stop/restart functionality)
- **No user configuration** - services are hardcoded in code
- **Status visibility only** - green/red health indicators
- **Expandable details** - technical info hidden until expanded

**Validation:**
- [ ] ShopBoss Application monitoring enhanced with app-specific details
- [ ] External SQL Servers added with proper Microsoft.Data.SqlClient connectivity 
- [ ] HTTP services (SpeedDial, Time & Attendance) monitored with endpoint checks
- [ ] Polling Service monitored (service type to be determined)
- [ ] All 5 services appear in System Status table
- [ ] Each row can expand to show technical details
- [ ] Health checks work for all service types
- [ ] Build succeeds without errors
- [ ] UI matches Project view expandable pattern

**Dependencies:** Phase 1 completed ✅

## Phase 3: Backup System Refinement - 1 day
**Objective:** Simple reliability improvements to existing backup functionality

**Target Files:**
- Backend: BackupService.cs (minor enhancements only)

**Monitoring-Only Focus:**
- **No configuration UI changes** - existing Backups tab is sufficient
- **Reliability improvements only** - error handling and validation
- **No feature additions** - maintain simplicity

**Tasks:**
1. Add backup validation checks (file integrity verification)
2. Improve error reporting in backup operations
3. Add backup retention enforcement (automatic cleanup)
4. Optimize backup scheduling reliability

**Validation:**
- [ ] Backup operations are more reliable
- [ ] Error messages are clearer and actionable
- [ ] Retention policies work automatically
- [ ] Build succeeds without errors

**Dependencies:** Phase 2 completed

## Phase 4: Future Expansion (Deferred)
**Objective:** Reserved for future system monitoring enhancements

**Notes:**
- System resource monitoring deferred to maintain simplicity
- Focus remains on service connectivity monitoring only
- Additional metrics can be added later if specifically requested

## Technical Architecture

### Database Schema (Current Implementation)
```sql
-- Service monitoring (✅ implemented)
MonitoredService: Id, ServiceName, ServiceType, ConnectionString, CheckInterval, IsEnabled, CreatedDate, LastModifiedDate, Description
ServiceHealthStatus: Id, ServiceId, Status, LastChecked, ResponseTimeMs, ErrorMessage, Details, IsReachable

-- Existing backup system (✅ using existing models)
BackupConfiguration: Id, BackupIntervalMinutes, MaxBackupRetention, BackupDirectoryPath, EnableCompression, EnableAutomaticBackups
BackupStatus: Id, CreatedDate, BackupType, IsSuccessful, BackupSize, OriginalSize, Duration, ErrorMessage
```

### Service Architecture (Current Implementation)
- **SystemMonitoringService**: Centralized health checking and service management
- **BackupService**: Database backup operations (existing, enhanced)
- **Future additions**: Service-specific health checkers as methods within SystemMonitoringService

### UI Architecture (Current Implementation) 
- **Single page with tabs**: `/ServerManagement/Index`
  - **System Status tab**: Unified monitoring table with expandable rows
  - **Backups tab**: Configuration form + Recent backups table
- **No separate views** - everything consolidated into tabbed interface
- **No SignalR complexity** - page load updates only
- **Monitoring-only interface** - no service management controls

### Expandable Row Pattern (Phase 2 Implementation)
Following Project view pattern for System Status table expansion:
- **Chevron Toggle**: First column contains expand/collapse chevron icon
- **Expandable Content**: Technical details hidden until row is expanded
- **Service-Specific Details**: Connection strings, configuration paths, status details
- **Consistent Styling**: Match Project view density and visual patterns
- **No Configuration**: Details are read-only technical information only

### Role-Based Access
- **Guest**: View-only access to dashboards and status
- **Admin**: Full access to configuration, management, and restore operations
- Implementation: Hide navigation buttons and action buttons for Guest users

## Success Criteria
- All shop-critical services monitored from single interface
- Existing backup functionality preserved in simplified interface
- SQL database backup and restore workflow through Backups tab
- Monitoring-only approach with status visibility focused design
- Clean separation from AdminController architecture
- Zero additional deployment complexity
- Maintains existing ShopBoss architectural patterns and simplicity philosophy