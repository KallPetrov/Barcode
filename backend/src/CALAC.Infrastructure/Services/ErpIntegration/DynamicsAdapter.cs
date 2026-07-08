using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;

namespace CALAC.Infrastructure.Services.ErpIntegration;

public class DynamicsAdapter(AppDbContext db) : IErpAdapter
{
    public string ProviderName => "Dynamics365";
    public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task SyncItemsAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SyncInventoryAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task PushGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct = default) => Task.CompletedTask;
    public Task PushTransferOrderAsync(TransferOrder transfer, CancellationToken ct = default) => Task.CompletedTask;
    public Task PushPickingOrderAsync(PickingOrder picking, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IEnumerable<Item>> ImportItemsAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<Item>());
}
