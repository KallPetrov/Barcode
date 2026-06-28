using CALAC.Domain.Enums;

namespace CALAC.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Device> Devices { get; set; } = [];
    public ICollection<Location> Locations { get; set; } = [];
    public ICollection<Item> Items { get; set; } = [];
    public ICollection<ErpConfiguration> ErpConfigurations { get; set; } = [];
}

public class User : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PinHash { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Operator;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}

public class Device : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string HardwareId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public int? BatteryLevel { get; set; }
    public Guid? AssignedUserId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? AssignedUser { get; set; }
    public ICollection<SyncOperation> SyncOperations { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}

public class AuditLog : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? DeviceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Device? Device { get; set; }
}

public class SyncOperation : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public string ClientOperationId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public SyncOperationStatus Status { get; set; } = SyncOperationStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public Device Device { get; set; } = null!;
}

public class Location : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Level { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<InventoryStock> InventoryStocks { get; set; } = [];
    public ICollection<InventoryCount> InventoryCounts { get; set; } = [];
    public ICollection<GoodsReceiptLine> GoodsReceiptLines { get; set; } = [];
    public ICollection<TransferOrderLine> TransferLinesSource { get; set; } = [];
    public ICollection<TransferOrderLine> TransferLinesTarget { get; set; } = [];
    public ICollection<ErpConfiguration> ErpConfigurations { get; set; } = [];
}

public class Item : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Barcode { get; set; }
    public string? BarcodeType { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Weight { get; set; }
    public string? UnitOfMeasure { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<InventoryStock> InventoryStocks { get; set; } = [];
    public ICollection<InventoryCount> InventoryCounts { get; set; } = [];
}

public class InventoryStock : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Guid LocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? ReservedQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Item Item { get; set; } = null!;
    public Location Location { get; set; } = null!;
}

public enum InventorySessionStatus
{
    Draft = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public class InventorySession : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InventorySessionStatus Status { get; set; } = InventorySessionStatus.Draft;
    public Guid? StartedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public User? StartedByUser { get; set; }
    public User? CompletedByUser { get; set; }
    public ICollection<InventoryCount> InventoryCounts { get; set; } = [];
}

public class InventoryCount : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InventorySessionId { get; set; }
    public Guid SessionId => InventorySessionId;
    public Guid ItemId { get; set; }
    public Guid LocationId { get; set; }
    public decimal? ExpectedQuantity { get; set; }
    public decimal? SystemQuantity => ExpectedQuantity;
    public decimal? CountedQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? CountedByUserId { get; set; }
    public DateTime? CountedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public InventorySession InventorySession { get; set; } = null!;
    public Item Item { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public User? CountedByUser { get; set; }
}

public enum PickingStrategy
{
    FIFO = 0, // First In, First Out
    FEFO = 1  // First Expired, First Out
}

public enum PickingOrderStatus
{
    Draft = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public class PickingOrder : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public PickingStrategy Strategy { get; set; } = PickingStrategy.FIFO;
    public PickingOrderStatus Status { get; set; } = PickingOrderStatus.Draft;
    public Guid? AssignedUserId { get; set; }
    public Guid? StartedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public User? AssignedUser { get; set; }
    public User? StartedByUser { get; set; }
    public User? CompletedByUser { get; set; }
    public ICollection<PickingOrderLine> Lines { get; set; } = [];
}

public class PickingOrderLine : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PickingOrderId { get; set; }
    public Guid ItemId { get; set; }
    public Guid? SourceLocationId { get; set; }
    public Guid? TargetLocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? PickedQuantity { get; set; }
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public PickingOrder PickingOrder { get; set; } = null!;
    public Item Item { get; set; } = null!;
    public Location? SourceLocation { get; set; }
    public Location? TargetLocation { get; set; }
    public ICollection<PickingStockLine> StockLines { get; set; } = [];
}

public class PickingStockLine : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PickingOrderLineId { get; set; }
    public Guid InventoryStockId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? PickedByUserId { get; set; }
    public DateTime? PickedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public PickingOrderLine PickingOrderLine { get; set; } = null!;
    public InventoryStock InventoryStock { get; set; } = null!;
    public User? PickedByUser { get; set; }
}

public enum GoodsReceiptStatus
{
    Draft = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public class GoodsReceipt : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? SupplierName { get; set; }
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;
    public Guid? ReceivedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public User? ReceivedByUser { get; set; }
    public User? CompletedByUser { get; set; }
    public ICollection<GoodsReceiptLine> Lines { get; set; } = [];
}

public class GoodsReceiptLine : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid GoodsReceiptId { get; set; }
    public Guid ItemId { get; set; }
    public Guid LocationId { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal? ReceivedQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public Item Item { get; set; } = null!;
    public Location Location { get; set; } = null!;
}

public enum TransferOrderStatus
{
    Draft = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public class TransferOrder : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public TransferOrderStatus Status { get; set; } = TransferOrderStatus.Draft;
    public Guid? MovedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? MovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public User? MovedByUser { get; set; }
    public User? CompletedByUser { get; set; }
    public ICollection<TransferOrderLine> Lines { get; set; } = [];
}

public class TransferOrderLine : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TransferOrderId { get; set; }
    public Guid ItemId { get; set; }
    public Guid SourceLocationId { get; set; }
    public Guid TargetLocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? MovedQuantity { get; set; }
    public DateTime? MovedAt { get; set; }
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public TransferOrder TransferOrder { get; set; } = null!;
    public Item Item { get; set; } = null!;
    public Location SourceLocation { get; set; } = null!;
    public Location TargetLocation { get; set; } = null!;
}

public enum ErpProviderType
{
    None = 0,
    Odoo = 1,
    Dynamics365 = 2,
    Sap = 3,
    Custom = 99
}

public class ErpConfiguration : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ErpProviderType ProviderType { get; set; } = ErpProviderType.None;
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? DatabaseName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AutoSyncItems { get; set; } = false;
    public bool AutoSyncInventory { get; set; } = false;
    public string? SettingsJson { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
