using CALAC.Domain.Entities;

namespace CALAC.Infrastructure.Services;

public interface IBillingService
{
    Task<string> CreateCheckoutSessionAsync(Guid tenantId, string planCode, string successUrl, string cancelUrl);
    Task<string> CreateCustomerPortalSessionAsync(Guid tenantId, string returnUrl);
    Task HandleWebhookAsync(string json, string stripeSignature);
    Task<TenantSubscription?> GetSubscriptionAsync(Guid tenantId);
}
