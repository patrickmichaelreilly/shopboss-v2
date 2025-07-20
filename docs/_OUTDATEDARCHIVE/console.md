2025-07-18 12:09:04 info: ShopBoss.Web.Services.BackupBackgroundService[0]
      BackupBackgroundService started
2025-07-18 12:09:04 warn: Microsoft.EntityFrameworkCore.Query[10103]
      The query uses the 'First'/'FirstOrDefault' operator without 'OrderBy' and filter operators. This may lead to unpredictable results.
2025-07-18 12:09:04 info: ShopBoss.Web.Services.HealthMonitoringService[0]
      HealthMonitoringService started
2025-07-18 12:09:04 warn: Microsoft.EntityFrameworkCore.Query[10103]
      The query uses the 'First'/'FirstOrDefault' operator without 'OrderBy' and filter operators. This may lead to unpredictable results.
2025-07-18 12:09:04 warn: Microsoft.AspNetCore.Server.Kestrel[0]
      Overriding address(es) 'http://0.0.0.0:5000'. Binding to endpoints defined via IConfiguration and/or UseKestrel() instead.
2025-07-18 12:09:04 info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
2025-07-18 12:09:04 info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
2025-07-18 12:09:04 info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
2025-07-18 12:09:04 info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\ShopBoss-Testing
2025-07-18 12:09:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:04 warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3]
      Failed to determine the https port for redirect.
2025-07-18 12:09:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 60.9531ms
2025-07-18 12:09:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/ - - -
2025-07-18 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/ - 200 - text/html;+charset=utf-8 111.1844ms
2025-07-18 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-18 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 7.3911ms
2025-07-18 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 30.3787ms
2025-07-18 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=sWi7u2qYM9TadL2pHt69tQ - - -
2025-07-18 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 2.3875ms
2025-07-18 12:09:10 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/Import - - -
2025-07-18 12:09:10 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/Import - 200 - text/html;+charset=utf-8 8.5861ms
2025-07-18 12:09:10 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=sWi7u2qYM9TadL2pHt69tQ - 101 - - 1512.2212ms
2025-07-18 12:09:10 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/js/WorkOrderTreeView.js - - -
2025-07-18 12:09:10 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/js/WorkOrderTreeView.js - 304 - text/javascript 2.9271ms
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.6193ms
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 3.5257ms
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/importProgress/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/importProgress/negotiate?negotiateVersion=1 - 200 316 application/json 0.7484ms
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=YFrYA5QFpHiaqxSLi201Aw - - -
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.9450ms
2025-07-18 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/importProgress?id=Kl9b3ifnFedISTaDhQ8DRg - - -
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/ - - -
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/ - 200 - text/html;+charset=utf-8 4.3876ms
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=YFrYA5QFpHiaqxSLi201Aw - 101 - - 3807.0253ms
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/importProgress?id=Kl9b3ifnFedISTaDhQ8DRg - 101 - - 3787.8526ms
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.6211ms
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.6409ms
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=zkHSzf0nTmVzMBtu52JtJQ - - -
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:15 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.7965ms
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/ - - -
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/ - 200 - text/html;+charset=utf-8 4.2516ms
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=zkHSzf0nTmVzMBtu52JtJQ - 101 - - 10964.3256ms
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.4695ms
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.2923ms
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=csaAxDAv9X2PLBYwevrFaw - - -
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:26 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.4211ms
2025-07-18 12:09:27 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Sorting - - -
2025-07-18 12:09:27 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=csaAxDAv9X2PLBYwevrFaw - 101 - - 1013.3435ms
2025-07-18 12:09:27 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Sorting - 200 - text/html;+charset=utf-8 25.5751ms
2025-07-18 12:09:27 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:27 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-18 12:09:27 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.8327ms
2025-07-18 12:09:27 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.0169ms
2025-07-18 12:09:28 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=4Ox-0cXWSwvw8SOgqOgivw - - -
2025-07-18 12:09:28 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:28 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.8362ms
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Cnc - - -
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Cnc - 200 - text/html;+charset=utf-8 13.5755ms
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=4Ox-0cXWSwvw8SOgqOgivw - 101 - - 1206.6673ms
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.5484ms
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.8120ms
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.6100ms
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=OJO4F7f2jZ55q_T7AufZTA - - -
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=UvGTlslqiRuXOJr9ANrJlg - - -
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:29 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.2660ms
2025-07-18 12:09:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/RackConfiguration - - -
2025-07-18 12:09:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/RackConfiguration - 200 - text/html;+charset=utf-8 69.0311ms
2025-07-18 12:09:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=OJO4F7f2jZ55q_T7AufZTA - 101 - - 1215.4378ms
2025-07-18 12:09:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=UvGTlslqiRuXOJr9ANrJlg - 101 - - 1198.4678ms
2025-07-18 12:09:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-18 12:09:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.2326ms
2025-07-18 12:09:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 1.5074ms
2025-07-18 12:09:31 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=A1_zfFJ2ptZkam1Ncx0F4w - - -
2025-07-18 12:09:31 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:31 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.3638ms
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/NewImportPreview - - -
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/NewImportPreview - 200 - text/html;+charset=utf-8 4.8096ms
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=A1_zfFJ2ptZkam1Ncx0F4w - 101 - - 3642.9296ms
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/importProgress/negotiate?negotiateVersion=1 - - 0
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/importProgress/negotiate?negotiateVersion=1 - 200 316 application/json 0.3702ms
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.4162ms
2025-07-18 12:09:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 1.8373ms
2025-07-18 12:09:35 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=kCNpa3IGrORB3YtoNsREVw - - -
2025-07-18 12:09:35 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:09:35 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.4324ms
2025-07-18 12:09:35 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/importProgress?id=h3OIUXDZwJA3auHE7b8Vww - - -
2025-07-18 12:09:43 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/admin/newimport/upload - multipart/form-data;+boundary=----WebKitFormBoundaryonYnrRQ9c1RMrn9s 21889236
2025-07-18 12:09:43 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Process path: C:\ShopBoss-Testing\ShopBoss.Web.exe
2025-07-18 12:09:43 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Using process directory: C:\ShopBoss-Testing
2025-07-18 12:09:43 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer path resolved to: C:\ShopBoss-Testing\tools\importer\bin\Release\net8.0\win-x86\Importer.exe
2025-07-18 12:09:43 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer executable exists: True
2025-07-18 12:09:43 info: ShopBoss.Web.Controllers.NewImportController[0]
      Phase I2: New import file uploaded successfully: MicrovellumWorkOrder.sdf (Session: c55d96e3-39b2-48c5-8efb-23336b34ec96)
