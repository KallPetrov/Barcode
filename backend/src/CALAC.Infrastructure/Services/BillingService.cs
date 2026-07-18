using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
// Stripe integration temporarily disabled due to .NET 8.0 compatibility
// using Stripe;
// using Stripe.Checkout;

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
        // StripeConfiguration.ApiKey = _config["Stripe:ApiKey"];
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid tenantId, string planCode, string successUrl, string cancelUrl)
    {
        // Stripe integration temporarily disabled - requires .NET 10.0+
        // For demo purposes, subscriptions can be managed manually in the database
        throw new NotImplementedException("Stripe integration temporarily disabled for .NET 8.0 compatibility");
    }

    public async Task<string> CreateCustomerPortalSessionAsync(Guid tenantId, string returnUrl)
    {
        // Stripe integration temporarily disabled - requires .NET 10.0+
        throw new NotImplementedException("Stripe integration temporarily disabled for .NET 8.0 compatibility");
    }

    public async Task HandleWebhookAsync(string json, string stripeSignature)
    {
        // Stripe integration temporarily disabled - requires .NET 10.0+
        throw new NotImplementedException("Stripe integration temporarily disabled for .NET 8.0 compatibility");
    }

    public async Task<TenantSubscription?> GetSubscriptionAsync(Guid tenantId)
    {
        return await _db.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);
    }
}
