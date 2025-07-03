using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Hubs;
using ShopBoss.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

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
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=shopboss.db;Cache=Shared"));

// Add custom services
builder.Services.AddScoped<ImporterService>();
builder.Services.AddScoped<ImportDataTransformService>();
builder.Services.AddScoped<ColumnMappingService>();
builder.Services.AddScoped<ImportSelectionService>();
builder.Services.AddScoped<AuditTrailService>();
builder.Services.AddScoped<SortingRuleService>();
builder.Services.AddScoped<PartFilteringService>();
builder.Services.AddScoped<ShippingService>();

var app = builder.Build();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ShopBossDbContext>();
    context.Database.EnsureCreated();
    
    // Seed default storage racks
    await StorageRackSeedService.SeedDefaultRacksAsync(context);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
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
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ImportProgressHub>("/importProgress");
app.MapHub<StatusHub>("/hubs/status");

app.Run();
