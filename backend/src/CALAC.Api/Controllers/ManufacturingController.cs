using CALAC.Infrastructure.Services.Manufacturing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CALAC.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ManufacturingController(ManufacturingService manufacturingService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpPost("work-orders")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<WorkOrderDto>> CreateWorkOrder([FromBody] CreateWorkOrderRequest request)
    {
        var result = await manufacturingService.CreateWorkOrderAsync(TenantId, request.BomId, request.Quantity, UserId);
        return Ok(result);
    }

    [HttpPost("work-orders/{id:guid}/produce")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<IActionResult> Produce(Guid id, [FromBody] ProduceRequest request)
    {
        await manufacturingService.ProduceAsync(TenantId, id, request.Quantity, request.BatchNumber, request.ExpiryDate, UserId);
        return Ok();
    }
}

public record CreateWorkOrderRequest(Guid BomId, decimal Quantity);
public record ProduceRequest(decimal Quantity, string? BatchNumber, DateTime? ExpiryDate);
