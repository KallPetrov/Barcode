using Microsoft.AspNetCore.Mvc;
using CALAC.Infrastructure.Services.Logistics;
using CALAC.Domain.Entities;

namespace CALAC.Api.Controllers;

[ApiController]
[Route("api/shipping")]
public class ShippingController : ControllerBase
{
    private readonly ShippingService _shippingService;

    public ShippingController(ShippingService shippingService)
    {
        _shippingService = shippingService;
    }

    [HttpGet]
    public async Task<IActionResult> GetShipments()
    {
        var shipments = await _shippingService.GetShipmentsAsync();
        return Ok(shipments);
    }

    [HttpPost]
    public async Task<IActionResult> CreateShipment([FromBody] Shipment shipment)
    {
        var result = await _shippingService.CreateShipmentAsync(shipment);
        return Ok(result);
    }

    [HttpPost("{id}/generate-waybill")]
    public async Task<IActionResult> GenerateWaybill(Guid id)
    {
        var result = await _shippingService.GenerateWaybillAsync(id);
        return Ok(result);
    }
}
