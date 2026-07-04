using Microsoft.AspNetCore.Mvc;
using CALAC.Infrastructure.Services.Ecommerce;
using CALAC.Domain.Entities;
using CALAC.Infrastructure.Services;

namespace CALAC.Api.Controllers;

[ApiController]
[Route("api/ecommerce")]
public class EcommerceController : ControllerBase
{
    private readonly EcommerceService _ecommerceService;
    private readonly ITenantService _tenantService;

    public EcommerceController(EcommerceService ecommerceService, ITenantService tenantService)
    {
        _ecommerceService = ecommerceService;
        _tenantService = tenantService;
    }

    [HttpGet("stores")]
    public async Task<IActionResult> GetStores()
    {
        var tenantId = _tenantService.GetTenantId() ?? throw new Exception("Unauthorized");
        var stores = await _ecommerceService.GetStoresAsync(tenantId);
        return Ok(stores);
    }

    [HttpPost("stores")]
    public async Task<IActionResult> CreateStore([FromBody] EcommerceStore store)
    {
        var result = await _ecommerceService.CreateStoreAsync(store);
        return Ok(result);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders()
    {
        var tenantId = _tenantService.GetTenantId() ?? throw new Exception("Unauthorized");
        var orders = await _ecommerceService.GetOrdersAsync(tenantId);
        return Ok(orders);
    }

    [HttpPost("stores/{id}/sync")]
    public async Task<IActionResult> SyncStore(Guid id)
    {
        await _ecommerceService.ImportOrdersAsync(id);
        return Ok();
    }

    [HttpPost("webhooks/{storeId}")]
    public async Task<IActionResult> Webhook(Guid storeId)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        await _ecommerceService.ProcessOrderWebhookAsync(storeId, json);
        return Ok();
    }
}
