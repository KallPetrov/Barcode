using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services;

public class KpiReportingService(AppDbContext db)
{
    public async Task<object> GetOverviewAsync(Guid tenantId, CancellationToken ct = default)
    {
        var workTasks = await db.WorkTasks.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var sessions = await db.InventorySessions.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var picks = await db.PickingOrders.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var inventory = await db.InventoryStocks.Where(x => x.TenantId == tenantId).ToListAsync(ct);

        var completedTasks = workTasks.Count(x => x.Status == WorkTaskStatus.Completed);
        var totalTasks = workTasks.Count;
        var completedPickings = picks.Count(x => x.Status == PickingOrderStatus.Completed);
        var totalPickings = picks.Count;
        var pickingAccuracy = totalPickings == 0 ? 0m : Math.Round((decimal)completedPickings / totalPickings * 100m, 2);
        var inventoryTurnover = inventory.Count == 0 ? 0m : Math.Round(inventory.Sum(x => x.Quantity) / inventory.Count, 2);
        var avgCompletionDays = workTasks.Where(x => x.Status == WorkTaskStatus.Completed && x.CompletedAt.HasValue)
            .Select(x => (x.CompletedAt!.Value - x.CreatedAt).Days)
            .DefaultIfEmpty(0)
            .Average();

        var stockLines = await db.PickingStockLines.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var totalOverrideCount = stockLines.Count(x => x.IsOverride);
        var complianceRate = stockLines.Count == 0 ? 100m : Math.Round((decimal)(stockLines.Count - totalOverrideCount) / stockLines.Count * 100m, 2);

        var expiredStockValue = inventory.Where(x => x.Status == StockStatus.Expired).Sum(x => x.Quantity);
        var quarantinedStockValue = inventory.Where(x => x.Status == StockStatus.Quarantined).Sum(x => x.Quantity);

        return new
        {
            totalTasks,
            completedTasks,
            overdueTasks = workTasks.Count(x => x.Status != WorkTaskStatus.Completed && x.DueDate < DateTime.UtcNow),
            activeSessions = sessions.Count(x => x.Status == InventorySessionStatus.InProgress),
            completedSessions = sessions.Count(x => x.Status == InventorySessionStatus.Completed),
            completedPickings,
            totalPickings,
            pickingAccuracy,
            inventoryTurnover,
            averageCompletionDays = Math.Round(avgCompletionDays, 2),
            totalOverrideCount,
            pickingComplianceRate = complianceRate,
            expiredStockQuantity = expiredStockValue,
            quarantinedStockQuantity = quarantinedStockValue
        };
    }
}
