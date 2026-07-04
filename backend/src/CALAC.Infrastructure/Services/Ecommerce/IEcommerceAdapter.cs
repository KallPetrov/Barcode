using CALAC.Domain.Entities;

namespace CALAC.Infrastructure.Services.Ecommerce;

public interface IEcommerceAdapter
{
    Task<List<EcommerceOrder>> FetchNewOrdersAsync(EcommerceStore store);
    Task SyncStockAsync(EcommerceStore store, Item item, decimal quantity);
}
