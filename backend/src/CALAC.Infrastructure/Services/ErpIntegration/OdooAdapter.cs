using System.Net;
using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services.ErpIntegration;

public class OdooAdapter(AppDbContext db, ErpConfiguration config) : IErpAdapter
{
    private readonly HttpClient _httpClient = new();

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ApiUrl))
            return false;

        try
        {
            await ExecuteWithRetryAsync(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, config.ApiUrl);
                if (!string.IsNullOrWhiteSpace(config.Username))
                    request.Headers.Add("X-Odoo-Username", config.Username);
                if (!string.IsNullOrWhiteSpace(config.ApiKey))
                    request.Headers.Add("X-Odoo-Api-Key", config.ApiKey);
                using var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
            }, ct);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SyncItemsAsync(CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, config.ApiUrl);
            if (!string.IsNullOrWhiteSpace(config.Username))
                request.Headers.Add("X-Odoo-Username", config.Username);
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
                request.Headers.Add("X-Odoo-Api-Key", config.ApiKey);
            using var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return;
            response.EnsureSuccessStatusCode();
        }, ct);
    }

    public async Task SyncInventoryAsync(CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await Task.Delay(50, ct);
        }, ct);
    }

    public async Task PushGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await Task.Delay(50, ct);
        }, ct);
    }

    public async Task PushTransferOrderAsync(TransferOrder transfer, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await Task.Delay(50, ct);
        }, ct);
    }

    private static async Task ExecuteWithRetryAsync(Func<Task> action, CancellationToken ct)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                await action();
                return;
            }
            catch when (attempt < 3)
            {
                attempt++;
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
        }
    }
}
