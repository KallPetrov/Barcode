using System.Linq.Expressions;
using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantService? tenantService = null) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncOperation> SyncOperations => Set<SyncOperation>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<PartnerApiKey> PartnerApiKeys => Set<PartnerApiKey>();
    public DbSet<BillOfMaterial> BillOfMaterials => Set<BillOfMaterial>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderConsumption> WorkOrderConsumptions => Set<WorkOrderConsumption>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();
    public DbSet<WaveBatch> WaveBatches => Set<WaveBatch>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<InventorySession> InventorySessions => Set<InventorySession>();
    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();
    public DbSet<PickingOrder> PickingOrders => Set<PickingOrder>();
    public DbSet<PickingOrderLine> PickingOrderLines => Set<PickingOrderLine>();
    public DbSet<PickingStockLine> PickingStockLines => Set<PickingStockLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<TransferOrder> TransferOrders => Set<TransferOrder>();
    public DbSet<TransferOrderLine> TransferOrderLines => Set<TransferOrderLine>();
    public DbSet<ErpConfiguration> ErpConfigurations => Set<ErpConfiguration>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<OperatorPerformanceSnapshot> OperatorPerformanceSnapshots => Set<OperatorPerformanceSnapshot>();
    public DbSet<NotificationAlert> NotificationAlerts => Set<NotificationAlert>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CourierConfiguration> CourierConfigurations => Set<CourierConfiguration>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<EcommerceStore> EcommerceStores => Set<EcommerceStore>();
    public DbSet<EcommerceOrder> EcommerceOrders => Set<EcommerceOrder>();

    public virtual Guid? CurrentTenantId => tenantService?.GetTenantId();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (CurrentTenantId.HasValue)
                {
                    entry.Entity.TenantId = CurrentTenantId.Value;
                }
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext).GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
        }

        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Code).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany(t => t.Users).HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.HardwareId }).IsUnique();
            e.Property(x => x.HardwareId).HasMaxLength(200);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Manufacturer).HasMaxLength(100);
            e.Property(x => x.Model).HasMaxLength(100);
            e.HasOne(x => x.Tenant).WithMany(t => t.Devices).HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.AssignedUser).WithMany(u => u.Devices).HasForeignKey(x => x.AssignedUserId);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.TenantId, x.UserId });
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.EntityType).HasMaxLength(100);
            e.HasOne(x => x.User).WithMany(u => u.AuditLogs).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Device).WithMany(d => d.AuditLogs).HasForeignKey(x => x.DeviceId);
        });

        modelBuilder.Entity<SyncOperation>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DeviceId, x.ClientOperationId }).IsUnique();
            e.HasIndex(x => new { x.DeviceId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.ClientOperationId).HasMaxLength(100);
            e.Property(x => x.IdempotencyKey).HasMaxLength(200);
            e.Property(x => x.OperationType).HasMaxLength(100);
            e.HasOne(x => x.Device).WithMany(d => d.SyncOperations).HasForeignKey(x => x.DeviceId);
        });

        modelBuilder.Entity<Location>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Zone).HasMaxLength(100);
            e.Property(x => x.Aisle).HasMaxLength(50);
            e.Property(x => x.Rack).HasMaxLength(50);
            e.Property(x => x.Level).HasMaxLength(50);
            e.Property(x => x.Position).HasMaxLength(50);
            e.HasOne(x => x.Tenant).WithMany(t => t.Locations).HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Barcode });
            e.Property(x => x.Sku).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Barcode).HasMaxLength(200);
            e.Property(x => x.BarcodeType).HasMaxLength(50);
            e.Property(x => x.UnitOfMeasure).HasMaxLength(50);
            e.HasOne(x => x.Tenant).WithMany(t => t.Items).HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<WebhookSubscription>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.EventType });
            e.HasIndex(x => new { x.TenantId, x.IsActive });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.EventType).HasMaxLength(100);
            e.Property(x => x.Url).HasMaxLength(500);
            e.Property(x => x.Secret).HasMaxLength(200);
            e.Property(x => x.LastError).HasMaxLength(500);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<PartnerApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.IsActive });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Key).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<TenantSubscription>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.PlanCode });
            e.Property(x => x.PlanCode).HasMaxLength(100);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<TenantBranding>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.CompanyName).HasMaxLength(200);
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.PrimaryColor).HasMaxLength(20);
            e.Property(x => x.SecondaryColor).HasMaxLength(20);
            e.Property(x => x.FaviconUrl).HasMaxLength(500);
            e.Property(x => x.WelcomeMessage).HasMaxLength(500);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<WaveBatch>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Name });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.OrdersJson).HasMaxLength(2000);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<InventoryStock>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.LocationId });
            e.HasIndex(x => new { x.TenantId, x.BatchNumber });
            e.HasIndex(x => new { x.TenantId, x.SerialNumber });
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.ReservedQuantity).HasPrecision(18, 4);
            e.Property(x => x.BatchNumber).HasMaxLength(100);
            e.Property(x => x.SerialNumber).HasMaxLength(100);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.Item).WithMany(i => i.InventoryStocks).HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.Location).WithMany(l => l.InventoryStocks).HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<InventorySession>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.StartedByUser).WithMany().HasForeignKey(x => x.StartedByUserId);
            e.HasOne(x => x.CompletedByUser).WithMany().HasForeignKey(x => x.CompletedByUserId);
        });

        modelBuilder.Entity<InventoryCount>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.InventorySessionId);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.LocationId });
            e.Property(x => x.ExpectedQuantity).HasPrecision(18, 4);
            e.Property(x => x.CountedQuantity).HasPrecision(18, 4);
            e.Property(x => x.BatchNumber).HasMaxLength(100);
            e.Property(x => x.SerialNumber).HasMaxLength(100);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.InventorySession).WithMany(s => s.InventoryCounts).HasForeignKey(x => x.InventorySessionId);
            e.HasOne(x => x.Item).WithMany(i => i.InventoryCounts).HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.Location).WithMany(l => l.InventoryCounts).HasForeignKey(x => x.LocationId);
            e.HasOne(x => x.CountedByUser).WithMany().HasForeignKey(x => x.CountedByUserId);
        });

        modelBuilder.Entity<PickingOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Reference).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId);
            e.HasOne(x => x.StartedByUser).WithMany().HasForeignKey(x => x.StartedByUserId);
            e.HasOne(x => x.CompletedByUser).WithMany().HasForeignKey(x => x.CompletedByUserId);
        });

        modelBuilder.Entity<PickingOrderLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PickingOrderId);
            e.HasIndex(x => new { x.TenantId, x.ItemId });
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.PickedQuantity).HasPrecision(18, 4);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.PickingOrder).WithMany(p => p.Lines).HasForeignKey(x => x.PickingOrderId);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.SourceLocation).WithMany().HasForeignKey(x => x.SourceLocationId);
            e.HasOne(x => x.TargetLocation).WithMany().HasForeignKey(x => x.TargetLocationId);
        });

        modelBuilder.Entity<PickingStockLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PickingOrderLineId);
            e.HasIndex(x => x.InventoryStockId);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.PickingOrderLine).WithMany(p => p.StockLines).HasForeignKey(x => x.PickingOrderLineId);
            e.HasOne(x => x.InventoryStock).WithMany().HasForeignKey(x => x.InventoryStockId);
            e.HasOne(x => x.PickedByUser).WithMany().HasForeignKey(x => x.PickedByUserId);
        });

        modelBuilder.Entity<GoodsReceipt>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Reference).HasMaxLength(200);
            e.Property(x => x.SupplierName).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.ReceivedByUser).WithMany().HasForeignKey(x => x.ReceivedByUserId);
            e.HasOne(x => x.CompletedByUser).WithMany().HasForeignKey(x => x.CompletedByUserId);
        });

        modelBuilder.Entity<GoodsReceiptLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.GoodsReceiptId);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.LocationId });
            e.Property(x => x.ExpectedQuantity).HasPrecision(18, 4);
            e.Property(x => x.ReceivedQuantity).HasPrecision(18, 4);
            e.Property(x => x.BatchNumber).HasMaxLength(100);
            e.Property(x => x.SerialNumber).HasMaxLength(100);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.GoodsReceipt).WithMany(g => g.Lines).HasForeignKey(x => x.GoodsReceiptId);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.Location).WithMany(l => l.GoodsReceiptLines).HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<TransferOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Reference).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.MovedByUser).WithMany().HasForeignKey(x => x.MovedByUserId);
            e.HasOne(x => x.CompletedByUser).WithMany().HasForeignKey(x => x.CompletedByUserId);
        });

        modelBuilder.Entity<TransferOrderLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TransferOrderId);
            e.HasIndex(x => new { x.TenantId, x.ItemId });
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.MovedQuantity).HasPrecision(18, 4);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.TransferOrder).WithMany(t => t.Lines).HasForeignKey(x => x.TransferOrderId);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
            e.HasOne(x => x.SourceLocation).WithMany(l => l.TransferLinesSource).HasForeignKey(x => x.SourceLocationId);
            e.HasOne(x => x.TargetLocation).WithMany(l => l.TransferLinesTarget).HasForeignKey(x => x.TargetLocationId);
        });

        modelBuilder.Entity<ErpConfiguration>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ProviderType });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ApiUrl).HasMaxLength(500);
            e.Property(x => x.ApiKey).HasMaxLength(500);
            e.Property(x => x.Username).HasMaxLength(200);
            e.Property(x => x.Password).HasMaxLength(200);
            e.Property(x => x.DatabaseName).HasMaxLength(200);
            e.Property(x => x.SettingsJson).HasMaxLength(2000);
            e.HasOne(x => x.Tenant).WithMany(t => t.ErpConfigurations).HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<WorkTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.AssignedUserId);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.TaskType).HasMaxLength(100);
            e.Property(x => x.Reference).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId);
        });

        modelBuilder.Entity<OperatorPerformanceSnapshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.UserId, x.Period });
            e.Property(x => x.Period).HasMaxLength(50);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<NotificationAlert>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.IsRead });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<Reminder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.IsCompleted, x.DueAt });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.RelatedEntityType).HasMaxLength(100);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Token).IsUnique();
            e.Property(x => x.Token).HasMaxLength(200);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<CourierConfiguration>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CourierType });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ApiUrl).HasMaxLength(500);
            e.Property(x => x.ApiKey).HasMaxLength(500);
            e.Property(x => x.Username).HasMaxLength(200);
            e.Property(x => x.Password).HasMaxLength(200);
            e.Property(x => x.ClientNumber).HasMaxLength(100);
            e.Property(x => x.SettingsJson).HasMaxLength(2000);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<Shipment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.WaybillNumber);
            e.Property(x => x.ReferenceNumber).HasMaxLength(200);
            e.Property(x => x.ReceiverName).HasMaxLength(200);
            e.Property(x => x.ReceiverPhone).HasMaxLength(50);
            e.Property(x => x.ReceiverEmail).HasMaxLength(200);
            e.Property(x => x.ReceiverAddress).HasMaxLength(500);
            e.Property(x => x.ReceiverCity).HasMaxLength(200);
            e.Property(x => x.ReceiverPostCode).HasMaxLength(20);
            e.Property(x => x.TotalWeight).HasPrecision(18, 4);
            e.Property(x => x.CodAmount).HasPrecision(18, 4);
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.WaybillNumber).HasMaxLength(100);
            e.Property(x => x.LabelPdfUrl).HasMaxLength(500);
            e.Property(x => x.LabelZpl).HasMaxLength(2000);
            e.Property(x => x.TrackingUrl).HasMaxLength(500);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.CourierConfiguration).WithMany().HasForeignKey(x => x.CourierConfigurationId);
            e.HasOne(x => x.PickingOrder).WithMany().HasForeignKey(x => x.PickingOrderId);
        });

        modelBuilder.Entity<EcommerceStore>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.PlatformType });
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.StoreUrl).HasMaxLength(500);
            e.Property(x => x.ConsumerKey).HasMaxLength(500);
            e.Property(x => x.ConsumerSecret).HasMaxLength(500);
            e.Property(x => x.WebhookSecret).HasMaxLength(500);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<EcommerceOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ExternalOrderId }).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.ExternalOrderId).HasMaxLength(100);
            e.Property(x => x.OrderNumber).HasMaxLength(100);
            e.Property(x => x.CustomerName).HasMaxLength(200);
            e.Property(x => x.CustomerEmail).HasMaxLength(200);
            e.Property(x => x.CustomerPhone).HasMaxLength(50);
            e.Property(x => x.ShippingAddress).HasMaxLength(500);
            e.Property(x => x.ShippingCity).HasMaxLength(200);
            e.Property(x => x.TotalAmount).HasPrecision(18, 4);
            e.Property(x => x.Currency).HasMaxLength(10);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.EcommerceStore).WithMany(s => s.Orders).HasForeignKey(x => x.EcommerceStoreId);
            e.HasOne(x => x.PickingOrder).WithMany().HasForeignKey(x => x.PickingOrderId);
        });
    }
}

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Tenants.AnyAsync())
            return;

        var tenant = new Tenant
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Демо компания",
            Code = "DEMO"
        };

        var admin = new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TenantId = tenant.Id,
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            PinHash = BCrypt.Net.BCrypt.HashPassword("1234"),
            FullName = "Администратор",
            Role = UserRole.Admin
        };

        var operatorUser = new User
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            TenantId = tenant.Id,
            Username = "operator",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator123!"),
            PinHash = BCrypt.Net.BCrypt.HashPassword("0000"),
            FullName = "Складов оператор",
            Role = UserRole.Operator
        };

        db.Tenants.Add(tenant);
        db.Users.AddRange(admin, operatorUser);
        await db.SaveChangesAsync();
    }
}
