using CALAC.Infrastructure.Services.Logistics.Returns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CALAC.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReturnsController(ReturnService returnService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpPost]
    public async Task<IActionResult> CreateReturn([FromBody] CreateReturnRequest request)
    {
        var result = await returnService.CreateReturnAsync(TenantId, request, UserId);
        return Ok(result);
    }

    [HttpPost("{id:guid}/process")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> ProcessReturn(Guid id)
    {
        await returnService.ProcessReturnAsync(TenantId, id, UserId);
        return Ok();
    }
}
