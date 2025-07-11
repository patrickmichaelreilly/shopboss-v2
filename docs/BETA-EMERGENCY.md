# ShopBoss Beta Emergency Procedures

## 🚨 Beta-Specific Emergency Response

This document provides emergency procedures specifically for ShopBoss Beta operations. These procedures are designed for the production environment with external backup systems and real-world operational constraints.

---

## **Emergency Contacts**

### **Primary Support**
- **Development Team**: [Your Contact]
- **Phone**: [Emergency Phone]
- **Email**: [Emergency Email]

### **On-Site Support**
- **System Administrator**: [Admin Name]
- **Phone**: [Admin Phone]
- **Shop Floor Manager**: [Manager Name]
- **Phone**: [Manager Phone]

### **Escalation**
- **Management**: [Management Contact]
- **IT Director**: [IT Director Contact]

---

## **Beta Emergency Response Levels**

### **🔴 LEVEL 1: PRODUCTION STOPPAGE**
**Production cannot continue, immediate response required**

**Immediate Actions (0-5 minutes):**
1. **STOP** all shop floor operations
2. **NOTIFY** management immediately
3. **ISOLATE** affected systems
4. **DOCUMENT** the incident
5. **BEGIN** recovery procedures

**Examples:**
- Complete system failure
- Database corruption preventing all operations
- Multiple station failures
- Data loss affecting active work orders

### **🟡 LEVEL 2: DEGRADED OPERATIONS**
**Some operations affected, workarounds possible**

**Immediate Actions (0-10 minutes):**
1. **ASSESS** scope of impact
2. **IMPLEMENT** workarounds if possible
3. **NOTIFY** affected operators
4. **DOCUMENT** issues and workarounds
5. **SCHEDULE** repair window

**Examples:**
- Single station failure
- Performance degradation
- Scanner issues at one station
- Non-critical data inconsistencies

### **🟢 LEVEL 3: MINOR ISSUES**
**Operations continue with minimal impact**

**Actions (0-30 minutes):**
1. **LOG** the issue
2. **MONITOR** for escalation
3. **SCHEDULE** maintenance
4. **DOCUMENT** for future reference

**Examples:**
- Minor UI glitches
- Slow response times
- Non-critical error messages
- Cosmetic issues

---

## **Beta-Specific Recovery Procedures**

### **Complete System Recovery**

#### **When to Use:**
- Application won't start
- Database corruption
- Major system failure

#### **Recovery Steps:**

1. **Immediate Assessment**
   ```powershell
   # Check system status
   Get-Process -Name "ShopBoss.Web" -ErrorAction SilentlyContinue
   
   # Check service status (if installed as service)
   Get-Service -Name "ShopBoss" -ErrorAction SilentlyContinue
   
   # Check database file
   Test-Path "C:\ShopBoss\shopboss.db"
   ```

2. **Stop All Processes**
   ```powershell
   # Stop ShopBoss processes
   Get-Process -Name "ShopBoss.Web" | Stop-Process -Force
   
   # Clean SQLite locks
   C:\ShopBoss\scripts\clean-sqlite-locks.ps1 -Force
   ```

3. **Restore from External Backup**
   ```powershell
   # List available backups
   C:\ShopBoss\scripts\restore-shopboss-beta.ps1
   
   # Restore from most recent backup
   C:\ShopBoss\scripts\restore-shopboss-beta.ps1 -BackupFilePath "C:\ShopBoss-Backups\[latest-backup]"
   ```

4. **Restart System**
   ```powershell
   # Start ShopBoss service
   Start-Service -Name "ShopBoss"
   
   # Or start manually
   cd C:\ShopBoss
   .\ShopBoss.Web.exe
   ```

5. **Verify Recovery**
   - Test each station (Admin, CNC, Sorting, Assembly, Shipping)
   - Verify active work order data
   - Check recent operations in audit log
   - Test scanner functionality

**Expected Recovery Time: 10-20 minutes**

---

### **Single Station Recovery**

#### **When to Use:**
- One station not responding
- Station-specific errors
- Scanner issues at specific station

#### **Recovery Steps:**

1. **Identify Affected Station**
   - Which station is having issues?
   - Are other stations working normally?
   - Is the issue consistent across users?

2. **Browser-Level Recovery**
   ```powershell
   # Clear browser cache
   # Close and reopen browser
   # Try different browser
   # Check network connectivity
   ```

