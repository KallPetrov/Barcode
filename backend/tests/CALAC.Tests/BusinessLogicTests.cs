using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using CALAC.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using CALAC.Infrastructure.Services.Logistics;
using System.Net;
using System.Net.Http;

namespace CALAC.Tests;

public class BusinessLogicTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task InventoryService_AddStock_IncreasesQuantity()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new Mock<AuditService>(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var service = new InventoryService(db, audit.Object, sp);
        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "TEST-1", Name = "Test Item" };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOC-1", Name = "Loc 1" };
        db.Items.Add(item);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var request = new AddStockRequest(item.Id, location.Id, 10, null, null, null, null, null, null);

        // Act
        await service.AddStockAsync(tenantId, request, Guid.NewGuid());

        // Assert
        var stock = await db.InventoryStocks.FirstOrDefaultAsync(s => s.ItemId == item.Id);
        Assert.NotNull(stock);
        Assert.Equal(10, stock.Quantity);
    }

    [Fact]
    public async Task PickingService_CreateOrder_SavesToDatabase()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var alerts = new NotificationAlertService(db, audit, sp);
        var hub = new Mock<IHubContext<DynamicHubProxy>>();
        var clients = new Mock<IHubClients>();
        var group = new Mock<IClientProxy>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);

        var shippingLogger = new Mock<ILogger<ShippingService>>();
        var dataProtection = new Mock<IDataProtectionProvider>();
        dataProtection.Setup(p => p.CreateProtector(Moq.It.IsAny<string>())).Returns(new Mock<IDataProtector>().Object);
        var shippingService = new ShippingService(db, shippingLogger.Object, Enumerable.Empty<ICourierAdapter>(), dataProtection.Object);
        var service = new PickingService(db, audit, alerts, hub.Object, shippingService);
        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "SKU-1", Name = "Item 1" };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var lines = new List<CreatePickingLineRequest> { new CreatePickingLineRequest(item.Id, null, null, 5, "Test line") };
        var request = new CreatePickingOrderRequest("Order 1", "REF-1", "FIFO", null, "Notes", lines);

        // Act
        var result = await service.CreateAsync(tenantId, request, Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Order 1", result.Name);
        Assert.Single(result.Lines);
        var orderInDb = await db.PickingOrders.FirstOrDefaultAsync(o => o.Id == result.Id);
        Assert.NotNull(orderInDb);
    }

    [Fact]
    public async Task PickingService_StartFEFO_OrdersByExpiryDate()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new Mock<AuditService>(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var alerts = new Mock<NotificationAlertService>(db, audit.Object, sp);
        var hub = new Mock<IHubContext<DynamicHubProxy>>();
        var shippingLogger = new Mock<ILogger<ShippingService>>();
        var dataProtection = new Mock<IDataProtectionProvider>();
        dataProtection.Setup(p => p.CreateProtector(Moq.It.IsAny<string>())).Returns(new Mock<IDataProtector>().Object);
        var shippingService = new ShippingService(db, shippingLogger.Object, Enumerable.Empty<ICourierAdapter>(), dataProtection.Object);
        var service = new PickingService(db, audit.Object, alerts.Object, hub.Object, shippingService);

        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "SKU-1", Name = "Item 1" };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOC-1", Name = "Loc 1" };
        db.Items.Add(item);
        db.Locations.Add(location);

        // Stock 1: Expiry in 10 days
        db.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), TenantId = tenantId, ItemId = item.Id, LocationId = location.Id, Quantity = 10, ExpiryDate = DateTime.UtcNow.AddDays(10), CreatedAt = DateTime.UtcNow });
        // Stock 2: Expiry in 5 days
        db.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), TenantId = tenantId, ItemId = item.Id, LocationId = location.Id, Quantity = 10, ExpiryDate = DateTime.UtcNow.AddDays(5), CreatedAt = DateTime.UtcNow.AddDays(-1) });

        await db.SaveChangesAsync();

        var lines = new List<CreatePickingLineRequest> { new CreatePickingLineRequest(item.Id, null, null, 5, "") };
        var orderDto = await service.CreateAsync(tenantId, new CreatePickingOrderRequest("FEFO Order", "REF-2", "FEFO", null, "", lines), Guid.NewGuid());

        // Act
        await service.StartAsync(tenantId, orderDto.Id, Guid.NewGuid());

        // Assert
        var order = await db.PickingOrders.Include(o => o.Lines).ThenInclude(l => l.StockLines).FirstAsync(o => o.Id == orderDto.Id);
        var stockLine = order.Lines.First().StockLines.First();
        var stock = await db.InventoryStocks.FindAsync(stockLine.InventoryStockId);

        // Should pick from Stock 2 because it expires first
        Assert.Equal(DateTime.UtcNow.AddDays(5).Date, stock!.ExpiryDate!.Value.Date);
    }

    [Fact]
    public async Task AuthService_LoginWithPin_LocksOutAfterFailedAttempts()
    {
        // Arrange
        using var db = CreateDbContext();
        var configMock = new Mock<IConfiguration>();
        var service = new AuthService(db, configMock.Object);
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = "operator1",
            PinHash = BCrypt.Net.BCrypt.HashPassword("1234"),
            IsActive = true,
            Tenant = new Tenant { Id = tenantId, Name = "Test Tenant", Code = "TT" }
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            var result = await service.LoginWithPinAsync("operator1", "wrong");
            Assert.False(result.Success);
            Assert.Equal("Невалиден PIN код", result.Error);
        }

        // 6th attempt should be locked out
        var lockoutResult = await service.LoginWithPinAsync("operator1", "1234");
        Assert.False(lockoutResult.Success);
        Assert.Contains("Акаунтът е блокиран", lockoutResult.Error!);
    }

    [Fact]
    public async Task ItemService_ListAsync_AppliesPaginationAndSearch()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var service = new ItemService(db, audit);
        var tenantId = Guid.NewGuid();

        db.Items.AddRange(
            new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "ALPHA-1", Name = "Alpha One", IsActive = true },
            new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "ALPHA-2", Name = "Alpha Two", IsActive = true },
            new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "BETA-1", Name = "Beta One", IsActive = true });
        await db.SaveChangesAsync();

        var result = await service.ListAsync(tenantId, page: 1, pageSize: 1, search: "ALPHA", sortBy: "sku", sortDirection: "asc");

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("ALPHA-1", result.Items[0].Sku);
    }

    [Fact]
    public async Task WebhookSubscriptionService_PublishAsync_SendsPayloadToActiveSubscriptions()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var handler = new StubHandler((request, cancellationToken) =>
        {
            Assert.Equal("https://example.test/webhook", request.RequestUri!.ToString());
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var httpClient = new HttpClient(handler);
        var service = new WebhookSubscriptionService(db, audit, httpClient);

        var tenantId = Guid.NewGuid();
        var subscription = await service.CreateAsync(tenantId, new CreateWebhookSubscriptionRequest("Picking completed", "PICKING_COMPLETED", "https://example.test/webhook", "secret", true), Guid.NewGuid());

        await service.PublishAsync(tenantId, "PICKING_COMPLETED", new { orderId = "123" });

        var saved = await db.WebhookSubscriptions.FirstAsync(s => s.Id == subscription.Id);
        Assert.True(saved.LastSuccessAt.HasValue);
        Assert.Null(saved.LastError);
    }

    [Fact]
    public async Task TenantOnboardingService_CreateAsync_CreatesTenantAndAdmin()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var service = new TenantOnboardingService(db, audit);

        var result = await service.CreateAsync(new CreateTenantOnboardingRequest("Acme Warehouse", "ACME", "admin", "Welcome123!", "Jane Admin"), Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("Acme Warehouse", result.TenantName);
        Assert.Equal("admin", result.AdminUsername);

        var tenant = await db.Tenants.FirstAsync(t => t.Id == result.TenantId);
        Assert.True(tenant.IsActive);

        var user = await db.Users.FirstAsync(u => u.TenantId == tenant.Id);
        Assert.Equal("admin", user.Username);
        Assert.True(BCrypt.Net.BCrypt.Verify("Welcome123!", user.PasswordHash));
    }

    [Fact]
    public async Task PartnerApiKeyService_CreateAndValidateAsync_Works()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var service = new PartnerApiKeyService(db, audit);

        var tenantId = Guid.NewGuid();
        var created = await service.CreateAsync(tenantId, new CreatePartnerApiKeyRequest("E-commerce", "partner-ecom"), Guid.NewGuid());

        Assert.NotNull(created.Key);
        Assert.StartsWith("pk_", created.Key);

        Assert.True(await service.ValidateAsync(tenantId, created.Key));
        Assert.False(await service.ValidateAsync(tenantId, "pk_invalid"));
    }

    [Fact]
    public async Task ForecastingService_GetForecastAsync_ReturnsWeightedAverage()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var service = new ForecastingService(db, audit);
        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "FC-1", Name = "Forecast Item" };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "FC-LOC", Name = "Forecast Loc" };
        db.Items.Add(item);
        db.Locations.Add(location);
        db.InventoryStocks.AddRange(
            new InventoryStock { Id = Guid.NewGuid(), TenantId = tenantId, ItemId = item.Id, LocationId = location.Id, Quantity = 10, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new InventoryStock { Id = Guid.NewGuid(), TenantId = tenantId, ItemId = item.Id, LocationId = location.Id, Quantity = 20, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new InventoryStock { Id = Guid.NewGuid(), TenantId = tenantId, ItemId = item.Id, LocationId = location.Id, Quantity = 30, CreatedAt = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var forecast = await service.GetForecastAsync(tenantId, item.Id, 3, 0.5m);

        // (10*0.5 + 20*1.0 + 30*1.5) / (0.5 + 1.0 + 1.5) = 70 / 3 = 23.333...
        Assert.Equal(23.33m, Math.Round(forecast.ExpectedDemand, 2));
    }

    [Fact]
    public async Task BatchPickingService_CreateWaveAsync_GroupsOrdersByLocation()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var service = new BatchPickingService(db, audit);
        var tenantId = Guid.NewGuid();

        var locationA = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "A1", Name = "A1" };
        var locationB = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "B1", Name = "B1" };
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "BP-1", Name = "Batch Item" };
        db.Locations.AddRange(locationA, locationB);
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var order1 = new PickingOrder { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Wave 1", Status = PickingOrderStatus.Draft };
        var order2 = new PickingOrder { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Wave 2", Status = PickingOrderStatus.Draft };
        db.PickingOrders.AddRange(order1, order2);
        db.PickingOrderLines.AddRange(
            new PickingOrderLine { Id = Guid.NewGuid(), TenantId = tenantId, PickingOrderId = order1.Id, ItemId = item.Id, SourceLocationId = locationA.Id, Quantity = 2 },
            new PickingOrderLine { Id = Guid.NewGuid(), TenantId = tenantId, PickingOrderId = order2.Id, ItemId = item.Id, SourceLocationId = locationB.Id, Quantity = 3 });
        await db.SaveChangesAsync();

        var wave = await service.CreateWaveAsync(tenantId, new CreateBatchWaveRequest("Batch Wave", new[] { order1.Id, order2.Id }), Guid.NewGuid());

        Assert.Equal("Batch Wave", wave.Name);
        Assert.Equal(2, wave.Orders.Count);
        Assert.Equal(2, wave.Groups.Count);
    }

    [Fact]
    public async Task NotificationAlertService_CreateExpiryAlertsAsync_DoesNotDuplicateForSameItem()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var service = new NotificationAlertService(db, audit, sp);
        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "EXP-1", Name = "Expiring Item" };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "E1", Name = "E1" };
        db.Items.Add(item);
        db.Locations.Add(location);
        db.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), TenantId = tenantId, ItemId = item.Id, LocationId = location.Id, Quantity = 5, ExpiryDate = DateTime.UtcNow.AddDays(3) });
        await db.SaveChangesAsync();

        await service.CreateExpiryAlertsAsync(tenantId, Guid.NewGuid());
        await service.CreateExpiryAlertsAsync(tenantId, Guid.NewGuid());

        var alerts = await db.NotificationAlerts.Where(a => a.TenantId == tenantId).ToListAsync();
        Assert.Single(alerts);
    }

    [Fact]
    public async Task TenantBrandingService_UpsertAsync_SavesBrandingSettings()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var service = new TenantBrandingService(db, audit);
        var tenantId = Guid.NewGuid();

        var created = await service.GetOrCreateAsync(tenantId);
        Assert.Equal("CALAC", created.CompanyName);

        var updated = await service.UpsertAsync(tenantId, new UpsertTenantBrandingRequest("Acme Warehouse", "/logo.png", "#123456", "#654321", "/favicon.ico", "Welcome to Acme"), Guid.NewGuid());

        Assert.Equal("Acme Warehouse", updated.CompanyName);
        Assert.Equal("#123456", updated.PrimaryColor);
        Assert.Equal("Welcome to Acme", updated.WelcomeMessage);

        var persisted = await db.TenantBrandings.FirstAsync(b => b.TenantId == tenantId);
        Assert.Equal("Acme Warehouse", persisted.CompanyName);
    }

    [Fact]
    public async Task SubscriptionService_ActivatePlan_SetsTenantPlanAndExpiry()
    {
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var service = new SubscriptionService(db, audit);
        var tenantId = Guid.NewGuid();

        var result = await service.ActivatePlanAsync(tenantId, new ActivateSubscriptionRequest("starter", 30), Guid.NewGuid());

        Assert.Equal("starter", result.PlanCode);
        Assert.True(result.IsActive);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task SyncService_PushAsync_IsIdempotentForSameKey()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var inventory = new InventoryService(db, audit, sp);
        var serviceProvider = sp;
        var service = new SyncService(db, audit, inventory, serviceProvider);

        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "SKU-IDEM", Name = "Sync Item", IsActive = true };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOC-IDEM", Name = "Sync Loc", IsActive = true };
        db.Items.Add(item);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var payload = System.Text.Json.JsonSerializer.Serialize(new AddStockRequest(item.Id, location.Id, 2, null, null, null, null, null, null));
        var operations = new List<SyncOperationItem>
        {
            new SyncOperationItem("op-idem", "STOCK_ADD", payload, DateTime.UtcNow, 1)
        };
        var request = new SyncPushRequest(operations);

        // Act
        var first = await service.PushAsync(tenantId, deviceId, Guid.NewGuid(), request, "idem-123");
        var second = await service.PushAsync(tenantId, deviceId, Guid.NewGuid(), request, "idem-123");

        // Assert
        Assert.True(first.Results[0].Success);
        Assert.True(second.Results[0].Success);
        Assert.Single(await db.SyncOperations.Where(s => s.DeviceId == deviceId).ToListAsync());
    }

    [Fact]
    public async Task SyncService_ProcessOperation_AddsStockCorrectly()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new Mock<AuditService>(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var inventory = new InventoryService(db, audit.Object, sp);
        var service = new SyncService(db, audit.Object, inventory, sp);

        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "SKU-S", Name = "Sync Item", IsActive = true };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOC-S", Name = "Sync Loc", IsActive = true };
        db.Items.Add(item);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var payload = System.Text.Json.JsonSerializer.Serialize(new AddStockRequest(item.Id, location.Id, 5, "B1", null, null, null, null, null));
        var operations = new List<SyncOperationItem>
        {
            new SyncOperationItem("op-1", "STOCK_ADD", payload, DateTime.UtcNow, 1)
        };
        var request = new SyncPushRequest(operations);

        // Act
        var result = await service.PushAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), request);

        // Assert
        Assert.True(result.Results[0].Success);
        var stock = await db.InventoryStocks.FirstOrDefaultAsync(s => s.ItemId == item.Id && s.BatchNumber == "B1");
        Assert.NotNull(stock);
        Assert.Equal(5, stock.Quantity);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    [Fact]
    public async Task InventoryService_FEFO_Picking_Suggested_Correctly()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new Mock<AuditService>(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var alerts = new Mock<NotificationAlertService>(db, audit.Object, sp);
        var hub = new Mock<IHubContext<DynamicHubProxy>>();
        var shippingLogger = new Mock<ILogger<ShippingService>>();
        var dataProtectionVal = new Mock<IDataProtectionProvider>();
        dataProtectionVal.Setup(p => p.CreateProtector(Moq.It.IsAny<string>())).Returns(new Mock<IDataProtector>().Object);
        var shippingService = new ShippingService(db, shippingLogger.Object, Enumerable.Empty<ICourierAdapter>(), dataProtectionVal.Object);
        var service = new PickingService(db, audit.Object, alerts.Object, hub.Object, shippingService);

        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "FEFO-1", Name = "FEFO Item" };
        var loc = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "L1", Name = "L1" };
        db.Items.Add(item);
        db.Locations.Add(loc);

        // Stock A expires later
        db.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), TenantId = tenantId, ItemId = item.Id, LocationId = loc.Id, Quantity = 10, ExpiryDate = DateTime.UtcNow.AddDays(20), CreatedAt = DateTime.UtcNow });
        // Stock B expires sooner
        db.InventoryStocks.Add(new InventoryStock { Id = Guid.NewGuid(), TenantId = tenantId, ItemId = item.Id, LocationId = loc.Id, Quantity = 10, ExpiryDate = DateTime.UtcNow.AddDays(5), CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var request = new CreatePickingOrderRequest("FEFO Order", "REF", "FEFO", null, "", new[] { new CreatePickingLineRequest(item.Id, null, null, 5, "") });
        var orderDto = await service.CreateAsync(tenantId, request, Guid.NewGuid());

        // Act
        await service.StartAsync(tenantId, orderDto.Id, Guid.NewGuid());

        // Assert
        var order = await db.PickingOrders.Include(o => o.Lines).ThenInclude(l => l.StockLines).FirstAsync(o => o.Id == orderDto.Id);
        var pickedStockId = order.Lines.First().StockLines.First().InventoryStockId;
        var pickedStock = await db.InventoryStocks.FindAsync(pickedStockId);

        Assert.Equal(DateTime.UtcNow.AddDays(5).Date, pickedStock!.ExpiryDate!.Value.Date);
    }

    [Fact]
    public async Task SyncService_ConflictResolution_LastWriteWins_ClientTimestamp()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new Mock<AuditService>(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var inventory = new InventoryService(db, audit.Object, sp);
        var serviceProvider = sp;
        var service = new SyncService(db, audit.Object, inventory, serviceProvider);

        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "SYNC-1", Name = "Sync Item" };
        var loc = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "L1", Name = "L1" };
        db.Items.Add(item);
        db.Locations.Add(loc);

        // Initial stock at T0
        var initialStock = new InventoryStock {
            TenantId = tenantId, ItemId = item.Id, LocationId = loc.Id, Quantity = 10,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10), Version = 1
        };
        db.InventoryStocks.Add(initialStock);
        await db.SaveChangesAsync();

        // Operation from client at T-5 (newer than T-10)
        var newerTimestamp = DateTime.UtcNow.AddMinutes(-5);
        var payload = System.Text.Json.JsonSerializer.Serialize(new AddStockRequest(item.Id, loc.Id, 5, null, null, null, null, null, null));
        var op = new SyncOperationItem("sync-op-1", "STOCK_ADD", payload, newerTimestamp, 2);

        // Act
        await service.PushAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), new SyncPushRequest(new[] { op }));

        // Assert
        var stock = await db.InventoryStocks.FirstAsync(s => s.ItemId == item.Id);
        Assert.Equal(15, stock.Quantity); // 10 + 5
    }

    [Fact]
    public async Task InventorySessionService_Complete_AdjustsStock()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new AuditService(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var alerts = new NotificationAlertService(db, audit, sp);
        var hub = new Mock<IHubContext<DynamicHubProxy>>();
        var service = new InventorySessionService(db, audit, alerts, hub.Object);

        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "SKU-COUNT", Name = "Item" };
        var loc = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "L1", Name = "L1" };
        db.Items.Add(item);
        db.Locations.Add(loc);
        db.InventoryStocks.Add(new InventoryStock { TenantId = tenantId, ItemId = item.Id, LocationId = loc.Id, Quantity = 10 });
        await db.SaveChangesAsync();

        var sessionDto = await service.CreateAsync(tenantId, new CreateSessionRequest("Session", ""), Guid.NewGuid());
        await service.StartAsync(tenantId, sessionDto.Id, Guid.NewGuid());

        var count = await db.InventoryCounts.FirstAsync(c => c.InventorySessionId == sessionDto.Id);
        await service.UpdateCountAsync(tenantId, count.Id, new UpdateCountRequest(15), Guid.NewGuid());

        // Act
        await service.CompleteAsync(tenantId, sessionDto.Id, Guid.NewGuid());

        // Assert
        var stock = await db.InventoryStocks.FirstAsync(s => s.ItemId == item.Id);
        Assert.Equal(15, stock.Quantity);
    }

    [Fact]
    public async Task SyncService_ConflictResolution_IgnoreOldUpdates()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new Mock<AuditService>(db);
        var sp = new ServiceCollection().BuildServiceProvider();
        var inventory = new InventoryService(db, audit.Object, sp);
        var serviceProvider = sp;
        var service = new SyncService(db, audit.Object, inventory, serviceProvider);

        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "SYNC-1", Name = "Sync Item" };
        var loc = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "L1", Name = "L1" };
        db.Items.Add(item);
        db.Locations.Add(loc);

        // Stock updated at T0
        var lastUpdate = DateTime.UtcNow;
        var initialStock = new InventoryStock {
            TenantId = tenantId, ItemId = item.Id, LocationId = loc.Id, Quantity = 10,
            UpdatedAt = lastUpdate, Version = 5
        };
        db.InventoryStocks.Add(initialStock);
        await db.SaveChangesAsync();

        // Operation from client at T-10 (older than T0)
        var olderTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var payload = System.Text.Json.JsonSerializer.Serialize(new AddStockRequest(item.Id, loc.Id, 5, null, null, null, null, null, null));
        var op = new SyncOperationItem("sync-op-old", "STOCK_ADD", payload, olderTimestamp, 4);

        // Act
        await service.PushAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), new SyncPushRequest(new[] { op }));

        // Assert
        var stock = await db.InventoryStocks.FirstAsync(s => s.ItemId == item.Id);
        Assert.Equal(10, stock.Quantity); // Should NOT change
    }
}
