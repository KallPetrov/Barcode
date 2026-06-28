namespace CALAC.Domain.Entities;

public enum WorkTaskStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum WorkTaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public class WorkTask : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskType { get; set; } = "General";
    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Medium;
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Open;
    public Guid? AssignedUserId { get; set; }
    public string? Reference { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? AssignedUser { get; set; }
}
