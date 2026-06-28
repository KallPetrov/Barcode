using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CALAC.Infrastructure.Services;

public interface ITenantService
{
    Guid? GetTenantId();
}

public class TenantService(IHttpContextAccessor httpContextAccessor) : ITenantService
{
    public Guid? GetTenantId()
    {
        var tenantIdClaim = httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            return tenantId;
        }
        return null;
    }
}