3. **Application-Level Recovery**
   ```powershell
   # Check application logs
   Get-Content "C:\ShopBoss\logs\*.log" -Tail 50
   
   # Restart application if needed
   Restart-Service -Name "ShopBoss"
   ```

4. **Test Recovery**
   - Navigate to affected station
   - Test basic functionality
   - Test scanner if applicable
   - Verify real-time updates

**Expected Recovery Time: 5-15 minutes**

---

### **Data Recovery Procedures**

#### **When to Use:**
- Missing work order data
- Parts showing incorrect status
- Lost recent operations

#### **Recovery Steps:**

1. **Assess Data Loss**
   ```powershell
   # Check recent backups
   ls "C:\ShopBoss-Backups" | Sort-Object LastWriteTime -Descending | Select-Object -First 10
   
   # Check incremental backups
   ls "C:\ShopBoss-Backups\incremental" | Sort-Object LastWriteTime -Descending | Select-Object -First 5
   ```

2. **Determine Recovery Point**
   - When was the last known good state?
   - What data can be lost without major impact?
   - Are there manual records to cross-reference?

3. **Create Emergency Backup**
   ```powershell
   # Backup current state before recovery
   C:\ShopBoss\scripts\backup-shopboss-beta.ps1 -BackupType "emergency-before-recovery"
   ```

4. **Restore from Backup**
   ```powershell
   # Restore from selected backup
   C:\ShopBoss\scripts\restore-shopboss-beta.ps1 -BackupFilePath "[selected-backup]"
   ```

5. **Verify Data Integrity**
   - Check work order completeness
   - Verify part statuses match physical state
   - Cross-reference with manual records
   - Update any discrepancies

**Expected Recovery Time: 20-45 minutes**

---

### **Performance Recovery**

#### **When to Use:**
- System running slowly
- High resource usage
- Database performance issues

#### **Recovery Steps:**

1. **Check System Resources**
   ```powershell
   # Check CPU and memory usage
   Get-Process -Name "ShopBoss.Web" | Select-Object CPU, WorkingSet, PagedMemorySize
   
   # Check disk space
   Get-PSDrive C | Select-Object Used, Free
   
   # Check database size
   Get-Item "C:\ShopBoss\shopboss.db" | Select-Object Length, LastWriteTime
   ```

2. **Clear Database Locks**
   ```powershell
   # Clean SQLite locks
   C:\ShopBoss\scripts\clean-sqlite-locks.ps1
   ```

3. **Restart Application**
   ```powershell
   # Restart ShopBoss service
   Restart-Service -Name "ShopBoss"
   ```

4. **Monitor Performance**
   - Check response times at each station
   - Monitor resource usage for 10 minutes
   - Test scanner responsiveness
   - Check database query performance

**Expected Recovery Time: 5-10 minutes**

---

## **Beta Communication Procedures**

### **Internal Communication**

#### **Level 1 Emergency (Production Stoppage)**
```
URGENT: ShopBoss Production Stoppage

Time: [Current Time]
Duration: [Estimated duration]
Impact: Production stopped - all stations affected

Actions Being Taken:
- [Current recovery steps]

Estimated Recovery: [Time estimate]
Next Update: [In 15 minutes]

Contact: [Your name and phone]
```

#### **Level 2 Emergency (Degraded Operations)**
```
NOTICE: ShopBoss Degraded Operations

Time: [Current Time]
Impact: [Specific impact description]
Workaround: [Available workarounds]

Actions Being Taken:
- [Current recovery steps]

Estimated Resolution: [Time estimate]
Next Update: [In 30 minutes]

Contact: [Your name and phone]
```

### **Recovery Complete Notification**
```
RESOLVED: ShopBoss System Restored

Incident: [Brief description]
Duration: [Start time] - [End time]
Total Impact: [Duration of impact]

Resolution: [What was done]
Root Cause: [If known]

Status: OPERATIONAL
Monitoring: [Ongoing monitoring plan]

Contact: [Your name and phone]
```

---

## **Beta-Specific Manual Workarounds**

### **CNC Station Workaround**
If CNC station is down:
1. **Manual Tracking**: Use paper forms to track cut parts
2. **Batch Update**: Update system when station is restored
3. **Status Override**: Use Admin station to manually mark parts as cut

### **Sorting Station Workaround**
If sorting station is down:
1. **Manual Bin Assignment**: Use printed rack diagrams
2. **Physical Tags**: Use paper tags to mark part locations
3. **Batch Update**: Update system when station is restored

