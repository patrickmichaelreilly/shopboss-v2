C:\ShopBoss-Testing>shopboss.web.exe
2025-07-10 09:16:52 info: ShopBoss.Web.Services.BackupBackgroundService[0]
      BackupBackgroundService started
2025-07-10 09:16:52 warn: Microsoft.EntityFrameworkCore.Query[10103]
      The query uses the 'First'/'FirstOrDefault' operator without 'OrderBy' and filter operators. This may lead to unpredictable results.
2025-07-10 09:16:52 info: ShopBoss.Web.Services.BackupBackgroundService[0]
      Starting automatic backup
2025-07-10 09:16:52 info: ShopBoss.Web.Services.HealthMonitoringService[0]
      HealthMonitoringService started
2025-07-10 09:16:52 warn: Microsoft.AspNetCore.Server.Kestrel[0]
      Overriding address(es) 'http://0.0.0.0:5000'. Binding to endpoints defined via IConfiguration and/or UseKestrel() instead.
2025-07-10 09:16:52 info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
2025-07-10 09:16:52 info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
2025-07-10 09:16:52 info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
2025-07-10 09:16:52 info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\ShopBoss-Testing
2025-07-10 09:16:52 warn: Microsoft.EntityFrameworkCore.Query[10103]
      The query uses the 'First'/'FirstOrDefault' operator without 'OrderBy' and filter operators. This may lead to unpredictable results.
2025-07-10 09:16:52 info: ShopBoss.Web.Services.BackupService[0]
      Backup created successfully: C:\ShopBoss-Testing\Backups\shopboss_backup_20250710_141652.db.gz (Type: Automatic)
2025-07-10 09:16:52 info: ShopBoss.Web.Services.AuditTrailService[0]
      Audit log created: Backup on Created Automatic backup created: shopboss_backup_20250710_141652.db (18.4 KB) from
2025-07-10 09:16:52 info: ShopBoss.Web.Services.BackupBackgroundService[0]
      Automatic backup completed successfully: C:\ShopBoss-Testing\Backups\shopboss_backup_20250710_141652.db.gz
2025-07-10 09:16:52 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 09:16:52 warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3]
      Failed to determine the https port for redirect.
2025-07-10 09:16:52 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 72.8879ms
2025-07-10 09:17:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/ - - -
2025-07-10 09:17:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/ - 200 - text/html;+charset=utf-8 103.3110ms
2025-07-10 09:17:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-10 09:17:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 09:17:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 6.9492ms
2025-07-10 09:17:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 26.2903ms
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=AHbbJXvOtVUiVaO7_FOmLw - - -
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 2.5658ms
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/Import - - -
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/Import - 200 - text/html;+charset=utf-8 11.6454ms
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=AHbbJXvOtVUiVaO7_FOmLw - 101 - - 578.9088ms
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.7835ms
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 3.6593ms
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/importProgress/negotiate?negotiateVersion=1 - - 0
2025-07-10 09:17:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/importProgress/negotiate?negotiateVersion=1 - 200 316 application/json 0.5324ms
2025-07-10 09:17:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=nNeOJTg0xFLv_YRiFg9MZw - - -
2025-07-10 09:17:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 09:17:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.6756ms
2025-07-10 09:17:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/importProgress?id=ZNHg4ZFnI5UA1KikstxUrg - - -
2025-07-10 09:17:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Import/UploadFile - multipart/form-data;+boundary=----WebKitFormBoundaryzO5VbCENHGCt45Wl 1048785
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Assembly location: C:\Users\Patrick\AppData\Local\Temp\.net\ShopBoss.Web\1fBV7HMPe3jiFxujzbQfUZ55jC8j8KA=\ShopBoss.Web.dll
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Detected Windows Service temp directory, skipping
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 - Process path: C:\ShopBoss-Testing\ShopBoss.Web.exe
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 SUCCESS - Using process directory: C:\ShopBoss-Testing
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer path resolved to: C:\ShopBoss-Testing\tools\importer\bin\Release\net8.0\win-x86\Importer.exe
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer executable exists: True
2025-07-10 09:17:08 info: ShopBoss.Web.Controllers.ImportController[0]
      File uploaded successfully: ShopBossWorkOrder.sdf (Session: 3114d474-9f33-46ac-b4ee-98ed29b4c809)
2025-07-10 09:17:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Import/UploadFile - 200 - application/json;+charset=utf-8 35.3417ms
2025-07-10 09:17:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Import/StartImport - application/json 73
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Assembly location: C:\Users\Patrick\AppData\Local\Temp\.net\ShopBoss.Web\1fBV7HMPe3jiFxujzbQfUZ55jC8j8KA=\ShopBoss.Web.dll
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Detected Windows Service temp directory, skipping
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 - Process path: C:\ShopBoss-Testing\ShopBoss.Web.exe
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 SUCCESS - Using process directory: C:\ShopBoss-Testing
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer path resolved to: C:\ShopBoss-Testing\tools\importer\bin\Release\net8.0\win-x86\Importer.exe
2025-07-10 09:17:08 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer executable exists: True
2025-07-10 09:17:08 info: ShopBoss.Web.Controllers.ImportController[0]
      Import started for session 3114d474-9f33-46ac-b4ee-98ed29b4c809
