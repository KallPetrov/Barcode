using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services.ErpIntegration;

public class OdooAdapter(AppDbContext db, ErpConfiguration config) : IErpAdapter
{
    private readonly HttpClient _httpClient = new();

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // TODO: Implement actual Odoo connection test
            // This would use XML-RPC or REST API to connect to Odoo
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
        // TODO: Implement actual Odoo items sync
        // 1. Fetch products from Odoo API
        // 2. Map Odoo products to CALAC Items
        // 3. Upsert into database
        await Task.Delay(100, ct);

        // Mocking some sync activity
        var logger = new List<string> { "Fetching products from Odoo...", "Found 10 new products.", "Syncing SKU-OD-101...", "Sync complete." };
    }

    public async Task SyncInventoryAsync(CancellationToken ct = default)
    {
        // TODO: Implement actual Odoo inventory sync
        // This would fetch stock quant from Odoo and sync to local InventoryStock table
        await Task.Delay(100, ct);
    }

    public async Task PushGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct = default)
    {
        // TODO: Implement actual push to Odoo
        // This would create a picking in Odoo when goods receipt is completed
        await Task.Delay(100, ct);
    }

    public async Task PushTransferOrderAsync(TransferOrder transfer, CancellationToken ct = default)
    {
        // TODO: Implement actual push to Odoo
        // This would create an internal transfer in Odoo
        await Task.Delay(100, ct);
    }
}
