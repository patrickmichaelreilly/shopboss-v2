2025-07-28 12:08:55 fail: Microsoft.AspNetCore.Antiforgery.DefaultAntiforgery[7]
      An exception was thrown while deserializing the token.
      Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException: The antiforgery token could not be decrypted.
       ---> System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Antiforgery.DefaultAntiforgeryTokenSerializer.Deserialize(String serializedToken)
         --- End of inner exception stack trace ---
         at Microsoft.AspNetCore.Antiforgery.DefaultAntiforgeryTokenSerializer.Deserialize(String serializedToken)
         at Microsoft.AspNetCore.Antiforgery.DefaultAntiforgery.GetCookieTokenDoesNotThrow(HttpContext httpContext)
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/ - 200 - text/html;+charset=utf-8 229.9894ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/lib/bootstrap/dist/css/bootstrap.min.css - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/ShopBoss.Web.styles.css?v=1dmAzoZTsGn5at846RfjQEtJ9Lh1ptPcIxwkp54RAoY - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/lib/bootstrap/dist/css/bootstrap.min.css - 304 - text/css 4.0716ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/ShopBoss.Web.styles.css?v=1dmAzoZTsGn5at846RfjQEtJ9Lh1ptPcIxwkp54RAoY - 304 - text/css 0.9911ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/css/site.css?v=3rLTEZWJ2exYxs4OoXC2_FW_kZGNtAIKMpsrY2WpYkw - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/css/site.css?v=3rLTEZWJ2exYxs4OoXC2_FW_kZGNtAIKMpsrY2WpYkw - 304 - text/css 0.2108ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Dog.svg - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Dog.svg - 304 - image/svg+xml 0.1501ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/lib/jquery/dist/jquery.min.js - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/lib/jquery/dist/jquery.min.js - 304 - text/javascript 0.1897ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/lib/bootstrap/dist/js/bootstrap.bundle.min.js - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/js/site.js?v=hRQyftXiu1lLX2P9Ly9xa4gHJgLeR1uGN5qegUobtGo - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/js/site.js?v=hRQyftXiu1lLX2P9Ly9xa4gHJgLeR1uGN5qegUobtGo - 304 - text/javascript 0.4283ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/lib/bootstrap/dist/js/bootstrap.bundle.min.js - 304 - text/javascript 0.4285ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/lib/signalr/dist/browser/signalr.js - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/lib/signalr/dist/browser/signalr.js - 304 - text/javascript 0.2993ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:08:55 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:55 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 9.6730ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 48.2719ms
2025-07-28 12:08:55 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=ZOtdRlMwNit4ZtFnHzB9KQ - - -
2025-07-28 12:08:55 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Cnc - - -
2025-07-28 12:08:58 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Cnc - 200 - text/html;+charset=utf-8 28.7192ms
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=ZOtdRlMwNit4ZtFnHzB9KQ - 101 - - 2495.2714ms
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/js/universal-scanner.js - - -
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/js/universal-scanner.js - 304 - text/javascript 0.5481ms
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-28 12:08:58 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:58 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 3.3088ms
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 3.5503ms
2025-07-28 12:08:58 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 12.1654ms
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=oAh0Qs5VWKKDO2pmspevBQ - - -
2025-07-28 12:08:58 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:58 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=QM-EhkhzdQoC1Tm8nf9DUQ - - -
2025-07-28 12:08:58 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:59 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/Admin/SetActiveWorkOrderJson - application/x-www-form-urlencoded 39
2025-07-28 12:08:59 warn: Microsoft.AspNetCore.Session.SessionMiddleware[7]
      Error unprotecting the session cookie.
      System.Security.Cryptography.CryptographicException: The key {ea8e3c5c-6c26-4c29-b054-d23c514ec6e1} was not found in the key ring. For more information go to https://aka.ms/aspnet/dataprotectionwarning
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore(Byte[] protectedData, Boolean allowOperationsOnRevokedKeys, UnprotectStatus& status)
         at Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.Unprotect(Byte[] protectedData)
         at Microsoft.AspNetCore.Session.CookieProtection.Unprotect(IDataProtector protector, String protectedText, ILogger logger)
