using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services.Optimization;

public record SlottingRecommendation(Guid ItemId, string Sku, Guid CurrentLocationId, string CurrentLocationCode, Guid SuggestedLocationId, string SuggestedLocationCode, string Reason);

public class SlottingService(AppDbContext db, AuditService audit)
{
    public async Task<List<SlottingRecommendation>> GetRecommendationsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var recommendations = new List<SlottingRecommendation>();

        // 1. Identification of aging FEFO batches in storage that should move to picking zones
        // Assumptions:
        // - Picking zones have "PICK" in their zone/code name
        // - Aging means < 60 days to expiry but in storage

        var pickingLocations = await db.Locations
            .Where(l => l.TenantId == tenantId && (l.Zone != null && l.Zone.Contains("PICK") || l.Code.StartsWith("P-")))
            .ToListAsync(ct);

        var pickingLocationIds = pickingLocations.Select(l => l.Id).ToList();

        var agingStorageStock = await db.InventoryStocks
            .Include(s => s.Item)
            .Include(s => s.Location)
            .Where(s => s.TenantId == tenantId &&
                        s.Quantity > 0 &&
                        s.ExpiryDate.HasValue &&
                        s.ExpiryDate < DateTime.UtcNow.AddDays(60) &&
                        !pickingLocationIds.Contains(s.LocationId))
            .ToListAsync(ct);

        foreach (var stock in agingStorageStock)
        {
            var target = pickingLocations.FirstOrDefault(l => !db.InventoryStocks.Any(s => s.LocationId == l.Id && s.ItemId != stock.ItemId));
            if (target != null)
            {
                recommendations.Add(new SlottingRecommendation(
                    stock.ItemId,
                    stock.Item.Sku,
                    stock.LocationId,
                    stock.Location.Code,
                    target.Id,
                    target.Code,
                    "Aging FEFO batch in storage zone. Move to picking zone for faster dispatch."));
            }
        }

        return recommendations;
    }
}
