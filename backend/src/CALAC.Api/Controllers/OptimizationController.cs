using CALAC.Infrastructure.Services.Optimization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CALAC.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor")]
public class OptimizationController(SlottingService slottingService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet("slotting-recommendations")]
    public async Task<ActionResult<List<SlottingRecommendation>>> GetSlottingRecommendations()
    {
        return Ok(await slottingService.GetRecommendationsAsync(TenantId));
    }
}
