namespace CALAC.Domain.Entities;

public class OperatorPerformanceSnapshot
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Period { get; set; } = string.Empty;
    public int TasksAssigned { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksOverdue { get; set; }
    public int PickingCompleted { get; set; }
    public int InventorySessionsCompleted { get; set; }
    public decimal EfficiencyRate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
}
