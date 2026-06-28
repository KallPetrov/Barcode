using CALAC.Domain.Entities;

namespace CALAC.Infrastructure.Services.ErpIntegration;

public interface IErpAdapter
{
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task SyncItemsAsync(CancellationToken ct = default);
    Task SyncInventoryAsync(CancellationToken ct = default);
    Task PushGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct = default);
    Task PushTransferOrderAsync(TransferOrder transfer, CancellationToken ct = default);
}

public record ErpSyncResult(bool Success, string? Error, int ItemsSynced = 0, int InventorySynced = 0);
