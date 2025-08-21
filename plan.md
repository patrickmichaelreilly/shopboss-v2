# ShopBoss Server Management Module Implementation Plan

## Overview
Implementation plan for the Server Management Module that monitors supporting infrastructure services and manages shop data backups. This will be a new module within ShopBoss that consolidates and expands existing backup and health monitoring functionality.

## Architecture Restructuring
**Phase 0: Extract existing functionality from AdminController**
- Move backup management from AdminController to new ServerManagementController
- Move health monitoring from AdminController to new ServerManagementController  
- Clean up AdminController to focus on core admin functions
- Maintain existing functionality during transition

## Phase 1: Foundation & SQL Monitoring - 2-3 days
**Objective:** Create basic server management infrastructure with SQL Server monitoring

**Target Files:**
- Backend: Controllers/ServerManagementController.cs, Services/ServiceMonitoringService.cs
- Frontend: Views/ServerManagement/Index.cshtml, Views/ServerManagement/Dashboard.cshtml
- Database: Models/MonitoredService.cs, Models/ServiceHealthStatus.cs
- Config: Program.cs service registration

**Tasks:**
1. Create ServerManagementController with Index action and basic dashboard
2. Move BackupService integration from AdminController to ServerManagementController
3. Move SystemHealthMonitor integration from AdminController to ServerManagementController
4. Create ServiceMonitoringService for centralized health checking
5. Create MonitoredService and ServiceHealthStatus models
6. Add models to ShopBossDbContext
7. Create database migration for new models
8. Implement SQL Server connectivity monitoring (alive/dead status)
9. Create unified dashboard view combining health and backup status
10. Add Guest/Admin role distinction in views (hide config buttons for guests)
11. Integrate SignalR using existing StatusHub for real-time updates
12. Add navigation link to Server Management from main admin menu
13. Update AdminController to remove moved functionality
14. Update Admin views to remove backup and health dashboard links

**Validation:**
- [ ] Build succeeds without errors
- [ ] Existing backup functionality works in new location
- [ ] Existing health monitoring works in new location
- [ ] SQL Server status displays correctly
- [ ] Real-time updates work via SignalR
- [ ] Guest users cannot see configuration options
- [ ] Admin functionality preserved

**Dependencies:** None - refactoring existing code

## Phase 2: Windows Service Management - 2-3 days
**Objective:** Add Windows service monitoring and management capabilities

**Target Files:**
- Backend: Services/WindowsServiceManager.cs, Models/WindowsServiceConfig.cs
- Frontend: Views/ServerManagement/Services.cshtml
- Database: Migration for WindowsServiceConfig model

**Tasks:**
1. Create WindowsServiceManager service for service operations
2. Implement Windows service status checking (running/stopped/disabled)
3. Add service start/stop/restart functionality with elevated permissions handling
4. Create WindowsServiceConfig model for service configuration
5. Add default monitored services: SQL Server, ShopBoss, SpeedDial, Time & Attendance, Polling Service
6. Create service configuration interface for adding/removing services
7. Add service uptime tracking and last-checked timestamps
8. Integrate service monitoring into main dashboard
9. Add service management section to navigation
10. Implement error handling for permission-related failures

**Validation:**
- [ ] Can view status of all configured Windows services
- [ ] Can start/stop services (with appropriate permissions)
- [ ] Service status updates in real-time on dashboard
- [ ] Can add/remove services from monitoring list
- [ ] Graceful handling of permission errors

**Dependencies:** Phase 1 completed

## Phase 3: Enhanced Backup System - 3-4 days
**Objective:** Extend backup system for SQL Server databases and offsite storage

**Target Files:**
- Backend: Services/SqlServerBackupService.cs, Services/OffsiteStorageService.cs
- Backend: Models/BackupTarget.cs, Models/BackupSchedule.cs
- Frontend: Views/ServerManagement/Backups.cshtml
- Database: Migration for new backup models

