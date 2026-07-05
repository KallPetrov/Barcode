using CALAC.Domain.Entities;

namespace CALAC.Infrastructure.Services.ExternalSync;

public interface IExternalSyncProvider
{
    string ProviderName { get; }
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task SyncItemsAsync(CancellationToken ct = default);
    Task SyncInventoryAsync(CancellationToken ct = default);
    Task PushGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct = default);
    Task PushTransferOrderAsync(TransferOrder transfer, CancellationToken ct = default);
}