### **Assembly Station Workaround**
If assembly station is down:
1. **Manual Checklists**: Use printed component lists
2. **Physical Verification**: Manually verify component availability
3. **Batch Completion**: Update system when station is restored

### **Shipping Station Workaround**
If shipping station is down:
1. **Manual Shipping Log**: Use paper shipping forms
2. **Physical Verification**: Manually verify completeness
3. **Batch Update**: Update system when station is restored

---

## **Beta Data Backup Verification**

### **Daily Backup Check**
```powershell
# Check latest backup
Get-ChildItem "C:\ShopBoss-Backups" -Filter "shopboss_beta_backup_*.db.gz" | 
Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Verify backup integrity
C:\ShopBoss\scripts\test-backup-restore.ps1 -Verbose
```

### **Weekly Backup Test**
```powershell
# Test restore process
C:\ShopBoss\scripts\restore-shopboss-beta.ps1 -BackupFilePath "[test-backup]"

# Verify data integrity
# Check work orders, parts, and audit logs
```

---

## **Beta Monitoring Checklist**

### **Hourly Checks** (During Critical Operations)
- [ ] System responding normally
- [ ] All stations accessible
- [ ] Scanner functionality working
- [ ] No error messages visible
- [ ] Real-time updates functioning

### **Daily Checks**
- [ ] Backup completed successfully
- [ ] Database size within normal range
- [ ] No SQLite lock files present
- [ ] System resource usage normal
- [ ] Error logs reviewed

### **Weekly Checks**
- [ ] Backup restore test completed
- [ ] Performance benchmarks met
- [ ] Emergency procedures reviewed
- [ ] Contact information updated
- [ ] Documentation current

---

## **Beta Escalation Matrix**

| Issue Type | Level | Response Time | Escalation Time | Contact |
|------------|-------|---------------|-----------------|---------|
| Production Stoppage | 1 | Immediate | 15 minutes | All contacts |
| Single Station Down | 2 | 5 minutes | 30 minutes | Dev team + Admin |
| Performance Issues | 2 | 10 minutes | 1 hour | Dev team |
| Minor Issues | 3 | 30 minutes | 4 hours | Dev team |
| Scheduled Maintenance | 3 | Planned | N/A | Admin |

---

## **Beta Recovery Time Objectives**

| Scenario | Target | Maximum | Workaround Available |
|----------|--------|---------|---------------------|
| Complete System Failure | 15 min | 30 min | Manual processes |
| Single Station Failure | 5 min | 15 min | Other stations |
| Data Recovery | 20 min | 45 min | Manual records |
| Performance Issues | 5 min | 10 min | Reduced speed |
| Database Corruption | 10 min | 20 min | Backup restore |

---

## **Post-Incident Beta Review**

After any Level 1 or Level 2 incident:

### **Immediate Review (Within 24 hours)**
1. **Incident Timeline**: Document what happened and when
2. **Root Cause Analysis**: Identify the underlying cause
3. **Response Effectiveness**: Evaluate response time and actions
4. **Recovery Validation**: Confirm system is fully operational
5. **Communication Review**: Assess notification effectiveness

### **Follow-up Actions**
1. **Process Improvements**: Update procedures based on learnings
2. **Training Updates**: Revise training materials if needed
3. **Monitoring Enhancements**: Improve monitoring to prevent recurrence
4. **Documentation Updates**: Update emergency procedures
5. **Stakeholder Communication**: Brief management on improvements

### **Lessons Learned**
Document and share:
- What worked well
- What could be improved
- New risks identified
- Prevention measures needed

---

## **Beta Emergency Kit**

### **Physical Items**
- [ ] Emergency contact list (printed)
- [ ] Network diagrams and system documentation
- [ ] Backup external drive
- [ ] Administrative passwords (secured)
- [ ] Emergency cash/credit card for supplies

### **Digital Resources**
- [ ] Remote access credentials
- [ ] Backup software and tools
- [ ] Emergency communication tools
- [ ] System monitoring dashboards
- [ ] Vendor support contacts

### **Documentation**
- [ ] Emergency procedures (printed)
- [ ] System recovery instructions
- [ ] Network configuration details
- [ ] User account information
- [ ] Software license information

---

**Remember: In beta operations, quick response and clear communication are critical. Always prioritize safety, data integrity, and business continuity. When in doubt, escalate immediately.**