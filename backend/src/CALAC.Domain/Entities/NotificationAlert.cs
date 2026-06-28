namespace CALAC.Domain.Entities;

public enum AlertLevel
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public class NotificationAlert : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertLevel Level { get; set; } = AlertLevel.Info;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
