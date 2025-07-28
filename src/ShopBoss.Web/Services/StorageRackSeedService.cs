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
                Location = "East Wall",
                IsActive = true,
                IsPortable = false,
                Rows = 4,
                Columns = 8
            },
            new StorageRack
            {
                Name = "Standard Rack B",
                Type = RackType.Standard,
                Description = "Secondary storage for standard parts",
                Location = "East Wall",
                IsActive = true,
                IsPortable = false,
                Rows = 4,
                Columns = 8
            },
            new StorageRack
            {
                Name = "Doors & Fronts Rack",
                Type = RackType.DoorsAndDrawerFronts,
                Description = "Specialized storage for doors and drawer fronts",
                Location = "North Wall",
                IsActive = true,
                IsPortable = false,
                Rows = 3,
                Columns = 6
            },
            new StorageRack
            {
                Name = "Adjustable Shelves Rack",
                Type = RackType.AdjustableShelves,
                Description = "Storage for adjustable shelves",
                Location = "South Wall",
                IsActive = true,
                IsPortable = false,
                Rows = 5,
                Columns = 6
            },
            new StorageRack
            {
                Name = "Mobile Cart 1",
                Type = RackType.Cart,
                Description = "Portable storage cart",
                Location = "Mobile",
                IsActive = true,
                IsPortable = true,
                Rows = 2,
                Columns = 4
            }
        };

        context.StorageRacks.AddRange(racks);
        await context.SaveChangesAsync();

        // Create bins for each rack based on its Rows and Columns configuration
        foreach (var rack in racks)
        {
            var bins = new List<Bin>();
            
            // Create bins using the rack's specific row/column configuration
            var binLabels = new List<string>();
            for (int row = 1; row <= rack.Rows; row++)
            {
                for (int col = 1; col <= rack.Columns; col++)
                {
                    binLabels.Add($"{(char)('A' + row - 1)}{col:D2}");
                }
            }

            foreach (var label in binLabels)
            {
                bins.Add(new Bin
                {
                    StorageRackId = rack.Id,
                    Status = BinStatus.Empty,
                    BinLabel = label
                });
            }
            
            context.Bins.AddRange(bins);
        }

        await context.SaveChangesAsync();
    }
}