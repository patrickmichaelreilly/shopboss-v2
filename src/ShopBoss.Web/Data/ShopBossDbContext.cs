using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Data;

public class ShopBossDbContext : DbContext
{
    public ShopBossDbContext(DbContextOptions<ShopBossDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkOrder> WorkOrders { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Part> Parts { get; set; }
    public DbSet<Subassembly> Subassemblies { get; set; }
    public DbSet<Hardware> Hardware { get; set; }
    public DbSet<DetachedProduct> DetachedProducts { get; set; }
    public DbSet<NestSheet> NestSheets { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<ScanHistory> ScanHistory { get; set; }
    public DbSet<StorageRack> StorageRacks { get; set; }
    public DbSet<Bin> Bins { get; set; }
    public DbSet<BackupConfiguration> BackupConfigurations { get; set; }
    public DbSet<BackupStatus> BackupStatuses { get; set; }
    public DbSet<SystemHealthStatus> SystemHealthStatus { get; set; }
    public DbSet<SortingRule> SortingRules { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectAttachment> ProjectAttachments { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<CustomWorkOrder> CustomWorkOrders { get; set; }
    public DbSet<ProjectEvent> ProjectEvents { get; set; }
    public DbSet<TaskBlock> TaskBlocks { get; set; }
    public DbSet<PartLabel> PartLabels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.ImportedDate).IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemNumber).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Qty).IsRequired();
            
            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.Products)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Qty).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            
            // Remove foreign key constraint for ProductId to allow DetachedProduct Parts
            // ProductId can now point to either Product.Id or DetachedProduct.Id
            entity.Property(e => e.ProductId).IsRequired(false);
                
            entity.HasOne(e => e.Subassembly)
                .WithMany(s => s.Parts)
                .HasForeignKey(e => e.SubassemblyId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.NestSheet)
                .WithMany(n => n.Parts)
                .HasForeignKey(e => e.NestSheetId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Bin)
                .WithMany()
                .HasForeignKey(e => e.BinId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Subassembly>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Qty).IsRequired();
            
