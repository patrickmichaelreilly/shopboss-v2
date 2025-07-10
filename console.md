C:\ShopBoss-Testing>shopboss.web.exe
info: Program[0]
      Applying database migrations...
fail: Microsoft.EntityFrameworkCore.Migrations[20410]
      The migration operation 'PRAGMA foreign_keys = 0;
      ' from migration 'AddNestSheetAndPartStatus' cannot be executed in a transaction. If the app is terminated or an unrecoverable error occurs while this operation is being executed then the migration will be left in a partially applied state and would need to be reverted manually before it can be applied again. Create a separate migration that contains just this operation.
fail: Microsoft.EntityFrameworkCore.Migrations[20410]
      The migration operation 'PRAGMA foreign_keys = 0;
      ' from migration 'RequireNestSheetIdWithDefaults' cannot be executed in a transaction. If the app is terminated or an unrecoverable error occurs while this operation is being executed then the migration will be left in a partially applied state and would need to be reverted manually before it can be applied again. Create a separate migration that contains just this operation.
fail: Microsoft.EntityFrameworkCore.Migrations[20410]
      The migration operation 'PRAGMA foreign_keys = 0;
      ' from migration 'AddHardwareProductRelationship' cannot be executed in a transaction. If the app is terminated or an unrecoverable error occurs while this operation is being executed then the migration will be left in a partially applied state and would need to be reverted manually before it can be applied again. Create a separate migration that contains just this operation.
fail: Microsoft.EntityFrameworkCore.Migrations[20410]
      The migration operation 'PRAGMA foreign_keys = 0;
      ' from migration 'RemovePartProductIdForeignKey' cannot be executed in a transaction. If the app is terminated or an unrecoverable error occurs while this operation is being executed then the migration will be left in a partially applied state and would need to be reverted manually before it can be applied again. Create a separate migration that contains just this operation.
info: Program[0]
      Database migrations completed successfully
info: ShopBoss.Web.Services.BackupBackgroundService[0]
      BackupBackgroundService started
warn: Microsoft.EntityFrameworkCore.Query[10103]
      The query uses the 'First'/'FirstOrDefault' operator without 'OrderBy' and filter operators. This may lead to unpredictable results.
info: ShopBoss.Web.Services.BackupBackgroundService[0]
      Starting automatic backup
info: ShopBoss.Web.Services.HealthMonitoringService[0]
      HealthMonitoringService started
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\ShopBoss-Testing
warn: Microsoft.EntityFrameworkCore.Query[10103]
      The query uses the 'First'/'FirstOrDefault' operator without 'OrderBy' and filter operators. This may lead to unpredictable results.
info: ShopBoss.Web.Services.BackupService[0]
      Backup created successfully: C:\ShopBoss-Testing\Backups\shopboss_backup_20250710_124253.db.gz (Type: Automatic)
info: ShopBoss.Web.Services.AuditTrailService[0]
      Audit log created: Backup on Created Automatic backup created: shopboss_backup_20250710_124253.db (18.76 KB) from 
info: ShopBoss.Web.Services.BackupBackgroundService[0]
      Automatic backup completed successfully: C:\ShopBoss-Testing\Backups\shopboss_backup_20250710_124253.db.gz