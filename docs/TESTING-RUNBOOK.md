# ShopBoss Testing Runbook

## 📋 Complete Testing Guide for Development and Beta Operations

This runbook provides comprehensive testing procedures for ShopBoss v2, including common issues, solutions, and validation steps.

---

## **Quick Reference**

### **Essential Testing Commands**
```powershell
# Build application
.\scripts\test-shortcuts.ps1 build

# Run application  
.\scripts\test-shortcuts.ps1 run

# Run with file watching
.\scripts\test-shortcuts.ps1 watch

# Check system status
.\scripts\test-shortcuts.ps1 status

# Create backup
.\scripts\backup-shopboss-beta.ps1

# Test backup/restore
.\scripts\test-backup-restore.ps1
```

### **Test Data Locations**
- **Sample SDF Files**: `test-data/`
- **Test Checkpoints**: `checkpoints/`
- **Test Backups**: `C:\ShopBoss-Backups\`

---

## **Pre-Testing Setup**

### **Environment Preparation**
1. **Clean Database State**
   ```powershell
   .\scripts\clean-sqlite-locks.ps1
   .\scripts\test-shortcuts.ps1 reset  # Use fresh-install checkpoint
   ```

2. **Verify Dependencies**
   ```powershell
   dotnet --version  # Should be 8.0 or higher
   Get-Process -Name "ShopBoss.Web" -ErrorAction SilentlyContinue  # Should be empty
   ```

3. **Create Test Checkpoint**
   ```powershell
   .\scripts\test-shortcuts.ps1 checkpoint
   # Enter name: "before-test-run"
   # Enter description: "Clean state before testing"
   ```

---

## **Testing Workflows**

### **Workflow 1: Import and Basic Operations**

#### **Test Steps**
1. **Start Application**
   ```powershell
   .\scripts\test-shortcuts.ps1 run
   ```
   - Navigate to http://localhost:5000
   - Verify no errors in console

2. **Import SDF File**
   - Go to Admin > Import
   - Upload `test-data/MicrovellumWorkOrder.sdf`
   - Wait for processing (should complete in 30-60 seconds)
   - Verify tree structure displays correctly

3. **Configure Storage Racks**
   - Go to Admin > Rack Configuration
   - Verify default racks are present
   - Add test rack: "TEST-RACK-01", 10 bins, 5 shelves

4. **Select and Import Data**
   - In tree view, select a work order with products
   - Click "Import Selected Items"
   - Verify success message
   - Check Admin > Work Orders shows imported data

#### **Expected Results**
- ✅ SDF file processes without errors
- ✅ Tree structure displays all hierarchy levels
- ✅ Import completes successfully
- ✅ Work order appears in Admin dashboard

#### **Common Issues**
- **Issue**: "File format not supported"
  - **Solution**: Ensure SDF file is valid Microvellum format
  - **Check**: File size should be > 1MB for real work orders

- **Issue**: "Import process hangs"
  - **Solution**: Check importer tool in task manager, restart if needed
  - **Check**: Verify temp directory permissions

### **Workflow 2: CNC Station Operations**

#### **Test Steps**
1. **Set Active Work Order**
   - Admin > Work Orders > Set Active (select imported work order)
   - Verify "Active Work Order" indicator appears

2. **Navigate to CNC Station**
   - Go to CNC Station (http://localhost:5000/Cnc)
   - Verify active work order displays
   - Check nest sheets section shows available sheets

3. **Test Nest Sheet Scanning**
   - Use test barcode: `NEST-001` (or scan actual nest sheet)
   - Verify parts list displays
   - Check "Mark All as Cut" functionality

4. **Test Individual Part Scanning**
   - Use test barcode: `PART-001` (or scan actual part)
   - Verify part status changes to "Cut"
   - Check progress counters update

#### **Expected Results**
- ✅ CNC station loads with active work order
- ✅ Nest sheet scanning works correctly
- ✅ Part status updates propagate to other stations
- ✅ Progress indicators update in real-time

#### **Common Issues**
- **Issue**: "No active work order"
  - **Solution**: Set active work order in Admin section
  - **Check**: Verify work order has parts in "Pending" status

- **Issue**: "Barcode not recognized"
  - **Solution**: Verify barcode format matches expected pattern
  - **Check**: Test with known good barcodes first

### **Workflow 3: Sorting Station Operations**

#### **Test Steps**
1. **Navigate to Sorting Station**
   - Go to Sorting Station (http://localhost:5000/Sorting)
   - Verify rack visualization displays

2. **Test Rack Navigation**
   - Use command: `NAV-SORTING-RACK-1`
   - Verify rack display changes
   - Check bin availability indicators

3. **Test Part Sorting**
   - Scan cut part barcode
   - Verify bin assignment appears
   - Check part status changes to "Sorted"

4. **Test Assembly Readiness**
   - Sort multiple parts for same product
   - Verify assembly readiness calculations
   - Check assembly queue updates

#### **Expected Results**
- ✅ Sorting station displays rack visualization
- ✅ Navigation commands work correctly
- ✅ Part sorting updates bin occupancy
- ✅ Assembly readiness calculations are accurate

#### **Common Issues**
- **Issue**: "No available bins"
  - **Solution**: Check rack configuration and capacity
  - **Check**: Verify rack has sufficient bins for part types

- **Issue**: "Part already sorted"
  - **Solution**: Normal behavior - parts can only be sorted once
  - **Check**: Verify part status in Admin > Work Orders

### **Workflow 4: Assembly Station Operations**

#### **Test Steps**
1. **Navigate to Assembly Station**
   - Go to Assembly Station (http://localhost:5000/Assembly)
   - Verify assembly queue displays

2. **Test Product Assembly**
   - Select product ready for assembly
   - Verify component locations display
   - Scan product barcode to complete assembly

3. **Test Component Guidance**
   - Check part location indicators
   - Verify bin number display
   - Test component status indicators

#### **Expected Results**
- ✅ Assembly queue shows ready products
- ✅ Component locations are accurate
- ✅ Assembly completion updates all related parts
- ✅ Assembly status propagates to shipping

#### **Common Issues**
- **Issue**: "Missing components"
  - **Solution**: Verify all parts are sorted first
  - **Check**: Review sorting station for missing parts

- **Issue**: "Assembly already complete"
  - **Solution**: Normal behavior - products can only be assembled once
  - **Check**: Verify product status in Admin dashboard

### **Workflow 5: Shipping Station Operations**

#### **Test Steps**
1. **Navigate to Shipping Station**
   - Go to Shipping Station (http://localhost:5000/Shipping)
   - Verify shipping queue displays

2. **Test Product Shipping**
   - Select assembled product
   - Verify shipping checklist
   - Complete shipping process

3. **Test Work Order Completion**
   - Ship all products in work order
   - Verify work order completion
   - Check final status updates

#### **Expected Results**
- ✅ Shipping queue shows assembled products
- ✅ Shipping checklist is complete and accurate
- ✅ Work order completion triggers correctly
- ✅ Final audit trail is complete

#### **Common Issues**
- **Issue**: "Product not ready for shipping"
  - **Solution**: Verify product is fully assembled
  - **Check**: Review assembly station for missing steps

- **Issue**: "Work order not completing"
  - **Solution**: Check for unshipped products or hardware
  - **Check**: Verify all DetachedProducts are processed

---

## **Advanced Testing Scenarios**

### **Scenario 1: Large Work Order (1000+ Parts)**

#### **Test Steps**
1. **Import Large SDF File**
   - Use `test-data/LargeWorkOrder.sdf` (if available)
   - Monitor performance during import
   - Check memory usage

2. **Performance Testing**
   - Navigate between stations
   - Measure page load times
   - Test scanning responsiveness

3. **Stress Testing**
   - Process multiple nest sheets simultaneously
   - Sort large numbers of parts
   - Monitor system stability

#### **Expected Results**
- ✅ Page loads under 3 seconds
- ✅ Scanning remains responsive
- ✅ Memory usage stays stable
- ✅ No performance degradation

### **Scenario 2: Concurrent Station Usage**

#### **Test Steps**
1. **Open Multiple Browser Windows**
   - CNC Station (Window 1)
   - Sorting Station (Window 2)
   - Assembly Station (Window 3)

2. **Test Real-time Updates**
   - Scan part at CNC station
   - Verify update appears at sorting station
   - Check assembly readiness updates

3. **Test Concurrent Operations**
   - Scan different parts simultaneously
   - Verify no conflicts or errors
   - Check audit trail accuracy

#### **Expected Results**
- ✅ Real-time updates work across all stations
- ✅ No conflicts with concurrent operations
- ✅ Audit trail captures all operations
- ✅ Data consistency maintained

### **Scenario 3: Error Recovery Testing**

#### **Test Steps**
1. **Simulate Database Lock**
   - Stop application during operation
   - Manually create lock files
   - Test recovery process

2. **Test Backup Recovery**
   - Corrupt database intentionally
   - Restore from backup
   - Verify data integrity

3. **Test Network Interruption**
   - Disconnect network during operation
   - Reconnect and verify recovery
   - Check SignalR reconnection

#### **Expected Results**
- ✅ Lock cleanup resolves database issues
- ✅ Backup recovery restores full functionality
- ✅ Network interruption recovery works
- ✅ No data loss during recovery

---

## **Manual Testing Checklist**

### **Pre-Testing Checklist**
- [ ] Development environment is clean
- [ ] Database is in known good state
- [ ] All dependencies are installed
- [ ] Test data is available
- [ ] Backup created before testing

### **Import Testing**
- [ ] SDF file upload works
- [ ] Processing completes without errors
- [ ] Tree structure displays correctly
- [ ] Import selection functions properly
- [ ] Import progress updates in real-time

### **Station Testing**
- [ ] All stations load without errors
- [ ] Navigation between stations works
- [ ] Scanner functionality works at each station
- [ ] Real-time updates function correctly
- [ ] Status changes propagate properly

### **Workflow Testing**
- [ ] Complete workflow (Import → CNC → Sorting → Assembly → Shipping)
- [ ] Status transitions are correct
- [ ] Progress indicators update accurately
- [ ] Audit trail is complete
- [ ] Data integrity maintained throughout

### **Error Testing**
- [ ] Invalid barcode handling
- [ ] Duplicate operation prevention
- [ ] Missing dependency detection
- [ ] Error messages are clear and actionable
- [ ] System recovery after errors

### **Performance Testing**
- [ ] Page load times under 3 seconds
- [ ] Scanning response under 1 second
- [ ] Large data set handling
- [ ] Memory usage remains stable
- [ ] No performance degradation over time

---

## **Common Issues and Solutions**

### **Issue: Scanner Not Working**
**Symptoms**: Barcode scans not recognized, no response from scanner
**Solutions**:
1. Check scanner connection and power
2. Verify scanner is configured for USB HID mode
3. Test with known good barcodes
4. Check browser compatibility
5. Verify scanner drivers are installed

### **Issue: Import Process Fails**
**Symptoms**: SDF import hangs, error messages during processing
**Solutions**:
1. Check SDF file format and validity
2. Verify temp directory permissions
3. Check available disk space
4. Restart importer service
5. Check for file corruption

### **Issue: Database Errors**
**Symptoms**: Application won't start, database connection errors
**Solutions**:
1. Run SQLite lock cleanup script
2. Check database file permissions
3. Verify database file integrity
4. Restore from backup if corrupted
5. Check connection string configuration

### **Issue: Performance Problems**
**Symptoms**: Slow page loads, unresponsive interface
**Solutions**:
1. Check system resources (CPU, memory)
2. Clean browser cache
3. Restart application
4. Check database performance
5. Verify network connectivity

### **Issue: Real-time Updates Not Working**
**Symptoms**: Status changes not appearing, SignalR disconnections
**Solutions**:
1. Check SignalR connection status
2. Verify network connectivity
3. Check firewall settings
4. Restart application
5. Clear browser cache

---

## **Test Data Management**

### **Creating Test Data**
```powershell
# Create test checkpoint
.\scripts\test-shortcuts.ps1 checkpoint

