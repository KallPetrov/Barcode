using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using CALAC.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

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
        var service = new InventoryService(db, audit.Object);
        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "TEST-1", Name = "Test Item" };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOC-1", Name = "Loc 1" };
        db.Items.Add(item);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var request = new AddStockRequest(item.Id, location.Id, 10, null, null, null);

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
        var alerts = new NotificationAlertService(db, audit);
        var hub = new Mock<IHubContext<DynamicHubProxy>>();
        var clients = new Mock<IHubClients>();
        var group = new Mock<IClientProxy>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);

        var service = new PickingService(db, audit, alerts, hub.Object);
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
        var alerts = new Mock<NotificationAlertService>(db, audit.Object);
        var hub = new Mock<IHubContext<DynamicHubProxy>>();
        var service = new PickingService(db, audit.Object, alerts.Object, hub.Object);

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
    public async Task SyncService_ProcessOperation_AddsStockCorrectly()
    {
        // Arrange
        using var db = CreateDbContext();
        var audit = new Mock<AuditService>(db);
        var inventory = new InventoryService(db, audit.Object);
        var service = new SyncService(db, audit.Object, inventory);

        var tenantId = Guid.NewGuid();
        var item = new Item { Id = Guid.NewGuid(), TenantId = tenantId, Sku = "SKU-S", Name = "Sync Item", IsActive = true };
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOC-S", Name = "Sync Loc", IsActive = true };
        db.Items.Add(item);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var payload = System.Text.Json.JsonSerializer.Serialize(new AddStockRequest(item.Id, location.Id, 5, "B1", null, null));
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
}
