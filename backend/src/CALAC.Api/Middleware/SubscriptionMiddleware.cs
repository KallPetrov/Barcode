using System.Net;
using CALAC.Infrastructure.Services;

namespace CALAC.Api.Middleware;

public class SubscriptionMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IBillingService billingService, ITenantService tenantService)
    {
        // Skip for anonymous endpoints or specific endpoints
        if (context.Request.Path.StartsWithSegments("/api/auth") ||
            context.Request.Path.StartsWithSegments("/api/billing/webhook") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        try
        {
            var tenantId = tenantService.GetTenantId();
            if (tenantId != null && tenantId != Guid.Empty)
            {
                var subscription = await billingService.GetSubscriptionAsync(tenantId.Value);

                // Example logic: if subscription is not active and not in a trial period (trial logic can be added)
                // For now, we'll just check if a subscription exists and is active
                if (subscription == null || !subscription.IsActive)
                {
                    // Allow billing endpoints even if subscription is inactive
                    if (context.Request.Path.StartsWithSegments("/api/billing"))
                    {
                        await _next(context);
                        return;
                    }

                    context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                    await context.Response.WriteAsJsonAsync(new { message = "Subscription required or expired. Please visit the billing portal." });
                    return;
                }
            }
        }
        catch (Exception)
        {
            // If tenant cannot be identified, we proceed and let other middleware/auth handle it
        }

        await _next(context);
    }
}

public static class SubscriptionMiddlewareExtensions
{
    public static IApplicationBuilder UseSubscriptionCheck(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SubscriptionMiddleware>();
    }
}
