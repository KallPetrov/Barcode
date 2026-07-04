using Microsoft.AspNetCore.Mvc;
using CALAC.Infrastructure.Services;

namespace CALAC.Api.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ITenantService _tenantService;

    public BillingController(IBillingService billingService, ITenantService tenantService)
    {
        _billingService = billingService;
        _tenantService = tenantService;
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        var tenantId = _tenantService.GetTenantId() ?? throw new Exception("Unauthorized");
        var url = await _billingService.CreateCheckoutSessionAsync(tenantId, request.PlanCode, request.SuccessUrl, request.CancelUrl);
        return Ok(new { url });
    }

    [HttpPost("portal")]
    public async Task<IActionResult> CreatePortal([FromBody] CreatePortalRequest request)
    {
        var tenantId = _tenantService.GetTenantId() ?? throw new Exception("Unauthorized");
        var url = await _billingService.CreateCustomerPortalSessionAsync(tenantId, request.ReturnUrl);
        return Ok(new { url });
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var stripeSignature = Request.Headers["Stripe-Signature"];

        try
        {
            await _billingService.HandleWebhookAsync(json, stripeSignature!);
            return Ok();
        }
        catch (Exception)
        {
            return BadRequest();
        }
    }

    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription()
    {
        var tenantId = _tenantService.GetTenantId() ?? throw new Exception("Unauthorized");
        var subscription = await _billingService.GetSubscriptionAsync(tenantId);
        return Ok(subscription);
    }
}

public record CreateCheckoutRequest(string PlanCode, string SuccessUrl, string CancelUrl);
public record CreatePortalRequest(string ReturnUrl);
