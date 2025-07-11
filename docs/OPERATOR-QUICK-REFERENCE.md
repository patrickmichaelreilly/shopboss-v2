# ShopBoss Operator Quick Reference Cards

## 📋 Station-Specific Quick Reference Cards

These cards provide essential information for each station operator. Print these for easy access at each workstation.

---

## **🏭 Admin Station Quick Reference**

### **Primary Functions**
- Import SDF files
- Manage work orders
- Configure system settings
- Monitor system health
- Manage backups

### **Key Actions**
| Action | Location | Notes |
|--------|----------|--------|
| Import SDF | Admin → Import | Drag & drop SDF file |
| Set Active Work Order | Admin → Work Orders → Set Active | Only one active at a time |
| Create Backup | Admin → Backup Management → Create Manual Backup | Creates immediate backup |
| View Health Status | Admin → Health Dashboard | Check system status |
| Configure Racks | Admin → Rack Configuration | Set up storage racks |

### **Common Issues**
- **Import hangs**: Check file size and format
- **No work orders**: Verify import completed successfully
- **Backup fails**: Check disk space and permissions

### **Emergency Contacts**
- **IT Support**: [Phone Number]
- **System Admin**: [Phone Number]

---

## **🔧 CNC Station Quick Reference**

### **Primary Functions**
- Scan nest sheets
- Mark parts as cut
- Track cutting progress
- Update part statuses

### **Key Actions**
| Action | Scanner Command | Notes |
|--------|----------------|--------|
| Scan Nest Sheet | [Nest Sheet Barcode] | Loads all parts on sheet |
| Mark All Parts Cut | Click "Mark All as Cut" | After scanning nest sheet |
| Scan Individual Part | [Part Barcode] | Marks single part as cut |
| Check Progress | View progress indicators | Shows cutting completion |

### **Scanner Commands**
- **Nest Sheet**: `NEST-[number]`
- **Individual Part**: `PART-[number]`
- **Navigation**: `NAV-CNC`

### **Status Indicators**
- **Pending**: ⏳ Not yet cut
- **Cut**: ✅ Cut and ready for sorting
- **Progress**: Shows percentage complete

### **Common Issues**
- **Barcode not recognized**: Check scanner connection
- **No active work order**: Set active work order in Admin
- **Part already cut**: Normal - parts can only be cut once

### **Emergency**
- **Scanner fails**: Use manual entry in Admin station
- **System down**: Use paper tracking, update when restored

---

## **📦 Sorting Station Quick Reference**

### **Primary Functions**
- Assign parts to storage bins
- Navigate between racks
- Update part locations
- Monitor assembly readiness

### **Key Actions**
| Action | Scanner Command | Notes |
|--------|----------------|--------|
| Navigate to Rack | `NAV-SORTING-RACK-[number]` | Changes rack display |
| Scan Part | [Part Barcode] | Shows bin assignment |
| Sort Part | Follow bin assignment | Part moves to "Sorted" |
| Check Assembly Ready | View assembly queue | Shows ready products |

### **Rack Navigation**
- **Rack 1**: `NAV-SORTING-RACK-1`
- **Rack 2**: `NAV-SORTING-RACK-2`
- **Rack 3**: `NAV-SORTING-RACK-3`
- **Default View**: `NAV-SORTING`

### **Bin Colors**
- **Green**: Available bin
- **Yellow**: Partially filled
- **Red**: Full bin
- **Gray**: Unavailable

### **Part Routing**
- **Doors/Fronts**: Specialized racks
- **Carcass Parts**: Grouped by product
- **Hardware**: Separate storage

### **Common Issues**
- **No available bins**: Check rack capacity
- **Part already sorted**: Normal - parts can only be sorted once
- **Wrong bin assignment**: Check part type and product

### **Emergency**
- **System down**: Use paper bin tags, update when restored
- **Scanner fails**: Use manual bin assignment

---

## **🔨 Assembly Station Quick Reference**

### **Primary Functions**
- View assembly queue
- Locate part components
- Complete product assembly
- Update assembly status

### **Key Actions**
| Action | Scanner Command | Notes |
|--------|----------------|--------|
| View Assembly Queue | Navigate to Assembly | Shows ready products |
| Select Product | Click product in queue | Shows component locations |
| Scan Product | [Product Barcode] | Completes assembly |
| Check Component Status | View component list | Shows sorted/missing parts |

### **Assembly Indicators**
- **Ready**: ✅ All components available
- **Partial**: ⏳ Some components missing
- **Complete**: 🏁 Assembly finished

### **Component Status**
- **Sorted**: Part is in assigned bin
- **Missing**: Part not yet sorted
- **Location**: Bin number for each part

### **Assembly Process**
1. Select ready product from queue
2. Gather all components using location guide
3. Complete assembly
4. Scan product barcode to finish

### **Common Issues**
- **Missing components**: Check sorting station
- **Product not in queue**: Verify all parts are sorted
- **Assembly already complete**: Normal - products can only be assembled once

### **Emergency**
- **System down**: Use paper component lists
- **Scanner fails**: Use manual completion in Admin

---

## **🚚 Shipping Station Quick Reference**

### **Primary Functions**
- View shipping queue
- Verify product completeness
- Process shipping
- Complete work orders

### **Key Actions**
| Action | Scanner Command | Notes |
|--------|----------------|--------|
| View Shipping Queue | Navigate to Shipping | Shows assembled products |
| Select Product | Click product in queue | Shows shipping checklist |
| Scan Product | [Product Barcode] | Marks as shipped |
| Complete Work Order | All products shipped | Automatic completion |

