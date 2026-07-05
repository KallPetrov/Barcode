using CALAC.Domain.Entities;
using CALAC.Infrastructure.Services.ExternalSync;

namespace CALAC.Infrastructure.Services.ErpIntegration;

public interface IErpAdapter : IExternalSyncProvider
{
}

public record ErpSyncResult(bool Success, string? Error, int ItemsSynced = 0, int InventorySynced = 0);