# Import test work order
# Navigate to Admin > Import
# Upload test-data/MicrovellumWorkOrder.sdf
# Import selected items

# Create test data checkpoint
.\scripts\test-shortcuts.ps1 checkpoint
# Name: "with-test-workorder"
# Description: "Test data loaded and ready"
```

### **Resetting Test Environment**
```powershell
# Reset to clean state
.\scripts\test-shortcuts.ps1 reset

# Or restore specific checkpoint
.\scripts\test-shortcuts.ps1 restore-checkpoint "fresh-install"
```

### **Test Data Validation**
```powershell
# Verify test data integrity
.\scripts\test-shortcuts.ps1 status

# Check work order data
# Navigate to Admin > Work Orders
# Verify expected work orders are present
```

---

## **Automated Testing Integration**

### **Running Unit Tests**
```powershell
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter Category=Integration
```

### **Integration Testing**
```powershell
# Test backup/restore cycle
.\scripts\test-backup-restore.ps1

# Test full workflow
.\scripts\test-full-workflow.ps1  # If available
```

---

## **Performance Benchmarks**

### **Target Performance Metrics**
- **Page Load Time**: < 3 seconds
- **Scanner Response**: < 1 second
- **Import Processing**: < 2 minutes per 1000 parts
- **Database Query**: < 100ms average
- **Memory Usage**: < 1GB for typical work orders

### **Performance Testing Commands**
```powershell
# Monitor performance
Get-Process -Name "ShopBoss.Web" | Select-Object CPU, WorkingSet, PagedMemorySize