2025-07-10 09:17:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Import/StartImport - 200 - application/json;+charset=utf-8 12.2824ms
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Successfully imported SDF file C:\ShopBoss-Testing\temp\uploads\3114d474-9f33-46ac-b4ee-98ed29b4c809_ShopBossWorkOrder.sdf in 2.5 seconds
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImportDataTransformService[0]
      Starting data transformation. Products: 2, Parts: 7, Subassemblies: 1, Hardware: 6, NestSheets: 2
2025-07-10 09:17:11 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImportDataTransformService[0]
      Processing OptimizationResults. Total records: 7
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImportDataTransformService[0]
      Created part to sheet mapping with 7 entries
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImportDataTransformService[0]
      Established nest sheet relationships for 7 parts using OptimizationResults
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImportDataTransformService[0]
      Identified 1 single-part products as detached products during transformation
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImportDataTransformService[0]
      Successfully transformed import data for work order Imported Work Order
2025-07-10 09:17:11 info: ShopBoss.Web.Controllers.ImportController[0]
      Import completed successfully for session 3114d474-9f33-46ac-b4ee-98ed29b4c809
2025-07-10 09:17:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Import/GetImportData?sessionId=3114d474-9f33-46ac-b4ee-98ed29b4c809 - - -
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Assembly location: C:\Users\Patrick\AppData\Local\Temp\.net\ShopBoss.Web\1fBV7HMPe3jiFxujzbQfUZ55jC8j8KA=\ShopBoss.Web.dll
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Detected Windows Service temp directory, skipping
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 - Process path: C:\ShopBoss-Testing\ShopBoss.Web.exe
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 SUCCESS - Using process directory: C:\ShopBoss-Testing
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer path resolved to: C:\ShopBoss-Testing\tools\importer\bin\Release\net8.0\win-x86\Importer.exe
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer executable exists: True
2025-07-10 09:17:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Import/GetImportData?sessionId=3114d474-9f33-46ac-b4ee-98ed29b4c809 - 200 - application/json;+charset=utf-8 19.5693ms
2025-07-10 09:17:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Import/GetImportTreeData?sessionId=3114d474-9f33-46ac-b4ee-98ed29b4c809 - - -
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Assembly location: C:\Users\Patrick\AppData\Local\Temp\.net\ShopBoss.Web\1fBV7HMPe3jiFxujzbQfUZ55jC8j8KA=\ShopBoss.Web.dll
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Detected Windows Service temp directory, skipping
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 - Process path: C:\ShopBoss-Testing\ShopBoss.Web.exe
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 SUCCESS - Using process directory: C:\ShopBoss-Testing
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer path resolved to: C:\ShopBoss-Testing\tools\importer\bin\Release\net8.0\win-x86\Importer.exe
2025-07-10 09:17:11 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer executable exists: True
2025-07-10 09:17:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Import/GetImportTreeData?sessionId=3114d474-9f33-46ac-b4ee-98ed29b4c809 - 200 - application/json;+charset=utf-8 6.5694ms
2025-07-10 09:17:16 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Import/ProcessSelectedItems - application/json 2322
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Assembly location: C:\Users\Patrick\AppData\Local\Temp\.net\ShopBoss.Web\1fBV7HMPe3jiFxujzbQfUZ55jC8j8KA=\ShopBoss.Web.dll
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 1 - Detected Windows Service temp directory, skipping
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 - Process path: C:\ShopBoss-Testing\ShopBoss.Web.exe
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImporterService[0]
      Strategy 2 SUCCESS - Using process directory: C:\ShopBoss-Testing
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer path resolved to: C:\ShopBoss-Testing\tools\importer\bin\Release\net8.0\win-x86\Importer.exe
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer executable exists: True
2025-07-10 09:17:16 info: ShopBoss.Web.Controllers.ImportController[0]
      Processing selected items for session 3114d474-9f33-46ac-b4ee-98ed29b4c809
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImportSelectionService[0]
      Starting conversion of selected items for work order: Shopboss
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImportSelectionService[0]
      Created DetachedProduct '2573I83N9PKF_detached_d57673c4' with associated Part '2573I89B6K4G'
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImportSelectionService[0]
      Imported 1 selected detached products with 1 associated parts
2025-07-10 09:17:16 info: ShopBoss.Web.Services.ImportSelectionService[0]
      Successfully saved work order 05511699-ace3-4e8a-bab1-afb7ed4fbacf with 1 products, 7 parts, 1 subassemblies, 6 hardware items
2025-07-10 09:17:16 info: ShopBoss.Web.Controllers.ImportController[0]
      Successfully processed selected items for session 3114d474-9f33-46ac-b4ee-98ed29b4c809. Converted: 1 products, 7 parts, 1 subassemblies, 6 hardware
2025-07-10 09:17:16 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Import/ProcessSelectedItems - 200 - application/json;+charset=utf-8 149.3684ms
2025-07-10 09:17:19 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin - - -
2025-07-10 09:17:19 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin - 200 - text/html;+charset=utf-8 5.6244ms
2025-07-10 09:17:19 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=nNeOJTg0xFLv_YRiFg9MZw - 101 - - 14554.2188ms
2025-07-10 09:17:19 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/importProgress?id=ZNHg4ZFnI5UA1KikstxUrg - 101 - - 14540.2748ms
2025-07-10 09:17:19 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-10 09:17:19 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.2818ms
2025-07-10 09:17:19 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 09:17:19 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.3878ms
2025-07-10 09:17:20 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=-4lMtR9A6uVghCb4tFb9mg - - -
2025-07-10 09:17:20 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 09:17:20 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.3551ms