**Tasks:**
1. Create SqlServerBackupService for SQL Server differential backups
2. Add backup targets for Microvellum and Production Coach databases
3. Create OffsiteStorageService for Egnyte/Carbonite integration
4. Implement BackupTarget and BackupSchedule models
5. Create configurable retention policies interface
6. Add backup scheduling with BackupScheduleService
7. Implement backup size tracking and history
8. Create backup management interface
9. Add manual backup trigger functionality
10. Integrate backup status into main dashboard
11. Add backup configuration to navigation

**Validation:**
- [ ] Can backup Microvellum and Production Coach databases
- [ ] Backups upload to offsite storage successfully
- [ ] Can configure backup schedules and retention
- [ ] Manual backups work on demand
- [ ] Backup history tracks all operations
- [ ] Backup status visible on main dashboard

**Dependencies:** Phase 2 completed

## Phase 4: Recovery Tools & System Monitoring - 2-3 days
**Objective:** Implement restore capabilities and comprehensive system monitoring

**Target Files:**
- Backend: Services/DatabaseRestoreService.cs, Services/SystemResourceMonitor.cs
- Frontend: Views/ServerManagement/Restore.cshtml, Views/ServerManagement/SystemResources.cshtml
- Database: Models/RestoreOperation.cs

**Tasks:**
1. Create DatabaseRestoreService for point-in-time restore operations
2. Implement pre-restore safety snapshot functionality
3. Create restore interface with database and time selection
4. Add SystemResourceMonitor for CPU/memory/disk monitoring
5. Implement log file viewer for monitored services
6. Create system resources monitoring dashboard
7. Add export functionality for monitoring data
8. Implement restore operation tracking and audit
9. Add restore section to navigation
10. Create comprehensive unified dashboard with all metrics

**Validation:**
- [ ] Can restore databases to specific points in time
- [ ] Pre-restore snapshots created automatically
- [ ] System resource monitoring displays current status
- [ ] Can view logs from monitored services
- [ ] Can export monitoring data
- [ ] All operations properly audited

**Dependencies:** Phase 3 completed

## Technical Architecture

### Database Schema
```sql
-- Service monitoring
MonitoredService: Id, ServiceName, ServiceType, ConnectionString, CheckInterval, IsEnabled, CreatedDate
ServiceHealthStatus: Id, ServiceId, Status, LastChecked, ResponseTime, ErrorMessage

-- Windows services
WindowsServiceConfig: Id, ServiceName, DisplayName, IsMonitored, CanManage, CreatedDate

-- Enhanced backup system
BackupTarget: Id, DatabaseName, ConnectionString, BackupType, IsEnabled
BackupSchedule: Id, TargetId, Schedule, RetentionDays, OffsiteEnabled
RestoreOperation: Id, DatabaseName, RestorePoint, Status, CreatedDate, CompletedDate
```

### Service Architecture
- **ServiceMonitoringService**: Centralized health checking coordinator
- **WindowsServiceManager**: Windows service operations
- **SqlServerBackupService**: SQL Server backup operations  
- **OffsiteStorageService**: Remote storage integration
- **DatabaseRestoreService**: Database restore operations
- **SystemResourceMonitor**: System performance monitoring

### SignalR Integration
- Use existing StatusHub with new group: `server-monitoring`
- Real-time updates for service status, backup progress, restore operations
- Client-side JavaScript for dashboard updates

### UI Structure
- `/ServerManagement/Index` - Main unified dashboard
- `/ServerManagement/Services` - Service configuration and management
- `/ServerManagement/Backups` - Backup configuration and history
- `/ServerManagement/Restore` - Database restore interface
- `/ServerManagement/SystemResources` - System performance monitoring

### Role-Based Access
- **Guest**: View-only access to dashboards and status
- **Admin**: Full access to configuration, management, and restore operations
- Implementation: Hide navigation buttons and action buttons for Guest users

## Success Criteria
- All shop-critical services monitored from single interface
- Existing backup and health functionality preserved and enhanced
- Complete SQL database backup and restore workflow
- Real-time monitoring with SignalR updates
- Clean separation from AdminController
- Zero additional deployment complexity
- Maintains existing ShopBoss architectural patterns