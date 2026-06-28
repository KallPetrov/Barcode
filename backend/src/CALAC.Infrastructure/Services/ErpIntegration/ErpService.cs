using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services.ErpIntegration;

public class ErpService(AppDbContext db, AuditService audit)
{
    public async Task<IReadOnlyList<ErpConfiguration>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await db.ErpConfigurations
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<ErpConfiguration?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
    }

    public async Task<ErpConfiguration> CreateAsync(Guid tenantId, CreateErpConfigRequest request, Guid userId, CancellationToken ct = default)
    {
        var config = new ErpConfiguration
        {
            TenantId = tenantId,
            Name = request.Name,
            ProviderType = request.ProviderType,
            ApiUrl = request.ApiUrl,
            ApiKey = request.ApiKey,
            Username = request.Username,
            Password = request.Password,
            DatabaseName = request.DatabaseName,
            IsActive = true,
            AutoSyncItems = request.AutoSyncItems,
            AutoSyncInventory = request.AutoSyncInventory,
            SettingsJson = request.SettingsJson
        };

        db.ErpConfigurations.Add(config);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_CONFIG_CREATED", userId, null, "ErpConfiguration", config.Id.ToString(),
            $"Name={config.Name}, Provider={config.ProviderType}", null, ct);

        return config;
    }

    public async Task<ErpConfiguration> UpdateAsync(Guid tenantId, Guid id, UpdateErpConfigRequest request, Guid userId, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        config.Name = request.Name;
        config.ProviderType = request.ProviderType;
        config.ApiUrl = request.ApiUrl;
        config.ApiKey = request.ApiKey;
        config.Username = request.Username;
        config.Password = request.Password;
        config.DatabaseName = request.DatabaseName;
        config.IsActive = request.IsActive;
        config.AutoSyncItems = request.AutoSyncItems;
        config.AutoSyncInventory = request.AutoSyncInventory;
        config.SettingsJson = request.SettingsJson;
        config.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_CONFIG_UPDATED", userId, null, "ErpConfiguration", config.Id.ToString(),
            $"Name={config.Name}", null, ct);

        return config;
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        db.ErpConfigurations.Remove(config);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_CONFIG_DELETED", userId, null, "ErpConfiguration", config.Id.ToString(),
            $"Name={config.Name}", null, ct);

        return true;
    }

    public async Task<bool> TestConnectionAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        var adapter = CreateAdapter(config);
        return await adapter.TestConnectionAsync(ct);
    }

    public async Task SyncItemsAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        var adapter = CreateAdapter(config);
        await adapter.SyncItemsAsync(ct);
        
        config.LastSyncAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_SYNC_ITEMS", userId, null, "ErpConfiguration", config.Id.ToString(),
            null, null, ct);
    }

    public async Task SyncInventoryAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        var adapter = CreateAdapter(config);
        await adapter.SyncInventoryAsync(ct);
        
        config.LastSyncAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_SYNC_INVENTORY", userId, null, "ErpConfiguration", config.Id.ToString(),
            null, null, ct);
    }

    private IErpAdapter CreateAdapter(ErpConfiguration config)
    {
        return config.ProviderType switch
        {
            ErpProviderType.Odoo => new OdooAdapter(db, config),
            ErpProviderType.Dynamics365 => new DynamicsAdapter(db, config),
            _ => throw new InvalidOperationException($"Unsupported ERP provider: {config.ProviderType}")
        };
    }
}

public record CreateErpConfigRequest(
    string Name,
    ErpProviderType ProviderType,
    string? ApiUrl,
    string? ApiKey,
    string? Username,
    string? Password,
    string? DatabaseName,
    bool AutoSyncItems,
    bool AutoSyncInventory,
    string? SettingsJson);

public record UpdateErpConfigRequest(
    string Name,
    ErpProviderType ProviderType,
    string? ApiUrl,
    string? ApiKey,
    string? Username,
    string? Password,
    string? DatabaseName,
    bool IsActive,
    bool AutoSyncItems,
    bool AutoSyncInventory,
    string? SettingsJson);