### **Shipping Checklist**
- **Product Complete**: ✅ All assembly finished
- **Hardware Included**: ✅ All hardware items
- **Quality Check**: ✅ Visual inspection
- **Documentation**: ✅ Shipping papers

### **Product Status**
- **Ready**: Product fully assembled
- **Shipped**: Product processed for shipping
- **Complete**: Work order finished

### **Work Order Completion**
- All products shipped
- All hardware processed
- All detached products handled

### **Common Issues**
- **Product not ready**: Check assembly station
- **Work order not completing**: Verify all items shipped
- **Missing hardware**: Check Admin station

### **Emergency**
- **System down**: Use paper shipping logs
- **Scanner fails**: Use manual completion in Admin

---

## **🚨 Emergency Quick Reference**

### **Emergency Contacts**
- **IT Support**: [Phone Number]
- **System Admin**: [Phone Number]
- **Management**: [Phone Number]

### **Emergency Levels**
- **🔴 CRITICAL**: Production stopped - Call immediately
- **🟡 WARNING**: Degraded operations - Call within 10 minutes
- **🟢 NOTICE**: Minor issues - Log and report

### **Common Emergency Actions**
| Issue | Action | Command |
|-------|--------|---------|
| System Down | Call IT Support | [Phone Number] |
| Scanner Not Working | Try different scanner | Check connections |
| Can't Access Station | Try different browser | Clear cache |
| Database Error | Call System Admin | [Phone Number] |

### **Manual Workarounds**
- **CNC Down**: Use paper tracking
- **Sorting Down**: Use paper bin tags
- **Assembly Down**: Use paper component lists
- **Shipping Down**: Use paper shipping logs

### **Recovery Commands**
```powershell
# Clean database locks
C:\ShopBoss\scripts\clean-sqlite-locks.ps1

# Check system status
C:\ShopBoss\scripts\test-shortcuts.ps1 status

# Create emergency backup
C:\ShopBoss\scripts\backup-shopboss-beta.ps1 -BackupType "emergency"
```

---

## **📱 Universal Scanner Commands**

### **Navigation Commands**
- `NAV-ADMIN` - Go to Admin station
- `NAV-CNC` - Go to CNC station
- `NAV-SORTING` - Go to Sorting station
- `NAV-SORTING-RACK-[X]` - Go to specific rack
- `NAV-ASSEMBLY` - Go to Assembly station
- `NAV-SHIPPING` - Go to Shipping station

### **Barcode Formats**
- **Nest Sheet**: `NEST-[number]`
- **Part**: `PART-[number]`
- **Product**: `PROD-[number]`
- **Hardware**: `HW-[number]`

### **Scanner Status**
- **Green Light**: Ready to scan
- **Red Light**: Scanner error
- **No Light**: Scanner disconnected

### **Scanner Troubleshooting**
1. Check USB connection
2. Check power/battery
3. Test with known good barcode
4. Restart browser
5. Call IT Support

---

## **⚙️ System Status Indicators**

### **Connection Status**
- **Connected**: 🟢 System responding normally
- **Disconnected**: 🔴 System not responding
- **Reconnecting**: 🟡 Attempting to reconnect

### **Real-time Updates**
- **Active**: Updates appear immediately
- **Delayed**: Updates appear after delay
- **Stopped**: No updates received

### **Station Status**
- **Operational**: Station working normally
- **Degraded**: Station slow or partial function
- **Offline**: Station not accessible

### **Work Order Status**
- **Active**: Currently processing
- **Paused**: Temporarily stopped
- **Complete**: All items processed

---

## **🔧 Basic Troubleshooting**

### **Page Won't Load**
1. Check internet connection
2. Refresh page (F5)
3. Clear browser cache
4. Try different browser
5. Restart computer

### **Scanner Not Working**
1. Check USB connection
2. Check scanner power
3. Test with known barcode
4. Try different scanner
5. Use manual entry

### **System Slow**
1. Close unnecessary programs
2. Restart browser
3. Check network connection
4. Clear browser cache
5. Restart computer

### **Error Messages**
1. Read error message carefully
2. Try the suggested action
3. Refresh page
4. Log out and log back in
5. Call IT Support

---

## **📞 Quick Contact List**

### **Technical Support**
- **Primary**: [Phone] - [Name]
- **Secondary**: [Phone] - [Name]
- **Emergency**: [Phone] - [Name]

### **Management**
- **Shop Manager**: [Phone] - [Name]
- **IT Manager**: [Phone] - [Name]
- **General Manager**: [Phone] - [Name]

### **Vendors**
- **Scanner Support**: [Phone] - [Vendor]
- **Hardware Support**: [Phone] - [Vendor]
- **Software Support**: [Phone] - [Vendor]

---

## **📝 Daily Checklist**

### **Start of Shift**
- [ ] Check system is running
- [ ] Test scanner at your station
- [ ] Verify network connection
- [ ] Check for any error messages
- [ ] Review active work order

### **During Shift**
- [ ] Report any issues immediately
- [ ] Follow proper scanning procedures
- [ ] Keep station area clean
- [ ] Monitor system status indicators
- [ ] Document any manual workarounds

### **End of Shift**
- [ ] Complete any pending operations
- [ ] Report any issues to next shift
- [ ] Clean scanner and workstation
- [ ] Log out of system
- [ ] Secure any manual records

---

**Remember: When in doubt, ask for help! It's better to get assistance than to make a mistake that could affect production.**