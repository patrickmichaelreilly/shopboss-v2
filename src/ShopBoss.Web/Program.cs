using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure for Windows Service hosting
builder.Host.UseWindowsService();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Add HTTP context accessor for services that need access to the current HTTP context
builder.Services.AddHttpContextAccessor();

// Configure Data Protection for Windows Service compatibility
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")))
    .SetApplicationName("ShopBoss");

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add Entity Framework
builder.Services.AddDbContext<ShopBossDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=shopboss.db;Cache=Shared;Foreign Keys=False";
    options.UseSqlite(connectionString);
    
    // Suppress pending model changes warning during status migration
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    
    // Enable sensitive data logging for debugging Entity Framework conflicts
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Warning);
    }
});

// Add custom services
builder.Services.AddScoped<ImporterService>();
builder.Services.AddScoped<FastImportService>();
builder.Services.AddScoped<ColumnMappingService>();
builder.Services.AddScoped<AuditTrailService>();
builder.Services.AddScoped<SortingRuleService>();
builder.Services.AddScoped<PartFilteringService>();
builder.Services.AddScoped<ShippingService>();
builder.Services.AddScoped<HardwareGroupingService>();
builder.Services.AddScoped<WorkOrderService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<SystemHealthMonitor>();
builder.Services.AddScoped<WorkOrderImportService>();
builder.Services.AddScoped<WorkOrderDeletionService>();
// UniversalScannerService removed - using event-based architecture instead

// Add background services
builder.Services.AddHostedService<BackupBackgroundService>();
builder.Services.AddHostedService<HealthMonitoringService>();

var app = builder.Build();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ShopBossDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    // Create database schema if it doesn't exist
    // Using EnsureCreated instead of migrations for immediate table creation
    await context.Database.EnsureCreatedAsync();
}

// Seed default storage racks (separate scope to ensure migration is committed)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ShopBossDbContext>();
    await StorageRackSeedService.SeedDefaultRacksAsync(context);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Admin/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");

app.MapHub<ImportProgressHub>("/importProgress");
app.MapHub<StatusHub>("/hubs/status");

app.Run();
