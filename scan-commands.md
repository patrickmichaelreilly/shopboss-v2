  Complete Command Barcode Reference

  Navigation Commands (NAV-*)

  - NAV-ADMIN → Go to Admin station
  - NAV-CNC → Go to CNC station
  - NAV-SORTING → Go to Sorting station
  - NAV-ASSEMBLY → Go to Assembly station
  - NAV-SHIPPING → Go to Shipping station
  - NAV-HEALTH → Go to Health Dashboard
  - NAV-BACKUP → Go to Backup Management
  - NAV-RACKS → Go to Rack Configuration

  System Commands (CMD-*)

  - CMD-REFRESH → Refresh current page
  - CMD-HELP → Show help information
  - CMD-CANCEL → Cancel current operation
  - CMD-CLEAR → Clear session data
  - CMD-LOGOUT → Logout user
  - CMD-RECENT → Show recent scans
  - CMD-SUMMARY → Show work order summary

  Admin Commands (ADMIN-*)

  - ADMIN-BACKUP → Create backup
  - ADMIN-ARCHIVE → Archive active work order
  - ADMIN-CLEARSESSIONS → Clear all sessions
  - ADMIN-HEALTHCHECK → Run health check
  - ADMIN-AUDITLOG → View audit log

  Station Commands (STN-[STATION]-[COMMAND])

  CNC Station:
  - STN-CNC-RECENT → Show recent nest sheets
  - STN-CNC-UNPROCESSED → Show unprocessed nest sheets

  Sorting Station:
  - STN-SORTING-RACKS → Show rack summary
  - STN-SORTING-READY → Show assembly readiness

  Assembly Station:
  - STN-ASSEMBLY-QUEUE → Show assembly queue
  - STN-ASSEMBLY-PROGRESS → Show product progress

  Shipping Station:
  - STN-SHIPPING-QUEUE → Show shipping queue
  - STN-SHIPPING-PROGRESS → Show work order progress

  Location of Definitions

  The command definitions are found in two main files:

  1. /src/ShopBoss.Web/Models/Scanner/BarcodeType.cs - Contains the enum definitions for command types
  2. /src/ShopBoss.Web/Services/UniversalScannerService.cs - Contains the parsing logic that maps barcode strings to commands
  (lines 274-344)

  All command barcodes use hyphens as separators (not colons) to be compatible with Code 39 barcode format for real barcode
  printers.