using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services.ErpIntegration;

public class DynamicsAdapter(AppDbContext db, ErpConfiguration config) : IErpAdapter
{
    private readonly HttpClient _httpClient = new();

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // TODO: Implement actual Dynamics 365 connection test
            // This would use OAuth2 and Web API to connect to Dynamics 365
            await Task.Delay(100, ct);
            return !string.IsNullOrEmpty(config.ApiUrl);
        }
        catch
        {
            return false;
        }
    }

    public async Task SyncItemsAsync(CancellationToken ct = default)
    {
        // TODO: Implement actual Dynamics 365 products sync
        // This would fetch products from Dynamics and sync to local Items table
        await Task.Delay(100, ct);
    }

    public async Task SyncInventoryAsync(CancellationToken ct = default)
    {
        // TODO: Implement actual Dynamics 365 inventory sync
        // This would fetch inventory from Dynamics and sync to local InventoryStock table
        await Task.Delay(100, ct);
    }

    public async Task PushGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct = default)
    {
        // TODO: Implement actual push to Dynamics 365
        // This would create a product receipt in Dynamics when goods receipt is completed
        await Task.Delay(100, ct);
    }

    public async Task PushTransferOrderAsync(TransferOrder transfer, CancellationToken ct = default)
    {
        // TODO: Implement actual push to Dynamics 365
        // This would create a transfer order in Dynamics
        await Task.Delay(100, ct);
    }
}