2025-07-18 12:09:43 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/admin/newimport/upload - 200 - application/json;+charset=utf-8 119.1016ms
2025-07-18 12:09:43 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/admin/newimport/start - application/json 52
2025-07-18 12:09:43 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Process path: C:\ShopBoss-Testing\ShopBoss.Web.exe
2025-07-18 12:09:43 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Using process directory: C:\ShopBoss-Testing
2025-07-18 12:09:43 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer path resolved to: C:\ShopBoss-Testing\tools\importer\bin\Release\net8.0\win-x86\Importer.exe
2025-07-18 12:09:43 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer executable exists: True
2025-07-18 12:09:43 info: ShopBoss.Web.Controllers.NewImportController[0]
      Phase I2: New import started for session c55d96e3-39b2-48c5-8efb-23336b34ec96
2025-07-18 12:09:43 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/admin/newimport/start - 200 - application/json;+charset=utf-8 9.9541ms
2025-07-18 12:10:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/.well-known/appspecific/com.chrome.devtools.json - - -
2025-07-18 12:10:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/.well-known/appspecific/com.chrome.devtools.json - 404 0 - 0.8546ms
2025-07-18 12:10:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[16]
      Request reached the end of the middleware pipeline without being handled by application code. Request path: GET http://localhost:5000/.well-known/appspecific/com.chrome.devtools.json, Response status code: 404
2025-07-18 12:10:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/lib/signalr/dist/browser/signalr.js.map - - -
2025-07-18 12:10:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/lib/signalr/dist/browser/signalr.js.map - 404 0 - 0.3764ms
2025-07-18 12:10:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[16]
      Request reached the end of the middleware pipeline without being handled by application code. Request path: GET http://localhost:5000/lib/signalr/dist/browser/signalr.js.map, Response status code: 404
2025-07-18 12:10:32 info: ShopBoss.Web.Services.ImporterService[0]
      Successfully imported SDF file C:\ShopBoss-Testing\temp\uploads\c55d96e3-39b2-48c5-8efb-23336b34ec96_MicrovellumWorkOrder.sdf in 48.8 seconds
2025-07-18 12:10:32 info: ShopBoss.Web.Services.WorkOrderImportService[0]
      Phase I2: Starting WorkOrder transformation. Raw data - Products: 42, Parts: 495, Hardware: 335
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 warn: ShopBoss.Web.Services.ColumnMappingService[0]
      No mapping found for logical column 'Quantity' in table type 'SUBASSEMBLIES'
2025-07-18 12:10:32 info: ShopBoss.Web.Services.WorkOrderImportService[0]
      Phase I2: WorkOrder transformation completed. Created - Products: 42, Parts: 495, Hardware: 0
2025-07-18 12:10:32 info: ShopBoss.Web.Controllers.NewImportController[0]
      Phase I2: New import completed successfully for session c55d96e3-39b2-48c5-8efb-23336b34ec96
2025-07-18 12:10:32 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/admin/newimport/status?sessionId=c55d96e3-39b2-48c5-8efb-23336b34ec96 - - -
2025-07-18 12:10:32 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Process path: C:\ShopBoss-Testing\ShopBoss.Web.exe
2025-07-18 12:10:32 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Using process directory: C:\ShopBoss-Testing
2025-07-18 12:10:32 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer path resolved to: C:\ShopBoss-Testing\tools\importer\bin\Release\net8.0\win-x86\Importer.exe
2025-07-18 12:10:32 info: ShopBoss.Web.Services.ImporterService[0]
      UPDATED IMPORTER SERVICE: Importer executable exists: True
2025-07-18 12:10:32 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/admin/newimport/status?sessionId=c55d96e3-39b2-48c5-8efb-23336b34ec96 - 200 - application/json;+charset=utf-8 10.7222ms
2025-07-18 12:10:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:10:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.3809ms
2025-07-18 12:11:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:11:34 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 2.0724ms
2025-07-18 12:12:35 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-18 12:12:35 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.4666ms