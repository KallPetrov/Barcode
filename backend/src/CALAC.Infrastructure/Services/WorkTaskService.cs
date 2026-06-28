using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services;

public record WorkTaskDto(
    Guid Id,
    string Title,
    string? Description,
    string TaskType,
    WorkTaskPriority Priority,
    WorkTaskStatus Status,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Reference,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CompletedAt);

public record CreateWorkTaskRequest(
    string Title,
    string? Description,
    string TaskType,
    WorkTaskPriority Priority,
    Guid? AssignedUserId,
    string? Reference,
    DateTime? DueDate);

public record UpdateWorkTaskRequest(
    string? Title,
    string? Description,
    string? TaskType,
    WorkTaskPriority? Priority,
    WorkTaskStatus? Status,
    Guid? AssignedUserId,
    string? Reference,
    DateTime? DueDate);

public class WorkTaskService(AppDbContext db, AuditService audit, NotificationAlertService alerts)
{
    public async Task<IReadOnlyList<WorkTaskDto>> ListAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.WorkTasks
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .Select(t => new WorkTaskDto(
                t.Id,
                t.Title,
                t.Description,
                t.TaskType,
                t.Priority,
                t.Status,
                t.AssignedUserId,
                t.AssignedUser != null ? t.AssignedUser.FullName : null,
                t.Reference,
                t.DueDate,
                t.CreatedAt,
                t.UpdatedAt,
                t.CompletedAt))
            .ToListAsync(ct);

    public async Task<WorkTaskDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var task = await db.WorkTasks
            .Include(t => t.AssignedUser)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);

        return task is null
            ? null
            : new WorkTaskDto(
                task.Id,
                task.Title,
                task.Description,
                task.TaskType,
                task.Priority,
                task.Status,
                task.AssignedUserId,
                task.AssignedUser?.FullName,
                task.Reference,
                task.DueDate,
                task.CreatedAt,
                task.UpdatedAt,
                task.CompletedAt);
    }

    public async Task<WorkTaskDto> CreateAsync(Guid tenantId, CreateWorkTaskRequest request, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Заглавието е задължително.");

        if (request.AssignedUserId is Guid assignedUserId &&
            !await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Id == assignedUserId, ct))
            throw new InvalidOperationException("Избраният потребител не съществува в текущия тенант.");

        var task = new WorkTask
        {
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            TaskType = string.IsNullOrWhiteSpace(request.TaskType) ? "General" : request.TaskType.Trim(),
            Priority = request.Priority,
            Status = WorkTaskStatus.Open,
            AssignedUserId = request.AssignedUserId,
            Reference = request.Reference?.Trim(),
            DueDate = request.DueDate,
            CreatedAt = DateTime.UtcNow
        };

        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "WORK_TASK_CREATED", userId, null, "WorkTask", task.Id.ToString(),
            $"Created task '{task.Title}'", null, ct);

        var alertLevel = task.Priority >= WorkTaskPriority.High ? AlertLevel.Warning : AlertLevel.Info;
        await alerts.CreateAsync(
            tenantId,
            task.Priority >= WorkTaskPriority.High ? "Нова спешна задача" : "Нова задача",
            $"Създадена е задача '{task.Title}'.",
            alertLevel,
            userId,
            ct);

        return await GetAsync(tenantId, task.Id, ct) ?? throw new InvalidOperationException("Task was not created.");
    }

    public async Task<WorkTaskDto> UpdateAsync(Guid tenantId, Guid id, UpdateWorkTaskRequest request, Guid userId, CancellationToken ct = default)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Задачата не беше намерена.");

        if (request.AssignedUserId is Guid assignedUserId &&
            !await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Id == assignedUserId, ct))
            throw new InvalidOperationException("Избраният потребител не съществува в текущия тенант.");

        task.Title = request.Title?.Trim() ?? task.Title;
        task.Description = request.Description?.Trim() ?? task.Description;
        task.TaskType = request.TaskType?.Trim() ?? task.TaskType;
        task.Priority = request.Priority ?? task.Priority;
        task.Status = request.Status ?? task.Status;
        task.AssignedUserId = request.AssignedUserId ?? task.AssignedUserId;
        task.Reference = request.Reference?.Trim() ?? task.Reference;
        task.DueDate = request.DueDate ?? task.DueDate;
        task.UpdatedAt = DateTime.UtcNow;

        if (request.Status == WorkTaskStatus.Completed && task.Status != WorkTaskStatus.Completed)
            task.CompletedAt = DateTime.UtcNow;

        if (request.Status != WorkTaskStatus.Completed && task.Status == WorkTaskStatus.Completed)
            task.CompletedAt = null;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "WORK_TASK_UPDATED", userId, null, "WorkTask", task.Id.ToString(),
            $"Updated task '{task.Title}'", null, ct);

        if (request.Status == WorkTaskStatus.Completed)
        {
            await alerts.CreateAsync(
                tenantId,
                "Задачата е завършена",
                $"Задачата '{task.Title}' е завършена.",
                AlertLevel.Info,
                userId,
                ct);
        }

        return await GetAsync(tenantId, task.Id, ct) ?? throw new InvalidOperationException("Task was not found after update.");
    }
}
