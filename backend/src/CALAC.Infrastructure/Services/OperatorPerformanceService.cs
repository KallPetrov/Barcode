using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services;

public record OperatorPerformanceDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string Period,
    int TasksAssigned,
    int TasksCompleted,
    int TasksOverdue,
    int PickingCompleted,
    int InventorySessionsCompleted,
    decimal EfficiencyRate,
    DateTime CreatedAt);

public class OperatorPerformanceService(AppDbContext db, AuditService audit)
{
    public async Task<IReadOnlyList<OperatorPerformanceDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var snapshots = await db.OperatorPerformanceSnapshots
            .Where(s => s.TenantId == tenantId)
            .Include(s => s.User)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return snapshots.Select(s => new OperatorPerformanceDto(
            s.Id,
            s.UserId,
            s.User.FullName,
            s.Period,
            s.TasksAssigned,
            s.TasksCompleted,
            s.TasksOverdue,
            s.PickingCompleted,
            s.InventorySessionsCompleted,
            s.EfficiencyRate,
            s.CreatedAt)).ToList();
    }

    public async Task<OperatorPerformanceDto> GenerateAsync(Guid tenantId, string period, Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct)
            ?? throw new KeyNotFoundException("Потребителят не беше намерен.");

        var assignedTasks = await db.WorkTasks.CountAsync(t => t.TenantId == tenantId && t.AssignedUserId == userId, ct);
        var completedTasks = await db.WorkTasks.CountAsync(t => t.TenantId == tenantId && t.AssignedUserId == userId && t.Status == WorkTaskStatus.Completed, ct);
        var overdueTasks = await db.WorkTasks.CountAsync(t => t.TenantId == tenantId && t.AssignedUserId == userId && t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled && t.DueDate < DateTime.UtcNow, ct);
        var pickingCompleted = await db.PickingOrders.CountAsync(p => p.TenantId == tenantId && p.AssignedUserId == userId && p.Status == PickingOrderStatus.Completed, ct);
        var inventorySessionsCompleted = await db.InventorySessions.CountAsync(s => s.TenantId == tenantId && s.StartedByUserId == userId && s.Status == InventorySessionStatus.Completed, ct);
        var efficiencyRate = assignedTasks == 0 ? 0m : Math.Round((decimal)completedTasks / assignedTasks * 100m, 2);

        var snapshot = new OperatorPerformanceSnapshot
        {
            TenantId = tenantId,
            UserId = userId,
            Period = period,
            TasksAssigned = assignedTasks,
            TasksCompleted = completedTasks,
            TasksOverdue = overdueTasks,
            PickingCompleted = pickingCompleted,
            InventorySessionsCompleted = inventorySessionsCompleted,
            EfficiencyRate = efficiencyRate
        };

        db.OperatorPerformanceSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "OPERATOR_PERFORMANCE_GENERATED", userId, null, "OperatorPerformanceSnapshot", snapshot.Id.ToString(), $"Generated performance snapshot for {user.FullName}", null, ct);

        return new OperatorPerformanceDto(
            snapshot.Id,
            snapshot.UserId,
            user.FullName,
            snapshot.Period,
            snapshot.TasksAssigned,
            snapshot.TasksCompleted,
            snapshot.TasksOverdue,
            snapshot.PickingCompleted,
            snapshot.InventorySessionsCompleted,
            snapshot.EfficiencyRate,
            snapshot.CreatedAt);
    }
}
