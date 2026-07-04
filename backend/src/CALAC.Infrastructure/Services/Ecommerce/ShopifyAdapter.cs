using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.Services.Ecommerce;

public class ShopifyAdapter : IEcommerceAdapter
{
    private readonly ILogger<ShopifyAdapter> _logger;

    public ShopifyAdapter(ILogger<ShopifyAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<List<EcommerceOrder>> FetchNewOrdersAsync(EcommerceStore store)
    {
        _logger.LogInformation("Fetching orders from Shopify at {Url}", store.StoreUrl);
        // Симулация на извличане на поръчки
        return new List<EcommerceOrder>();
    }

    public async Task SyncStockAsync(EcommerceStore store, Item item, decimal quantity)
    {
        _logger.LogInformation("Syncing stock for SKU {Sku} to Shopify: {Qty}", item.Sku, quantity);
    }
}
