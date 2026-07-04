using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services;

public record NotificationAlertDto(Guid Id, string Title, string Message, string Level, bool IsRead, DateTime CreatedAt);

public class NotificationAlertService(AppDbContext db, AuditService audit)
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
        var threshold = DateTime.UtcNow.AddDays(7);
        var soonToExpire = await db.InventoryStocks
            .Where(s => s.TenantId == tenantId && s.ExpiryDate.HasValue && s.ExpiryDate.Value <= threshold && s.Quantity > 0)
            .Include(s => s.Item)
            .OrderBy(s => s.ExpiryDate)
            .ToListAsync(ct);

        foreach (var stock in soonToExpire)
        {
            var alertKey = $"expiry:{stock.ItemId}:{stock.ExpiryDate:yyyy-MM-dd}";
            var alreadyExists = await db.NotificationAlerts.AnyAsync(a =>
                a.TenantId == tenantId &&
                a.Title == "Expiry alert" &&
                a.Message.Contains(alertKey, System.StringComparison.OrdinalIgnoreCase), ct);

            if (alreadyExists)
                continue;

            await CreateAsync(
                tenantId,
                "Expiry alert",
                $"{alertKey} | Партида за {stock.Item.Sku} изтича на {stock.ExpiryDate:yyyy-MM-dd}",
                AlertLevel.Warning,
                userId,
                ct);
        }

        return await ListAsync(tenantId, ct);
    }
}