# Check response times
Measure-Command { Invoke-WebRequest http://localhost:5000/Cnc }
```

---

## **Testing Documentation**

### **Test Results Template**
```
Test Run: [Date/Time]
Tester: [Name]
Environment: [Development/Staging/Production]
ShopBoss Version: [Version]

Test Results:
- Import Testing: [PASS/FAIL]
- CNC Station: [PASS/FAIL]
- Sorting Station: [PASS/FAIL]
- Assembly Station: [PASS/FAIL]
- Shipping Station: [PASS/FAIL]
- Performance: [PASS/FAIL]

Issues Found:
- [Issue 1]: [Description and Resolution]
- [Issue 2]: [Description and Resolution]

Overall Status: [PASS/FAIL]
Ready for [Next Phase/Deployment]: [YES/NO]
```

### **Bug Report Template**
```
Bug Report #[Number]
Date: [Date]
Tester: [Name]
Severity: [Critical/High/Medium/Low]

Description:
[Detailed description of the issue]

Steps to Reproduce:
1. [Step 1]
2. [Step 2]
3. [Step 3]

Expected Result:
[What should happen]

Actual Result:
[What actually happened]

Environment:
- OS: [Operating System]
- Browser: [Browser and Version]
- ShopBoss Version: [Version]

Screenshots/Logs:
[Attach relevant files]
```

---

## **Testing Schedule**

### **Daily Testing** (During Development)
- [ ] Build and run application
- [ ] Test basic functionality
- [ ] Check for new issues
- [ ] Verify recent changes

### **Weekly Testing** (Beta Phase)
- [ ] Complete workflow testing
- [ ] Performance testing
- [ ] Error recovery testing
- [ ] Backup/restore testing

### **Release Testing**
- [ ] Full regression testing
- [ ] Performance benchmarking
- [ ] Security testing
- [ ] User acceptance testing

---

**Remember**: Testing is critical for beta success. Document all issues, follow procedures consistently, and maintain test data integrity throughout the testing process.