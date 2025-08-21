# ShopBoss Server Management Module PRD

## What It Does
New module within ShopBoss that monitors supporting infrastructure services and manages shop data backups, reinforcing ShopBoss as the operating system for the shop.

## Problem
Shop operations depend on multiple supporting services (SQL Server, time clock systems, SpeedDial, etc.) but monitoring them requires separate tools. Need unified visibility into all shop infrastructure from within the main shop management system.

## Architecture Decision
**ShopBoss Module** rather than separate application because:
- ShopBoss is evolving into the shop's digital infrastructure hub
- Supporting services directly impact shop operations and production data
- Unified authentication and deployment pipeline
- Contextual integration - correlate infrastructure health with shop performance
- Familiar development environment and iterative deployment process

## Core Features

### Infrastructure Monitoring
- SQL Server health and connection status
- Time clock data aggregator service monitoring
- SpeedDial DNS/proxy service status
- Windows service management (start/stop/restart)
- System resources (CPU, memory, disk space)
- HTTP endpoint health checks
- Service logs viewer integrated into ShopBoss interface

### Shop Data Protection
- SQL Server differential backups (shop databases)
- ShopBoss configuration and file backups
- Supporting service configuration backups
- Automated backup scheduling to offsite storage
- Backup status tracking and history

### Recovery Tools
- Database restore with point-in-time selection
- Configuration restore for supporting services
- Manual backup triggers for immediate protection
- Pre-restore safety snapshots

## Development Approach
**Iterative module development:**
- Start with blank dashboard within existing ShopBoss framework
- Add one monitoring service at a time using familiar codebase
- Leverage existing authentication, UI components, and deployment processes
- Build on proven ShopBoss architecture and patterns

## Technical Integration
- New controller and views within existing ShopBoss MVC structure
- Shared database and authentication system
- Role-based access (shop managers vs IT functions)
- Same deployment and testing pipeline as main ShopBoss features

## Development Phases
**Phase 1:** Empty dashboard with SQL Server monitoring
**Phase 2:** Add supporting service monitoring one by one
**Phase 3:** Backup management and scheduling
**Phase 4:** Restore capabilities and disaster recovery

## Success Criteria
- All shop-critical services monitored from single ShopBoss interface
- Infrastructure health visible alongside production data
- Complete shop data protection with easy restore workflow
- Zero additional deployment complexity beyond existing ShopBoss processes