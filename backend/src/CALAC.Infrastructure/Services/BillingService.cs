using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace CALAC.Infrastructure.Services;

public class BillingService : IBillingService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<BillingService> _logger;

    public BillingService(AppDbContext db, IConfiguration config, ILogger<BillingService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
        StripeConfiguration.ApiKey = _config["Stripe:ApiKey"];
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid tenantId, string planCode, string successUrl, string cancelUrl)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId)
            ?? throw new Exception("Tenant not found");

        var subscription = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        var priceId = planCode.ToLower() switch
        {
            "starter" => _config["Stripe:ProductStarterId"],
            "professional" => _config["Stripe:ProductProfessionalId"],
            "enterprise" => _config["Stripe:ProductEnterpriseId"],
            _ => throw new Exception("Invalid plan code")
        };

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1,
                },
            },
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = tenantId.ToString(),
            Customer = subscription?.StripeCustomerId,
            Metadata = new Dictionary<string, string>
            {
                { "plan_code", planCode }
            }
        };

        var service = new SessionService();
        Session session = await service.CreateAsync(options);
        return session.Url;
    }

    public async Task<string> CreateCustomerPortalSessionAsync(Guid tenantId, string returnUrl)
    {
        var subscription = await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId)
            ?? throw new Exception("Subscription not found");

        if (string.IsNullOrEmpty(subscription.StripeCustomerId))
        {
            throw new Exception("Tenant does not have a Stripe Customer ID");
        }

        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = subscription.StripeCustomerId,
            ReturnUrl = returnUrl,
        };

        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(options);
        return session.Url;
    }

    public async Task HandleWebhookAsync(string json, string stripeSignature)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, _config["Stripe:WebhookSecret"]);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                var tenantId = Guid.Parse(session!.ClientReferenceId);
                var planCode = session.Metadata.ContainsKey("plan_code") ? session.Metadata["plan_code"] : "starter";

                var subscription = await _db.TenantSubscriptions
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId);

                if (subscription == null)
                {
                    subscription = new TenantSubscription
                    {
                        TenantId = tenantId,
                        PlanCode = planCode,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.TenantSubscriptions.Add(subscription);
                }

                subscription.PlanCode = planCode;
                subscription.StripeCustomerId = session.CustomerId;
                subscription.StripeSubscriptionId = session.SubscriptionId;
                subscription.IsActive = true;
                subscription.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
            }
            else if (stripeEvent.Type == EventTypes.CustomerSubscriptionDeleted)
            {
                var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
                var subscription = await _db.TenantSubscriptions
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription!.Id);

                if (subscription != null)
                {
                    subscription.IsActive = false;
                    subscription.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
            // Add more event handlers as needed (subscription updated, etc.)
        }
        catch (StripeException e)
        {
            _logger.LogError(e, "Stripe webhook error");
            throw;
        }
    }

    public async Task<TenantSubscription?> GetSubscriptionAsync(Guid tenantId)
    {
        return await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);
    }
}
