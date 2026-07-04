using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.Services.Ecommerce;

public class WooCommerceAdapter : IEcommerceAdapter
{
    private readonly ILogger<WooCommerceAdapter> _logger;

    public WooCommerceAdapter(ILogger<WooCommerceAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<List<EcommerceOrder>> FetchNewOrdersAsync(EcommerceStore store)
    {
        _logger.LogInformation("Fetching orders from WooCommerce at {Url}", store.StoreUrl);
        // Симулация на извличане на поръчки
        return new List<EcommerceOrder>();
    }

    public async Task SyncStockAsync(EcommerceStore store, Item item, decimal quantity)
    {
        _logger.LogInformation("Syncing stock for SKU {Sku} to WooCommerce: {Qty}", item.Sku, quantity);
    }
}
