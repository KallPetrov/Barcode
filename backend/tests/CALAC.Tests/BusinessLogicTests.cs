using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using CALAC.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.AspNetCore.SignalR;

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
        var audit = new Mock<AuditService>(db);
        var alerts = new Mock<NotificationAlertService>(db, audit.Object);
        var hub = new Mock<IHubContext<DynamicHubProxy>>();
        var service = new PickingService(db, audit.Object, alerts.Object, hub.Object);
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
}