            entity.HasOne(e => e.Product)
                .WithMany(p => p.Subassemblies)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.ParentSubassembly)
                .WithMany(s => s.ChildSubassemblies)
                .HasForeignKey(e => e.ParentSubassemblyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Hardware>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Qty).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            
            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.Hardware)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Product)
                .WithMany(p => p.Hardware)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DetachedProduct>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemNumber).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Qty).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            
            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.DetachedProducts)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NestSheet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Material).IsRequired();
            entity.Property(e => e.Barcode).IsRequired();
            entity.Property(e => e.CreatedDate).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            
            entity.HasOne(e => e.WorkOrder)
                .WithMany(w => w.NestSheets)
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => new { e.WorkOrderId, e.Barcode }).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Action).IsRequired();
            entity.Property(e => e.EntityType).IsRequired();
            entity.Property(e => e.EntityId).IsRequired();
            entity.Property(e => e.Station).IsRequired();
            entity.Property(e => e.Details).IsRequired();
            
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.WorkOrderId);
        });

        modelBuilder.Entity<ScanHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Barcode).IsRequired();
            entity.Property(e => e.Station).IsRequired();
            entity.Property(e => e.IsSuccessful).IsRequired();
            entity.Property(e => e.Details).IsRequired();
            
            entity.HasOne(e => e.NestSheet)
                .WithMany()
                .HasForeignKey(e => e.NestSheetId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(e => e.WorkOrder)
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Barcode);
            entity.HasIndex(e => e.Station);
        });

        modelBuilder.Entity<StorageRack>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedDate).IsRequired();
            entity.Property(e => e.Rows).IsRequired();
            entity.Property(e => e.Columns).IsRequired();
            
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<Bin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StorageRackId).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.BinLabel);
            entity.Property(e => e.PartsCount).IsRequired();
            
            entity.HasOne(e => e.StorageRack)
                .WithMany(r => r.Bins)
                .HasForeignKey(e => e.StorageRackId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(e => e.WorkOrder)
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.StorageRackId);
            entity.HasIndex(e => new { e.StorageRackId, e.BinLabel }).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.PartId);
            entity.HasIndex(e => e.ProductId);
        });

        modelBuilder.Entity<BackupConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BackupIntervalMinutes).IsRequired();
            entity.Property(e => e.MaxBackupRetention).IsRequired();
            entity.Property(e => e.EnableCompression).IsRequired();
            entity.Property(e => e.BackupDirectoryPath).IsRequired();
            entity.Property(e => e.EnableAutomaticBackups).IsRequired();
            entity.Property(e => e.LastUpdated).IsRequired();
        });

        modelBuilder.Entity<BackupStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedDate).IsRequired();
            entity.Property(e => e.BackupType).IsRequired();
            entity.Property(e => e.FilePath).IsRequired();
            entity.Property(e => e.OriginalSize).IsRequired();
            entity.Property(e => e.BackupSize).IsRequired();
            entity.Property(e => e.IsSuccessful).IsRequired();
            entity.Property(e => e.Duration).IsRequired();
            
            entity.HasIndex(e => e.CreatedDate);
            entity.HasIndex(e => e.BackupType);
            entity.HasIndex(e => e.IsSuccessful);
        });

        modelBuilder.Entity<SystemHealthStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OverallStatus).IsRequired();
            entity.Property(e => e.DatabaseStatus).IsRequired();
            entity.Property(e => e.DiskSpaceStatus).IsRequired();
            entity.Property(e => e.MemoryStatus).IsRequired();
            entity.Property(e => e.ResponseTimeStatus).IsRequired();
            entity.Property(e => e.AvailableDiskSpaceGB).IsRequired();
            entity.Property(e => e.TotalDiskSpaceGB).IsRequired();
            entity.Property(e => e.MemoryUsagePercentage).IsRequired();
            entity.Property(e => e.AverageResponseTimeMs).IsRequired();
            entity.Property(e => e.DatabaseConnectionTimeMs).IsRequired();
            entity.Property(e => e.ActiveWorkOrderCount).IsRequired();
            entity.Property(e => e.TotalPartsCount).IsRequired();
            entity.Property(e => e.LastHealthCheck).IsRequired();
            
            entity.HasIndex(e => e.LastHealthCheck);
            entity.HasIndex(e => e.OverallStatus);
        });

        modelBuilder.Entity<SortingRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Priority).IsRequired();
            entity.Property(e => e.Keywords).IsRequired().HasMaxLength(500);
            entity.Property(e => e.TargetRackType).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedDate).IsRequired();
            
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.IsActive, e.Priority });
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProjectName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ProjectCategory).IsRequired();
            entity.Property(e => e.CreatedDate).IsRequired();
            entity.Property(e => e.IsArchived).IsRequired();
            
            entity.HasIndex(e => e.ProjectId).IsUnique();
            entity.HasIndex(e => e.ProjectName);
            entity.HasIndex(e => e.ProjectCategory);
            entity.HasIndex(e => e.IsArchived);
            entity.HasIndex(e => e.CreatedDate);
            entity.HasIndex(e => e.SmartsheetSheetId);
        });

        modelBuilder.Entity<ProjectAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileSize).IsRequired();
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UploadedDate).IsRequired();
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Attachments)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.Label);
            entity.HasIndex(e => e.UploadedDate);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.HasOne(e => e.Project)
                .WithMany(p => p.WorkOrders)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.PurchaseOrderNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.VendorName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.OrderDate).IsRequired();
            entity.Property(e => e.CreatedDate).IsRequired();
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.PurchaseOrders)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.PurchaseOrderNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);
        });

        modelBuilder.Entity<CustomWorkOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.WorkOrderType).IsRequired();
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedDate).IsRequired();
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.CustomWorkOrders)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.WorkOrderType);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedDate);
        });

        modelBuilder.Entity<ProjectEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.EventDate).IsRequired();
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.DisplayOrder).IsRequired();
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Events)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.ParentBlock)
                .WithMany(tc => tc.Events)
                .HasForeignKey(e => e.ParentBlockId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.EventDate);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.ParentBlockId);
            entity.HasIndex(e => e.DisplayOrder);
            entity.HasIndex(e => new { e.ParentBlockId, e.DisplayOrder });
        });

        modelBuilder.Entity<TaskBlock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.DisplayOrder).IsRequired();
            entity.Property(e => e.IsTemplate).IsRequired();
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.TaskBlocks)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.DisplayOrder);
            entity.HasIndex(e => new { e.ProjectId, e.DisplayOrder });
        });

        modelBuilder.Entity<PartLabel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PartId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.WorkOrderId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.NestSheetId).IsRequired(false).HasMaxLength(100);
            entity.Property(e => e.LabelHtml).IsRequired();
            entity.Property(e => e.ImportedDate).IsRequired();
            
            entity.HasOne(e => e.Part)
                .WithMany()
                .HasForeignKey(e => e.PartId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.WorkOrder)
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.NestSheet)
                .WithMany()
                .HasForeignKey(e => e.NestSheetId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasIndex(e => e.PartId);
            entity.HasIndex(e => e.WorkOrderId);
            entity.HasIndex(e => e.NestSheetId);
            entity.HasIndex(e => new { e.WorkOrderId, e.PartId }).IsUnique();
        });

    }
}
