using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace CALAC.Infrastructure.Services;

public record NotificationAlertDto(Guid Id, string Title, string Message, string Level, bool IsRead, DateTime CreatedAt);

public class NotificationAlertService(AppDbContext db, AuditService audit, IServiceProvider serviceProvider)
{
    public async Task<IReadOnlyList<NotificationAlertDto>> ListAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.NotificationAlerts
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new NotificationAlertDto(a.Id, a.Title, a.Message, a.Level.ToString(), a.IsRead, a.CreatedAt))
            .ToListAsync(ct);

    public async Task<NotificationAlertDto> CreateAsync(Guid tenantId, string title, string message, AlertLevel level, Guid userId, CancellationToken ct = default)
    {
        var alert = new NotificationAlert
        {
            TenantId = tenantId,
            Title = title,
            Message = message,
            Level = level,
            IsRead = false
        };

        db.NotificationAlerts.Add(alert);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ALERT_CREATED", userId, null, "NotificationAlert", alert.Id.ToString(), message, null, ct);

        var hubContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Microsoft.AspNetCore.SignalR.IHubContext<CALAC.Infrastructure.Hubs.WarehouseHub>>(serviceProvider);
        if (hubContext?.Clients != null)
        {
            await hubContext.Clients.Group(tenantId.ToString()).SendAsync("WarehouseEvent", new { type = "ALERT_CREATED", title, level = level.ToString() }, ct);
        }

        return new NotificationAlertDto(alert.Id, alert.Title, alert.Message, alert.Level.ToString(), alert.IsRead, alert.CreatedAt);
    }

    public async Task MarkReadAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var alert = await db.NotificationAlerts.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Известието не беше намерено.");

        alert.IsRead = true;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ALERT_READ", userId, null, "NotificationAlert", alert.Id.ToString(), "Alert marked as read", null, ct);
    }

    public async Task<IReadOnlyList<NotificationAlertDto>> CreateExpiryAlertsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var thresholds = new[] { 90, 30, 15, 7, 0 };

        foreach (var days in thresholds)
        {
            var thresholdDate = now.AddDays(days);
            var stocks = await db.InventoryStocks
                .Where(s => s.TenantId == tenantId && s.ExpiryDate.HasValue && s.ExpiryDate.Value <= thresholdDate && s.Quantity > 0)
                .Include(s => s.Item)
                .ToListAsync(ct);

            foreach (var stock in stocks)
            {
                var isExpired = stock.ExpiryDate!.Value <= now;
                if (isExpired && stock.Status != StockStatus.Expired)
                {
                    stock.Status = StockStatus.Expired;
                    await audit.LogAsync(tenantId, "STOCK_EXPIRED", userId, null, "InventoryStock", stock.Id.ToString(), $"Auto-blocked due to expiry: {stock.ExpiryDate:yyyy-MM-dd}", null, ct);
                }

                var title = isExpired ? "Изтекъл срок на годност" : $"Наближаващ срок ({days} дни)";
                var level = isExpired ? AlertLevel.Critical : (days <= 7 ? AlertLevel.Warning : AlertLevel.Info);
                var alertKey = $"expiry:{stock.ItemId}:{stock.ExpiryDate:yyyy-MM-dd}:{days}";

                var alreadyExists = await db.NotificationAlerts.AnyAsync(a =>
                    a.TenantId == tenantId &&
                    a.Title == title &&
                    a.Message.Contains(alertKey, StringComparison.OrdinalIgnoreCase), ct);

                if (alreadyExists) continue;

                await CreateAsync(
                    tenantId,
                    title,
                    $"{alertKey} | Артикул {stock.Item.Sku} (Партида: {stock.BatchNumber}) изтича на {stock.ExpiryDate:yyyy-MM-dd}",
                    level,
                    userId,
                    ct);
            }
        }

        await db.SaveChangesAsync(ct);
        return await ListAsync(tenantId, ct);
    }
}
