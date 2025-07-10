C:\ShopBoss-Testing>shopboss.web.exe
2025-07-10 12:59:00 info: ShopBoss.Web.Services.BackupBackgroundService[0]
      BackupBackgroundService started
2025-07-10 12:59:00 warn: Microsoft.EntityFrameworkCore.Query[10103]
      The query uses the 'First'/'FirstOrDefault' operator without 'OrderBy' and filter operators. This may lead to unpredictable results.
2025-07-10 12:59:00 info: ShopBoss.Web.Services.HealthMonitoringService[0]
      HealthMonitoringService started
2025-07-10 12:59:01 warn: Microsoft.AspNetCore.Server.Kestrel[0]
      Overriding address(es) 'http://0.0.0.0:5000'. Binding to endpoints defined via IConfiguration and/or UseKestrel() instead.
2025-07-10 12:59:01 info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
2025-07-10 12:59:01 info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
2025-07-10 12:59:01 info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
2025-07-10 12:59:01 info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\ShopBoss-Testing
2025-07-10 12:59:01 warn: Microsoft.EntityFrameworkCore.Query[10103]
      The query uses the 'First'/'FirstOrDefault' operator without 'OrderBy' and filter operators. This may lead to unpredictable results.
2025-07-10 12:59:02 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/ - - -
2025-07-10 12:59:02 warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3]
      Failed to determine the https port for redirect.
2025-07-10 12:59:02 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/ - 200 - text/html;+charset=utf-8 137.9305ms
2025-07-10 12:59:02 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/js/universal-scanner.js - - -
2025-07-10 12:59:02 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/js/universal-scanner.js - 304 - text/javascript 3.4130ms
2025-07-10 12:59:02 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-10 12:59:02 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 12:59:02 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 6.6332ms
2025-07-10 12:59:02 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 47.9828ms
2025-07-10 12:59:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=jPKrmVtQ_5xkU6wZvnr6VQ - - -
2025-07-10 12:59:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 12:59:03 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 25.8890ms
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Admin/SetActiveWorkOrder - application/x-www-form-urlencoded 222
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Admin/SetActiveWorkOrder - 302 0 - 54.1170ms
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/ - - -
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/ - 200 - text/html;+charset=utf-8 23.3682ms
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=jPKrmVtQ_5xkU6wZvnr6VQ - 101 - - 1103.1366ms
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.7200ms
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 3.6216ms
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=f9WLb2gk0K3IMfwRAH9zew - - -
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 12:59:04 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 2.4086ms
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/ModifyWorkOrder/2eb652de-d857-4582-9062-59ae72c0baba - - -
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/ModifyWorkOrder/2eb652de-d857-4582-9062-59ae72c0baba - 302 0 - 4.7558ms
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/ModifyWorkOrderUnified/2eb652de-d857-4582-9062-59ae72c0baba - - -
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/ModifyWorkOrderUnified/2eb652de-d857-4582-9062-59ae72c0baba - 200 - text/html;+charset=utf-8 246.9908ms
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=f9WLb2gk0K3IMfwRAH9zew - 101 - - 983.5372ms
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.7027ms
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/api/WorkOrderTreeApi/2eb652de-d857-4582-9062-59ae72c0baba?includeStatus=true - - -
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.8737ms
2025-07-10 12:59:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/api/WorkOrderTreeApi/2eb652de-d857-4582-9062-59ae72c0baba?includeStatus=true - 200 - application/json;+charset=utf-8 27.1791ms
2025-07-10 12:59:06 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=bE1KskeeVKFQEk_YcXmdzA - - -
2025-07-10 12:59:06 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 12:59:06 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 2.2740ms
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Cnc - - -
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Cnc - 200 - text/html;+charset=utf-8 39.9933ms
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=bE1KskeeVKFQEk_YcXmdzA - 101 - - 2792.1392ms
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.7467ms
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.4480ms
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-10 12:59:08 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.5651ms
2025-07-10 12:59:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=il2tR_ZtRZlf8UHROxwmQQ - - -
2025-07-10 12:59:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=yk_D2jLAdeoaQFv1T2MPGg - - -
2025-07-10 12:59:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 12:59:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.6534ms
2025-07-10 12:59:23 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Cnc/ProcessNestSheet - application/json 20
2025-07-10 12:59:23 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Cnc/ProcessNestSheet - application/json 20
2025-07-10 12:59:24 info: ShopBoss.Web.Services.AuditTrailService[0]
      Scan history logged: [empty] at CNC - Failed
2025-07-10 12:59:24 info: ShopBoss.Web.Services.AuditTrailService[0]
      Scan history logged: [empty] at CNC - Failed
2025-07-10 12:59:24 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Cnc/ProcessNestSheet - 200 - application/json;+charset=utf-8 34.2298ms
2025-07-10 12:59:24 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Cnc/ProcessNestSheet - 200 - application/json;+charset=utf-8 34.2296ms
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Cnc - - -
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Cnc - 200 - text/html;+charset=utf-8 4.8289ms
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=yk_D2jLAdeoaQFv1T2MPGg - 101 - - 21670.5644ms
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=il2tR_ZtRZlf8UHROxwmQQ - 101 - - 21685.7616ms
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.6164ms
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.5719ms
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-10 12:59:30 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 1.5587ms
2025-07-10 12:59:31 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=jcP6J8LegX4BuIgBgnxfKg - - -
2025-07-10 12:59:31 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=F-QrQg8cs2GFGY17sW3hFQ - - -
2025-07-10 12:59:31 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - - -
2025-07-10 12:59:31 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetHealthMetrics - 200 - application/json;+charset=utf-8 1.5220ms
2025-07-10 12:59:37 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Cnc/ProcessNestSheet - application/json 20
2025-07-10 12:59:37 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Cnc/ProcessNestSheet - application/json 20
2025-07-10 12:59:37 info: ShopBoss.Web.Services.AuditTrailService[0]
      Scan history logged: [empty] at CNC - Failed
2025-07-10 12:59:37 info: ShopBoss.Web.Services.AuditTrailService[0]
      Scan history logged: [empty] at CNC - Failed
2025-07-10 12:59:37 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Cnc/ProcessNestSheet - 200 - application/json;+charset=utf-8 7.9851ms
2025-07-10 12:59:37 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Cnc/ProcessNestSheet - 200 - application/json;+charset=utf-8 7.9847ms