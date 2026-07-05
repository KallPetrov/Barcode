using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;

namespace CALAC.Infrastructure.Services.ErpIntegration;

public class OdooAdapter(AppDbContext db) : IErpAdapter
{
    public string ProviderName => "Odoo";
    public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task SyncItemsAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SyncInventoryAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task PushGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct = default) => Task.CompletedTask;
    public Task PushTransferOrderAsync(TransferOrder transfer, CancellationToken ct = default) => Task.CompletedTask;
}
