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
                IsPortable = false
            },
            new StorageRack
            {
                Name = "Standard Rack B",
                Type = RackType.Standard,
                Description = "Secondary storage for standard parts",
                Location = "East Wall",
                IsActive = true,
                IsPortable = false
            },
            new StorageRack
            {
                Name = "Doors & Fronts Rack",
                Type = RackType.DoorsAndDrawerFronts,
                Description = "Specialized storage for doors and drawer fronts",
                Location = "North Wall",
                IsActive = true,
                IsPortable = false
            },
            new StorageRack
            {
                Name = "Adjustable Shelves Rack",
                Type = RackType.AdjustableShelves,
                Description = "Storage for adjustable shelves",
                Location = "South Wall",
                IsActive = true,
                IsPortable = false
            },
            new StorageRack
            {
                Name = "Mobile Cart 1",
                Type = RackType.Cart,
                Description = "Portable storage cart",
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
            
            // Create a standard set of bins with predefined labels
            // For simplicity, create 32 bins (4 rows x 8 columns equivalent)
            var binLabels = new List<string>();
            for (int row = 1; row <= 4; row++)
            {
                for (int col = 1; col <= 8; col++)
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