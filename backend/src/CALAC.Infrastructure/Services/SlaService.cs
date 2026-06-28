using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services;

public record SlaTaskMetric(
    Guid Id,
    string Title,
    string? Reference,
    string Status,
    string SlaStatus,
    int? DaysRemaining,
    bool IsOverdue,
    DateTime? DueDate,
    DateTime CreatedAt);

public record SlaOverviewDto(
    int TotalTasks,
    int OverdueTasks,
    int AtRiskTasks,
    int OnTrackTasks,
    IReadOnlyList<SlaTaskMetric> Tasks);

public class SlaService(AppDbContext db)
{
    public async Task<SlaOverviewDto> GetOverviewAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tasks = await db.WorkTasks
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .ToListAsync(ct);

        var metrics = tasks.Select(task => BuildMetric(task, now)).ToList();

        return new SlaOverviewDto(
            metrics.Count,
            metrics.Count(m => m.IsOverdue),
            metrics.Count(m => m.SlaStatus is "Warning" or "Critical"),
            metrics.Count(m => m.SlaStatus is "Healthy" or "Info"),
            metrics);
    }

    private static SlaTaskMetric BuildMetric(WorkTask task, DateTime now)
    {
        var isCompleted = task.Status is WorkTaskStatus.Completed or WorkTaskStatus.Cancelled;
        var isOverdue = !isCompleted && task.DueDate.HasValue && task.DueDate.Value < now;

        int? daysRemaining = null;
        if (task.DueDate.HasValue && !isCompleted)
        {
            daysRemaining = (task.DueDate.Value.Date - now.Date).Days;
        }

        var slaStatus = (isCompleted, isOverdue, daysRemaining) switch
        {
            (true, _, _) => "Completed",
            (_, true, _) => "Critical",
            (_, false, < 1) => "Warning",
            (_, false, <= 2) when task.Priority >= WorkTaskPriority.High => "Warning",
            (_, false, <= 3) => "Info",
            _ => "Healthy"
        };

        return new SlaTaskMetric(
            task.Id,
            task.Title,
            task.Reference,
            task.Status.ToString(),
            slaStatus,
            daysRemaining,
            isOverdue,
            task.DueDate,
            task.CreatedAt);
    }
}
