# ShopBoss Emergency Recovery Procedures

## 🚨 Emergency Response Guide

This document provides step-by-step procedures for recovering from critical ShopBoss failures during beta operations.

---

## **Emergency Contact Information**

- **Primary Support**: [Your Contact Information]
- **Secondary Support**: [Backup Contact]
- **System Administrator**: [Admin Contact]

---

## **Quick Reference - Emergency Response**

### **Immediate Actions (First 5 Minutes)**

1. **STOP** - Don't panic, follow procedures
2. **ASSESS** - Identify the severity and scope
3. **ISOLATE** - Prevent further damage
4. **COMMUNICATE** - Notify relevant parties
5. **DOCUMENT** - Record what happened

### **Severity Levels**

- **🔴 CRITICAL**: System completely down, production stopped
- **🟡 WARNING**: System degraded but operational
- **🟢 NOTICE**: Minor issues, no production impact

---

## **Recovery Scenarios**

### **Scenario 1: Database Corruption**

**Symptoms:**
- Application won't start
- Database errors in logs
- Data appears corrupted or missing

**Recovery Steps:**

1. **Immediate Response**
   ```powershell
   # Stop ShopBoss service
   .\scripts\clean-sqlite-locks.ps1 -Force
   ```

2. **Assess Database**
   ```powershell
   # Check if database file exists
   ls src\ShopBoss.Web\shopboss.db
   
   # Try integrity check (if sqlite3 available)
   sqlite3 src\ShopBoss.Web\shopboss.db "PRAGMA integrity_check;"
   ```

3. **Restore from Backup**
   ```powershell
   # List available backups
   .\scripts\restore-shopboss-beta.ps1
   
   # Restore from most recent backup
   .\scripts\restore-shopboss-beta.ps1 -BackupFilePath "C:\ShopBoss-Backups\[backup-file]"
   ```

4. **Verify Recovery**
   ```powershell
   # Start ShopBoss
   .\scripts\test-shortcuts.ps1 run
   
   # Check system status
   .\scripts\test-shortcuts.ps1 status
   ```

**Expected Recovery Time**: 5-15 minutes

---

### **Scenario 2: Application Won't Start**

**Symptoms:**
- ShopBoss service fails to start
- Port conflicts or binding errors
- Configuration errors

**Recovery Steps:**

1. **Check Process Status**
   ```powershell
   # Kill any hanging processes
   Get-Process -Name "ShopBoss.Web" -ErrorAction SilentlyContinue | Stop-Process -Force
   
   # Clean SQLite locks
   .\scripts\clean-sqlite-locks.ps1
   ```

2. **Check Configuration**
   ```powershell
   # Verify database connection
   Test-Path src\ShopBoss.Web\shopboss.db
   
   # Check appsettings.json
   Get-Content src\ShopBoss.Web\appsettings.json
   ```

3. **Try Build and Run**
   ```powershell
   # Clean build
   dotnet clean src\ShopBoss.Web\ShopBoss.Web.csproj
   dotnet build src\ShopBoss.Web\ShopBoss.Web.csproj
   
   # Try to run
   dotnet run --project src\ShopBoss.Web\ShopBoss.Web.csproj
   ```

4. **Check Logs**
   ```powershell
   # Check application logs
   Get-Content src\ShopBoss.Web\logs\*.txt -Tail 50
   ```

**Expected Recovery Time**: 10-30 minutes

---

### **Scenario 3: Data Loss or Corruption**

**Symptoms:**
- Work orders missing or corrupted
- Parts showing wrong status
- Storage rack data corrupted

**Recovery Steps:**

1. **Immediate Backup**
   ```powershell
   # Create emergency backup of current state
   .\scripts\backup-shopboss-beta.ps1 -BackupType "emergency"
   ```

2. **Assess Data Loss**
   ```powershell
   # Check recent backups
   ls "C:\ShopBoss-Backups" | Sort-Object LastWriteTime -Descending
   
   # Check incremental backups
   ls "C:\ShopBoss-Backups\incremental" | Sort-Object LastWriteTime -Descending
   ```

3. **Restore from Known Good State**
   ```powershell
   # Restore from most recent clean backup
   .\scripts\restore-shopboss-beta.ps1 -BackupFilePath "[selected-backup]"
   ```

4. **Verify Data Integrity**
   ```powershell
   # Start application and check data
   .\scripts\test-shortcuts.ps1 run
   
   # Navigate to Admin > Health Dashboard
   # Verify work orders and storage racks
   ```

**Expected Recovery Time**: 15-45 minutes

---

### **Scenario 4: System Performance Issues**

**Symptoms:**
- Slow response times
- High CPU or memory usage
- Database locks

**Recovery Steps:**

1. **Check System Resources**
   ```powershell
   # Check memory usage
   Get-Process -Name "ShopBoss.Web" | Select-Object CPU, WorkingSet, PagedMemorySize
   
   # Check disk space
   Get-PSDrive C
   ```

2. **Clean Database Locks**
   ```powershell
   # Clean SQLite locks
   .\scripts\clean-sqlite-locks.ps1
   ```

3. **Restart Service**
   ```powershell
   # Stop and restart ShopBoss
   Get-Process -Name "ShopBoss.Web" | Stop-Process -Force
   Start-Sleep -Seconds 5
   .\scripts\test-shortcuts.ps1 run
   ```

4. **Monitor Performance**
   ```powershell
   # Check system status
   .\scripts\test-shortcuts.ps1 status
   
   # Monitor for 5 minutes
   ```

