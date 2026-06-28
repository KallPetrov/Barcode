using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services;

public record ReminderDto(Guid Id, string Title, string? Message, Guid? UserId, string? UserName, Guid? RelatedEntityId, string? RelatedEntityType, DateTime DueAt, bool IsCompleted, DateTime CreatedAt, DateTime? CompletedAt);

public class ReminderService(AppDbContext db, AuditService audit)
{
    public async Task<IReadOnlyList<ReminderDto>> ListAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.Reminders
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.DueAt)
            .Include(r => r.User)
            .Select(r => new ReminderDto(r.Id, r.Title, r.Message, r.UserId, r.User != null ? r.User.FullName : null, r.RelatedEntityId, r.RelatedEntityType, r.DueAt, r.IsCompleted, r.CreatedAt, r.CompletedAt))
            .ToListAsync(ct);

    public async Task<ReminderDto> CreateAsync(Guid tenantId, string title, string? message, Guid? userId, Guid? relatedEntityId, string? relatedEntityType, DateTime dueAt, Guid actorId, CancellationToken ct = default)
    {
        var reminder = new Reminder
        {
            TenantId = tenantId,
            Title = title.Trim(),
            Message = message?.Trim(),
            UserId = userId,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            DueAt = dueAt,
            IsCompleted = false
        };

        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "REMINDER_CREATED", actorId, null, "Reminder", reminder.Id.ToString(), title, null, ct);
        return (await ListAsync(tenantId, ct)).First(r => r.Id == reminder.Id);
    }

    public async Task CompleteAsync(Guid tenantId, Guid id, Guid actorId, CancellationToken ct = default)
    {
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Напомнянето не беше намерено.");

        reminder.IsCompleted = true;
        reminder.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "REMINDER_COMPLETED", actorId, null, "Reminder", reminder.Id.ToString(), reminder.Title, null, ct);
    }
}