2025-07-28 12:08:59 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/Admin/SetActiveWorkOrderJson - 200 - application/json;+charset=utf-8 42.9279ms
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Cnc - - -
2025-07-28 12:09:00 fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (1ms) [Parameters=[@__activeWorkOrderId_0='?' (Size = 36)], CommandType='Text', CommandTimeout='30']
      SELECT "n"."Id", "n"."Barcode", "n"."CreatedDate", "n"."Length", "n"."Material", "n"."Name", "n"."Status", "n"."StatusUpdatedDate", "n"."Thickness", "n"."Width", "n"."WorkOrderId", "p"."Id", "p"."BinId", "p"."Category", "p"."DetachedProductId", "p"."EdgebandingBottom", "p"."EdgebandingLeft", "p"."EdgebandingRight", "p"."EdgebandingTop", "p"."Length", "p"."Location", "p"."Material", "p"."Name", "p"."NestSheetId", "p"."ProductId", "p"."Qty", "p"."Status", "p"."StatusUpdatedDate", "p"."SubassemblyId", "p"."Thickness", "p"."Width"
      FROM "NestSheets" AS "n"
      LEFT JOIN "Parts" AS "p" ON "n"."Id" = "p"."NestSheetId"
      WHERE "n"."WorkOrderId" = @__activeWorkOrderId_0 AND "n"."Name" <> 'Default Nest Sheet'
      ORDER BY "n"."Name", "n"."Id"