**Expected Recovery Time**: 5-10 minutes

---

### **Scenario 5: Complete System Failure**

**Symptoms:**
- Server/computer won't boot
- Hardware failure
- Network issues

**Recovery Steps:**

1. **Assess Hardware**
   - Check power, network connections
   - Verify hardware status
   - Document any error messages

2. **Backup Recovery**
   ```powershell
   # On recovery system, restore from external backup
   .\scripts\restore-shopboss-beta.ps1 -BackupDirectory "C:\ShopBoss-Backups"
   ```

3. **Rebuild System**
   - Install .NET 8.0 runtime
   - Deploy ShopBoss application
   - Restore database from backup

4. **Test and Verify**
   ```powershell
   # Full system test
   .\scripts\test-shortcuts.ps1 build
   .\scripts\test-shortcuts.ps1 run
   ```

**Expected Recovery Time**: 1-4 hours

---

## **Recovery Validation Checklist**

After any recovery procedure, verify:

- [ ] **Database Integrity**: All work orders and parts are present
- [ ] **Application Startup**: ShopBoss starts without errors
- [ ] **Station Functionality**: All stations (Admin, CNC, Sorting, Assembly, Shipping) work
- [ ] **Real-time Updates**: SignalR connections working
- [ ] **Backup System**: Automatic backups are running
- [ ] **Scanner Integration**: Barcode scanning works at all stations
- [ ] **Data Audit**: Recent operations are in audit log

---

## **Prevention Measures**

### **Daily Checks**
- [ ] Verify automatic backups are running
- [ ] Check disk space availability
- [ ] Review error logs for warnings
- [ ] Test scanner functionality

### **Weekly Checks**
- [ ] Test backup/restore process
- [ ] Review system performance
- [ ] Update documentation
- [ ] Check for software updates

### **Monthly Checks**
- [ ] Full system health review
- [ ] Update emergency procedures
- [ ] Review recovery time objectives
- [ ] Test disaster recovery plan

---

## **Backup Verification**

### **Quick Backup Test**
```powershell
# Test backup creation
.\scripts\backup-shopboss-beta.ps1 -BackupType "test"

# Verify backup integrity
.\scripts\test-backup-restore.ps1 -Verbose
```

### **Backup Locations**
- **Primary**: `C:\ShopBoss-Backups\`
- **Incremental**: `C:\ShopBoss-Backups\incremental\`
- **Checkpoints**: `.\checkpoints\`

---

## **Communication Templates**

### **Critical Incident Notification**
```
SUBJECT: ShopBoss Critical Incident - [Brief Description]

Incident Time: [Timestamp]
Severity: CRITICAL
Impact: [Description of impact]
Status: [In Progress/Resolved]

Actions Taken:
- [List steps taken]

Estimated Recovery Time: [Time estimate]
Next Update: [When next update will be provided]

Contact: [Your contact info]
```

### **Recovery Complete Notification**
```
SUBJECT: ShopBoss Recovery Complete

Incident: [Brief description]
Recovery Time: [Start time] - [End time]
Total Downtime: [Duration]

Root Cause: [What caused the issue]
Resolution: [What was done to fix it]

System Status: OPERATIONAL
Next Steps: [Any follow-up actions]

Contact: [Your contact info]
```

---

## **Recovery Time Objectives (RTO)**

| Scenario | Target RTO | Maximum RTO |
|----------|------------|-------------|
| Database Corruption | 15 minutes | 30 minutes |
| Application Failure | 10 minutes | 20 minutes |
| Performance Issues | 5 minutes | 15 minutes |
| Data Loss | 30 minutes | 60 minutes |
| Complete System Failure | 2 hours | 4 hours |

---

## **Recovery Point Objectives (RPO)**

- **Automatic Backups**: Every 60 minutes (maximum 1 hour data loss)
- **Manual Backups**: On-demand (before major changes)
- **Incremental Backups**: Per patch deployment (no data loss)

---

## **Emergency Toolkit**

Keep these tools ready:

1. **Scripts Directory**
   - `clean-sqlite-locks.ps1`
   - `backup-shopboss-beta.ps1`
   - `restore-shopboss-beta.ps1`
   - `test-shortcuts.ps1`

2. **Backup Locations**
   - External backup drive
   - Network backup location
   - Cloud backup (if configured)

3. **Documentation**
   - This recovery guide
   - System configuration details
   - Contact information

4. **Software**
   - .NET 8.0 Runtime installer
   - SQLite command-line tools
   - Text editor for configuration

---

## **Post-Incident Review**

After any emergency recovery:

1. **Document the incident** in detail
2. **Review response times** against RTO targets
3. **Identify improvements** to procedures
4. **Update documentation** as needed
5. **Conduct lessons learned** session
6. **Test improvements** in development environment

---

## **Testing Emergency Procedures**

### **Monthly Emergency Drill**
```powershell
# Create test scenario
.\scripts\test-shortcuts.ps1 checkpoint

# Simulate failure
# [Follow recovery procedures]

# Verify recovery
.\scripts\test-shortcuts.ps1 status

# Reset to clean state
.\scripts\test-shortcuts.ps1 reset
```

### **Quarterly Full Recovery Test**
- Complete system restore from backup
- Full application rebuild
- End-to-end workflow testing
- Performance verification

---

**Remember**: Stay calm, follow procedures, and document everything. The goal is to restore service quickly and safely while preserving data integrity.