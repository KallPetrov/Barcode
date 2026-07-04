using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.Services.Ecommerce;

public class EcommerceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EcommerceService> _logger;
    private readonly IEnumerable<IEcommerceAdapter> _adapters;
    private readonly PickingService _pickingService;

    public EcommerceService(AppDbContext db, ILogger<EcommerceService> logger, IEnumerable<IEcommerceAdapter> adapters, PickingService pickingService)
    {
        _db = db;
        _logger = logger;
        _adapters = adapters;
        _pickingService = pickingService;
    }

    public async Task<List<EcommerceStore>> GetStoresAsync(Guid tenantId)
    {
        return await _db.EcommerceStores
            .Where(s => s.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<EcommerceStore> CreateStoreAsync(EcommerceStore store)
    {
        _db.EcommerceStores.Add(store);
        await _db.SaveChangesAsync();
        return store;
    }

    public async Task ImportOrdersAsync(Guid storeId)
    {
        var store = await _db.EcommerceStores.FindAsync(storeId) ?? throw new Exception("Store not found");
        var adapter = GetAdapter(store.PlatformType);

        var newOrders = await adapter.FetchNewOrdersAsync(store);
        foreach (var order in newOrders)
        {
            var exists = await _db.EcommerceOrders.AnyAsync(o => o.ExternalOrderId == order.ExternalOrderId && o.TenantId == store.TenantId);
            if (!exists)
            {
                order.EcommerceStoreId = store.Id;
                order.TenantId = store.TenantId;
                order.Status = EcommerceOrderStatus.Imported;
                _db.EcommerceOrders.Add(order);

                // Автоматично създаване на Picking Order
                if (store.AutoImportOrders)
                {
                    await CreatePickingForOrderAsync(order);
                }
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task ProcessOrderWebhookAsync(Guid storeId, string json)
    {
        var store = await _db.EcommerceStores.FindAsync(storeId) ?? throw new Exception("Store not found");
        _logger.LogInformation("Processing webhook for store {StoreName}", store.Name);

        // Тук се добавя логика за парсване на JSON според платформата и създаване на EcommerceOrder
        // За демо цели само логваме
    }

    private IEcommerceAdapter GetAdapter(EcommercePlatformType type)
    {
        return type switch
        {
            EcommercePlatformType.WooCommerce => _adapters.OfType<WooCommerceAdapter>().First(),
            EcommercePlatformType.Shopify => _adapters.OfType<ShopifyAdapter>().First(),
            _ => throw new NotSupportedException($"Platform {type} is not supported")
        };
    }

    private async Task CreatePickingForOrderAsync(EcommerceOrder order)
    {
        _logger.LogInformation("Creating Picking Order for Ecommerce Order {OrderNumber}", order.OrderNumber);

        // В реална система тук ще извлечем линиите на поръчката от RawJson или друго място
        // За демо цели създаваме празна picking поръчка или с мок данни
        var pickingRequest = new CreatePickingOrderRequest(
            Name: $"Ecom Order: {order.OrderNumber}",
            Reference: order.OrderNumber,
            Strategy: "FEFO",
            AssignedUserId: null,
            Notes: $"Автоматично генерирана от {order.EcommerceStore?.Name}",
            Lines: new List<CreatePickingLineRequest>() // Тук трябва да се добавят реални линии
        );

        // Тъй като нямаме линии, няма да създаваме реално в БД, за да не гърми PickingService
        // Но логиката е тук.
        await Task.CompletedTask;
    }

    public async Task<List<EcommerceOrder>> GetOrdersAsync(Guid tenantId)
    {
        return await _db.EcommerceOrders
            .Include(o => o.EcommerceStore)
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.OrderCreatedAt)
            .ToListAsync();
    }
}