2025-07-28 12:09:00 fail: Microsoft.EntityFrameworkCore.Query[10100]
      An exception occurred while iterating over the results of a query for context type 'ShopBoss.Web.Data.ShopBossDbContext'.
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: p.BinId'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: p.BinId'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
2025-07-28 12:09:00 fail: ShopBoss.Web.Controllers.CncController[0]
      Error retrieving nest sheets for CNC station
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: p.BinId'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
         at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
         at ShopBoss.Web.Controllers.CncController.Index()
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Cnc - 200 - text/html;+charset=utf-8 45.1236ms
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=oAh0Qs5VWKKDO2pmspevBQ - 101 - - 1818.6410ms
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=QM-EhkhzdQoC1Tm8nf9DUQ - 101 - - 1803.3473ms
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.8825ms
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 1.1597ms
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.4755ms
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/favicon.ico - - -
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/favicon.ico - 304 - image/x-icon 0.5127ms
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=IZykN7mLQSqCEeW6oreS7g - - -
2025-07-28 12:09:00 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=pcRZWPMQq_MZRHEDWJiiFw - - -
2025-07-28 12:09:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Sorting - - -
2025-07-28 12:09:05 fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (0ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT "s"."Id", "s"."CreatedDate", "s"."Description", "s"."Height", "s"."IsActive", "s"."IsPortable", "s"."LastModifiedDate", "s"."Length", "s"."Location", "s"."Name", "s"."Type", "s"."Width", "b"."Id", "b"."AssignedDate", "b"."BinLabel", "b"."Contents", "b"."LastUpdatedDate", "b"."Notes", "b"."PartId", "b"."PartsCount", "b"."ProductId", "b"."Status", "b"."StorageRackId", "b"."WorkOrderId"
      FROM "StorageRacks" AS "s"
      LEFT JOIN "Bins" AS "b" ON "s"."Id" = "b"."StorageRackId"
      WHERE "s"."IsActive"
      ORDER BY "s"."Type", "s"."Name", "s"."Id"
2025-07-28 12:09:05 fail: Microsoft.EntityFrameworkCore.Query[10100]
      An exception occurred while iterating over the results of a query for context type 'ShopBoss.Web.Data.ShopBossDbContext'.
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: b.BinLabel'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: b.BinLabel'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
2025-07-28 12:09:05 fail: ShopBoss.Web.Controllers.SortingController[0]
      Error retrieving sorting station data
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: b.BinLabel'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
         at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
         at ShopBoss.Web.Services.SortingRuleService.GetActiveRacksAsync()
         at ShopBoss.Web.Controllers.SortingController.Index(String rackId)
2025-07-28 12:09:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Sorting - 200 - text/html;+charset=utf-8 34.3895ms
2025-07-28 12:09:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:09:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-28 12:09:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.5247ms
2025-07-28 12:09:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.8041ms
2025-07-28 12:09:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=pcRZWPMQq_MZRHEDWJiiFw - 101 - - 5111.5295ms
2025-07-28 12:09:05 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=IZykN7mLQSqCEeW6oreS7g - 101 - - 5132.7862ms
2025-07-28 12:09:06 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=Vo4MKY60IEOB3AMo25XzvQ - - -
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Assembly - - -
2025-07-28 12:09:09 fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (0ms) [Parameters=[@__workOrderId_0='?' (Size = 36)], CommandType='Text', CommandTimeout='30']
      SELECT "p"."Id", "p"."ItemNumber", "p"."Length", "p"."Name", "p"."Qty", "p"."Status", "p"."StatusUpdatedDate", "p"."Width", "p"."WorkOrderId", "p0"."Id", "p0"."BinId", "p0"."Category", "p0"."DetachedProductId", "p0"."EdgebandingBottom", "p0"."EdgebandingLeft", "p0"."EdgebandingRight", "p0"."EdgebandingTop", "p0"."Length", "p0"."Location", "p0"."Material", "p0"."Name", "p0"."NestSheetId", "p0"."ProductId", "p0"."Qty", "p0"."Status", "p0"."StatusUpdatedDate", "p0"."SubassemblyId", "p0"."Thickness", "p0"."Width"
      FROM "Products" AS "p"
      LEFT JOIN "Parts" AS "p0" ON "p"."Id" = "p0"."ProductId"
      WHERE "p"."WorkOrderId" = @__workOrderId_0
      ORDER BY "p"."Id"
2025-07-28 12:09:09 fail: Microsoft.EntityFrameworkCore.Query[10100]
      An exception occurred while iterating over the results of a query for context type 'ShopBoss.Web.Data.ShopBossDbContext'.
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: p0.BinId'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: p0.BinId'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
2025-07-28 12:09:09 fail: ShopBoss.Web.Services.SortingRuleService[0]
      Error checking assembly readiness for work order e08d25c8-0ab4-4019-8f66-4b119e70da79
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: p0.BinId'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
         at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
         at ShopBoss.Web.Services.SortingRuleService.CheckAssemblyReadinessAsync(String workOrderId)
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Assembly - 200 - text/html;+charset=utf-8 109.1238ms
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.4868ms
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.4985ms
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 1.8447ms
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=Vo4MKY60IEOB3AMo25XzvQ - 101 - - 3076.7117ms
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=XDuKJCOd8v-pSUaj0uUrUg - - -
2025-07-28 12:09:09 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=36jBTXUloYzwNkr7UANTeA - - -
2025-07-28 12:09:10 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Shipping - - -
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Shipping - 200 - text/html;+charset=utf-8 42.7884ms
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=XDuKJCOd8v-pSUaj0uUrUg - 101 - - 1498.1142ms
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=36jBTXUloYzwNkr7UANTeA - 101 - - 1483.1221ms
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.0569ms
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.3177ms
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.4003ms
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=hJ19zp_nT3p0jnLVyh0z1A - - -
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=YcSt9_WpSleQufEGRwqahw - - -
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Shipping/GetShippingProgress - - -
2025-07-28 12:09:11 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Shipping/GetShippingProgress - 200 - application/json;+charset=utf-8 20.0725ms
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/RackConfiguration - - -
2025-07-28 12:09:12 fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (0ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT "s"."Id", "s"."CreatedDate", "s"."Description", "s"."Height", "s"."IsActive", "s"."IsPortable", "s"."LastModifiedDate", "s"."Length", "s"."Location", "s"."Name", "s"."Type", "s"."Width", "b"."Id", "b"."AssignedDate", "b"."BinLabel", "b"."Contents", "b"."LastUpdatedDate", "b"."Notes", "b"."PartId", "b"."PartsCount", "b"."ProductId", "b"."Status", "b"."StorageRackId", "b"."WorkOrderId"
      FROM "StorageRacks" AS "s"
      LEFT JOIN "Bins" AS "b" ON "s"."Id" = "b"."StorageRackId"
      ORDER BY "s"."Name", "s"."Id"
2025-07-28 12:09:12 fail: Microsoft.EntityFrameworkCore.Query[10100]
      An exception occurred while iterating over the results of a query for context type 'ShopBoss.Web.Data.ShopBossDbContext'.
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: b.BinLabel'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: b.BinLabel'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
2025-07-28 12:09:12 fail: ShopBoss.Web.Controllers.AdminController[0]
      Error loading rack configuration
      Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 1: 'no such column: b.BinLabel'.
         at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
         at Microsoft.Data.Sqlite.SqliteCommand.PrepareAndEnumerateStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteCommand.GetStatements()+MoveNext()
         at Microsoft.Data.Sqlite.SqliteDataReader.NextResult()
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior behavior)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
         at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync[TSource](IQueryable`1 source, CancellationToken cancellationToken)
         at ShopBoss.Web.Controllers.AdminController.RackConfiguration()
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/RackConfiguration - 200 - text/html;+charset=utf-8 30.4158ms
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - - 0
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - - -
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/hubs/status/negotiate?negotiateVersion=1 - 200 316 application/json 0.4377ms
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/Admin/GetAllWorkOrders - 200 - application/json;+charset=utf-8 2.0467ms
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=hJ19zp_nT3p0jnLVyh0z1A - 101 - - 1277.9470ms
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:5000/hubs/status?id=YcSt9_WpSleQufEGRwqahw - 101 - - 1259.9604ms
2025-07-28 12:09:12 info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:5000/hubs/status?id=U7sHKzrZSC5ssctpUhIziA - - -