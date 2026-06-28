namespace CALAC.Domain.Entities;

public class Reminder : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public Guid? UserId { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime DueAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? User { get; set; }
}
