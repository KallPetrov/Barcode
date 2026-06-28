using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services;

public record OperatorActionHistoryItem(Guid Id, string Action, string? Details, string? UserName, DateTime CreatedAt);

public class OperatorActionHistoryService(AppDbContext db)
{
    public async Task<IReadOnlyList<OperatorActionHistoryItem>> ListAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.AuditLogs
            .Where(a => a.TenantId == tenantId && (a.Action.Contains("WORK_TASK") || a.Action.Contains("PICKING") || a.Action.Contains("SESSION") || a.Action.Contains("COUNT") || a.Action.Contains("LOGIN")))
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Include(a => a.User)
            .Select(a => new OperatorActionHistoryItem(a.Id, a.Action, a.Details, a.User != null ? a.User.FullName : null, a.CreatedAt))
            .ToListAsync(ct);
}
