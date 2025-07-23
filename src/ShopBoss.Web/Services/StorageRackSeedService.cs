using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;

namespace ShopBoss.Web.Services;

public static class StorageRackSeedService
{
    public static async Task SeedDefaultRacksAsync(ShopBossDbContext context)
    {
        // Check if racks already exist
        if (await context.StorageRacks.AnyAsync())
        {
            return; // Already seeded
        }

        var racks = new[]
        {
            new StorageRack
            {
                Name = "Standard Rack A",
                Type = RackType.Standard,
                Description = "Main storage for standard parts",
                Rows = 4,
                Columns = 8,
                Location = "East Wall",
                IsActive = true,
                IsPortable = false
            },
            new StorageRack
            {
                Name = "Standard Rack B",
                Type = RackType.Standard,
                Description = "Secondary storage for standard parts",
                Rows = 4,
                Columns = 8,
                Location = "East Wall",
                IsActive = true,
                IsPortable = false
            },
            new StorageRack
            {
                Name = "Doors & Fronts Rack",
                Type = RackType.DoorsAndDrawerFronts,
                Description = "Specialized storage for doors and drawer fronts",
                Rows = 3,
                Columns = 6,
                Location = "North Wall",
                IsActive = true,
                IsPortable = false
            },
            new StorageRack
            {
                Name = "Adjustable Shelves Rack",
                Type = RackType.AdjustableShelves,
                Description = "Storage for adjustable shelves",
                Rows = 2,
                Columns = 10,
                Location = "South Wall",
                IsActive = true,
                IsPortable = false
            },
            new StorageRack
            {
                Name = "Mobile Cart 1",
                Type = RackType.Cart,
                Description = "Portable storage cart",
                Rows = 3,
                Columns = 4,
                Location = "Mobile",
                IsActive = true,
                IsPortable = true
            }
        };

        context.StorageRacks.AddRange(racks);
        await context.SaveChangesAsync();

        // Create bins for each rack
        foreach (var rack in racks)
        {
            var bins = new List<Bin>();
            for (int row = 1; row <= rack.Rows; row++)
            {
                for (int col = 1; col <= rack.Columns; col++)
                {
                    bins.Add(new Bin
                    {
                        StorageRackId = rack.Id,
                        Row = row,
                        Column = col,
                        Status = BinStatus.Empty,
                        MaxCapacity = 50 // Orphaned property - kept for database compatibility
                    });
                }
            }
            context.Bins.AddRange(bins);
        }

        await context.SaveChangesAsync();
    }
}