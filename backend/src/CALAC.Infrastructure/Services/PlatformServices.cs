using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CALAC.Infrastructure.Services.Logistics;

namespace CALAC.Infrastructure.Services;

public record AuthResult(bool Success, string? Token, string? RefreshToken, UserDto? User, string? Error, bool MustChangePassword = false);

public record UserDto(Guid Id, string Username, string FullName, UserRole Role, Guid TenantId, string TenantName, bool MustChangePassword);

public class AuthService(AppDbContext db, IConfiguration config)
{
    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResult(false, null, null, null, "Невалидно потребителско име или парола");

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var dto = ToDto(user);
        var token = GenerateToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id, ct);
        return new AuthResult(true, token, refreshToken, dto, null, user.MustChangePassword);
    }

    public async Task<AuthResult> LoginWithPinAsync(string username, string pin, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

        if (user is null)
            return new AuthResult(false, null, null, null, "Невалидно потребителско име");

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            return new AuthResult(false, null, null, null, $"Акаунтът е блокиран до {user.LockoutEnd.Value.ToLocalTime():HH:mm:ss}");

        if (user.PinHash is null || !BCrypt.Net.BCrypt.Verify(pin, user.PinHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                user.AccessFailedCount = 0;
            }
            await db.SaveChangesAsync(ct);
            return new AuthResult(false, null, null, null, "Невалиден PIN код");
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync(ct);

        var token = GenerateToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id, ct);
        return new AuthResult(true, token, refreshToken, ToDto(user), null, user.MustChangePassword);
    }

    public async Task<AuthResult> RefreshTokenAsync(string token, CancellationToken ct = default)
    {
        var refreshToken = await db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(r => r.Token == token, ct);

        if (refreshToken is null || !refreshToken.IsActive)
            return new AuthResult(false, null, null, null, "Невалиден refresh token");

        var newRefreshToken = await RotateRefreshTokenAsync(refreshToken, ct);
        var jwtToken = GenerateToken(refreshToken.User);

        return new AuthResult(true, jwtToken, newRefreshToken, ToDto(refreshToken.User), null, refreshToken.User.MustChangePassword);
    }

    public async Task RevokeTokenAsync(string token, CancellationToken ct = default)
    {
        var refreshToken = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, ct);
        if (refreshToken is not null && refreshToken.IsActive)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(double.Parse(config["Jwt:ExpireHours"] ?? "8"));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tenant_id", user.TenantId.ToString()),
            new("full_name", user.FullName),
            new("must_change_password", user.MustChangePassword.ToString().ToLower())
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);
        return token;
    }

    private async Task<string> RotateRefreshTokenAsync(RefreshToken oldToken, CancellationToken ct = default)
    {
        var newToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var refreshToken = new RefreshToken
        {
            UserId = oldToken.UserId,
            Token = newToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        oldToken.RevokedAt = DateTime.UtcNow;
        oldToken.ReplacedByToken = newToken;

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);
        return newToken;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([userId], ct);
        if (user is null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Username, user.FullName, user.Role, user.TenantId, user.Tenant.Name, user.MustChangePassword);
}

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string? UserName,
    Guid? DeviceId,
    string? DeviceName,
    string Action,
    string? EntityType,
    string? EntityId,
    string? Details,
    string? IpAddress,
    DateTime CreatedAt);

public class AuditService(AppDbContext db)
{
    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await db.AuditLogs
            .Include(a => a.User)
            .Include(a => a.Device)
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                a.User != null ? a.User.FullName : null,
                a.DeviceId,
                a.Device != null ? a.Device.Name : null,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.IpAddress,
                a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task LogAsync(
        Guid tenantId,
        string action,
        Guid? userId = null,
        Guid? deviceId = null,
        string? entityType = null,
        string? entityId = null,
        string? details = null,
        string? ipAddress = null,
        CancellationToken ct = default,
        decimal? temperature = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            DeviceId = deviceId,
            Action = action,
            TemperatureCelsius = temperature,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress
        });
        await db.SaveChangesAsync(ct);
    }
}

public record DeviceDto(
    Guid Id,
    string HardwareId,
    string Name,
    string? Manufacturer,
    string? Model,
    string? AppVersion,
    DeviceStatus Status,
    int? BatteryLevel,
    string? AssignedUserName,
    DateTime RegisteredAt,
    DateTime? LastSeenAt);

public record CreateWebhookSubscriptionRequest(string Name, string EventType, string Url, string? Secret, bool IsActive);
public record WebhookSubscriptionDto(Guid Id, string Name, string EventType, string Url, string? Secret, bool IsActive, DateTime? LastSuccessAt, string? LastError, DateTime CreatedAt);

public class WebhookSubscriptionService(AppDbContext db, AuditService audit, HttpClient httpClient)
{
    public async Task<IReadOnlyList<WebhookSubscriptionDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var subscriptions = await db.WebhookSubscriptions
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return subscriptions.Select(MapToDto).ToList();
    }

    public async Task<WebhookSubscriptionDto> CreateAsync(Guid tenantId, CreateWebhookSubscriptionRequest request, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Името на абонамента е задължително");
        if (string.IsNullOrWhiteSpace(request.EventType)) throw new InvalidOperationException("Събитието е задължително");
        if (string.IsNullOrWhiteSpace(request.Url)) throw new InvalidOperationException("URL адресът е задължителен");

        var subscription = new WebhookSubscription
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            EventType = request.EventType.Trim(),
            Url = request.Url.Trim(),
            Secret = request.Secret,
            IsActive = request.IsActive
        };

        db.WebhookSubscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(tenantId, "WEBHOOK_SUBSCRIPTION_CREATED", userId, null, "WebhookSubscription", subscription.Id.ToString(), null, null, ct);

        return MapToDto(subscription);
    }

    public async Task PublishAsync(Guid tenantId, string eventType, object payload, CancellationToken ct = default)
    {
        var subscriptions = await db.WebhookSubscriptions
            .Where(s => s.TenantId == tenantId && s.IsActive && s.EventType == eventType)
            .ToListAsync(ct);

        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        foreach (var subscription in subscriptions)
        {
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
                {
                    Content = content
                };
                request.Headers.Add("X-Webhook-Event", eventType);
                request.Headers.Add("X-Webhook-Tenant", tenantId.ToString());

                if (!string.IsNullOrWhiteSpace(subscription.Secret))
                {
                    request.Headers.Add("X-Webhook-Signature", Convert.ToHexString(Encoding.UTF8.GetBytes(subscription.Secret)));
                }

                using var response = await httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                subscription.LastSuccessAt = DateTime.UtcNow;
                subscription.LastError = null;
                subscription.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                subscription.LastError = ex.Message;
                subscription.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static WebhookSubscriptionDto MapToDto(WebhookSubscription subscription) =>
        new(subscription.Id, subscription.Name, subscription.EventType, subscription.Url, subscription.Secret, subscription.IsActive, subscription.LastSuccessAt, subscription.LastError, subscription.CreatedAt);
}

public record CreateTenantOnboardingRequest(string TenantName, string TenantCode, string AdminUsername, string AdminPassword, string AdminFullName);
public record TenantOnboardingResult(Guid TenantId, string TenantName, string AdminUsername);

public record CreatePartnerApiKeyRequest(string Name, string? Description);
public record PartnerApiKeyDto(Guid Id, string Name, string Key, string? Description, bool IsActive, DateTime CreatedAt, DateTime? LastUsedAt);

public record ForecastPoint(DateTime Timestamp, decimal Quantity);
public record ForecastResult(Guid ItemId, decimal ExpectedDemand, decimal RecommendedReorderPoint);

public class ForecastingService(AppDbContext db, AuditService audit)
{
    public async Task<ForecastResult> GetForecastAsync(Guid tenantId, Guid itemId, int lookbackPeriods = 3, decimal smoothingFactor = 0.5m, CancellationToken ct = default)
    {
        var history = await db.InventoryStocks
            .Where(s => s.TenantId == tenantId && s.ItemId == itemId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(Math.Max(1, lookbackPeriods))
            .Select(s => new ForecastPoint(s.CreatedAt, s.Quantity))
            .ToListAsync(ct);

        if (history.Count == 0)
            throw new KeyNotFoundException("Няма история за този артикул");

        var points = history.OrderBy(p => p.Timestamp).ToList();
        decimal weighted = 0m;
        decimal weightSum = 0m;

        for (var i = 0; i < points.Count; i++)
        {
            var weight = (i + 1) * smoothingFactor;
            weighted += points[i].Quantity * weight;
            weightSum += weight;
        }

        var expectedDemand = weightSum == 0 ? 0m : weighted / weightSum;
        var recommendedReorderPoint = expectedDemand * 1.2m;

        await audit.LogAsync(tenantId, "FORECAST_REQUESTED", null, null, "Forecast", itemId.ToString(), null, null, ct);
        return new ForecastResult(itemId, expectedDemand, recommendedReorderPoint);
    }
}

public record CreateBatchWaveRequest(string Name, IEnumerable<Guid> PickingOrderIds);
public record BatchWaveDto(Guid Id, string Name, IReadOnlyList<Guid> Orders, IReadOnlyList<BatchWaveGroupDto> Groups);
public record BatchWaveGroupDto(string LocationKey, IReadOnlyList<Guid> PickingOrderIds);

public class BatchPickingService(AppDbContext db, AuditService audit)
{
    public async Task<BatchWaveDto> CreateWaveAsync(Guid tenantId, CreateBatchWaveRequest request, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Името на wave-a е задължително");

        var orderIds = request.PickingOrderIds?.Distinct().ToList() ?? [];
        if (orderIds.Count == 0) throw new InvalidOperationException("Трябва да има поне една picking поръчка");

        var orders = await db.PickingOrders
            .Include(o => o.Lines)
            .Where(o => o.TenantId == tenantId && orderIds.Contains(o.Id))
            .ToListAsync(ct);

        var groups = orders
            .SelectMany(o => o.Lines.Where(l => l.SourceLocationId.HasValue).Select(l => new { OrderId = o.Id, LocationKey = l.SourceLocationId!.Value.ToString() }))
            .GroupBy(x => x.LocationKey)
            .Select(g => new BatchWaveGroupDto(g.Key, g.Select(x => x.OrderId).Distinct().ToList()))
            .ToList();

        if (groups.Count == 0)
            groups.Add(new BatchWaveGroupDto("Unassigned", orders.Select(o => o.Id).ToList()));

        var wave = new WaveBatch { Id = Guid.NewGuid(), TenantId = tenantId, Name = request.Name.Trim(), OrdersJson = System.Text.Json.JsonSerializer.Serialize(orderIds) };
        db.Set<WaveBatch>().Add(wave);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(tenantId, "BATCH_WAVE_CREATED", userId, null, "BatchWave", wave.Id.ToString(), null, null, ct);

        return new BatchWaveDto(wave.Id, wave.Name, orderIds, groups);
    }
}

public record ActivateSubscriptionRequest(string PlanCode, int? Days);
public record SubscriptionDto(Guid TenantId, string PlanCode, bool IsActive, DateTime? ExpiresAt, DateTime CreatedAt);
public record UpsertTenantBrandingRequest(string? CompanyName, string? LogoUrl, string? PrimaryColor, string? SecondaryColor, string? FaviconUrl, string? WelcomeMessage);
public record TenantBrandingDto(Guid Id, Guid TenantId, string CompanyName, string? LogoUrl, string PrimaryColor, string SecondaryColor, string? FaviconUrl, string? WelcomeMessage, DateTime CreatedAt, DateTime? UpdatedAt);

public class TenantBrandingService(AppDbContext db, AuditService audit)
{
    public async Task<TenantBrandingDto> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default)
    {
        var branding = await db.TenantBrandings.FirstOrDefaultAsync(b => b.TenantId == tenantId, ct);
        if (branding is null)
        {
            branding = new TenantBranding { TenantId = tenantId, CompanyName = "CALAC" };
            db.TenantBrandings.Add(branding);
            await db.SaveChangesAsync(ct);
        }

        return Map(branding);
    }

    public async Task<TenantBrandingDto> UpsertAsync(Guid tenantId, UpsertTenantBrandingRequest request, Guid userId, CancellationToken ct = default)
    {
        var branding = await db.TenantBrandings.FirstOrDefaultAsync(b => b.TenantId == tenantId, ct) ?? new TenantBranding { TenantId = tenantId };
        branding.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? "CALAC" : request.CompanyName.Trim();
        branding.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        branding.PrimaryColor = string.IsNullOrWhiteSpace(request.PrimaryColor) ? "#2563eb" : request.PrimaryColor.Trim();
        branding.SecondaryColor = string.IsNullOrWhiteSpace(request.SecondaryColor) ? "#0f172a" : request.SecondaryColor.Trim();
        branding.FaviconUrl = string.IsNullOrWhiteSpace(request.FaviconUrl) ? null : request.FaviconUrl.Trim();
        branding.WelcomeMessage = string.IsNullOrWhiteSpace(request.WelcomeMessage) ? null : request.WelcomeMessage.Trim();
        branding.UpdatedAt = DateTime.UtcNow;

        if (branding.Id == Guid.Empty)
        {
            branding.Id = Guid.NewGuid();
            db.TenantBrandings.Add(branding);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "TENANT_BRANDING_UPDATED", userId, null, "TenantBranding", branding.Id.ToString(), null, null, ct);
        return Map(branding);
    }

    private static TenantBrandingDto Map(TenantBranding branding) => new(branding.Id, branding.TenantId, branding.CompanyName, branding.LogoUrl, branding.PrimaryColor, branding.SecondaryColor, branding.FaviconUrl, branding.WelcomeMessage, branding.CreatedAt, branding.UpdatedAt);
}

public class SubscriptionService(AppDbContext db, AuditService audit)
{
    public async Task<SubscriptionDto> ActivatePlanAsync(Guid tenantId, ActivateSubscriptionRequest request, Guid userId, CancellationToken ct = default)
    {
        var planCode = string.IsNullOrWhiteSpace(request.PlanCode) ? "starter" : request.PlanCode.Trim().ToLowerInvariant();
        var days = request.Days ?? 30;
        var expiresAt = DateTime.UtcNow.AddDays(days);

        var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct) ?? new TenantSubscription { TenantId = tenantId };
        subscription.PlanCode = planCode;
        subscription.IsActive = true;
        subscription.ExpiresAt = expiresAt;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (subscription.Id == Guid.Empty)
        {
            subscription.Id = Guid.NewGuid();
            db.TenantSubscriptions.Add(subscription);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "SUBSCRIPTION_ACTIVATED", userId, null, "TenantSubscription", subscription.Id.ToString(), null, null, ct);
        return new SubscriptionDto(subscription.TenantId, subscription.PlanCode, subscription.IsActive, subscription.ExpiresAt, subscription.CreatedAt);
    }
}

public class TenantOnboardingService(AppDbContext db, AuditService audit)
{
    public async Task<TenantOnboardingResult> CreateAsync(CreateTenantOnboardingRequest request, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TenantName)) throw new InvalidOperationException("Името на клиента е задължително");
        if (string.IsNullOrWhiteSpace(request.TenantCode)) throw new InvalidOperationException("Кодът на клиента е задължителен");
        if (string.IsNullOrWhiteSpace(request.AdminUsername)) throw new InvalidOperationException("Потребителското име е задължително");
        if (string.IsNullOrWhiteSpace(request.AdminPassword)) throw new InvalidOperationException("Паролата е задължителна");

        if (await db.Tenants.AnyAsync(t => t.Code == request.TenantCode, ct))
            throw new InvalidOperationException("Този код за клиент вече е зает");

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Code = request.TenantCode.Trim().ToUpperInvariant(),
            IsActive = true
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        var admin = new User
        {
            TenantId = tenant.Id,
            Username = request.AdminUsername.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            FullName = string.IsNullOrWhiteSpace(request.AdminFullName) ? request.AdminUsername.Trim() : request.AdminFullName.Trim(),
            Role = UserRole.Admin,
            IsActive = true,
            Tenant = tenant
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(tenant.Id, "TENANT_ONBOARDED", userId, null, "Tenant", tenant.Id.ToString(), null, null, ct);

        return new TenantOnboardingResult(tenant.Id, tenant.Name, admin.Username);
    }
}

public class PartnerApiKeyService(AppDbContext db, AuditService audit)
{
    public async Task<PartnerApiKeyDto> CreateAsync(Guid tenantId, CreatePartnerApiKeyRequest request, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Името на API ключа е задължително");

        var key = $"pk_{Guid.NewGuid():N}";
        var entity = new PartnerApiKey
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Key = key,
            Description = request.Description,
            IsActive = true
        };

        db.PartnerApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "PARTNER_API_KEY_CREATED", userId, null, "PartnerApiKey", entity.Id.ToString(), null, null, ct);

        return new PartnerApiKeyDto(entity.Id, entity.Name, entity.Key, entity.Description, entity.IsActive, entity.CreatedAt, entity.LastUsedAt);
    }

    public async Task<bool> ValidateAsync(Guid tenantId, string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        var entity = await db.PartnerApiKeys.FirstOrDefaultAsync(k => k.TenantId == tenantId && k.IsActive && k.Key == key, ct);
        if (entity is null) return false;

        entity.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public class DeviceService(AppDbContext db, AuditService audit)
{
    public async Task<DeviceDto> RegisterOrUpdateAsync(
        Guid tenantId,
        Guid? userId,
        RegisterDeviceRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var device = await db.Devices
            .Include(d => d.AssignedUser)
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.HardwareId == request.HardwareId, ct);

        if (device is null)
        {
            device = new Device
            {
                TenantId = tenantId,
                HardwareId = request.HardwareId,
                Name = request.Name ?? request.HardwareId,
                Manufacturer = request.Manufacturer,
                Model = request.Model,
                OsVersion = request.OsVersion,
                AppVersion = request.AppVersion,
                Status = DeviceStatus.Online,
                BatteryLevel = request.BatteryLevel,
                AssignedUserId = userId,
                LastSeenAt = DateTime.UtcNow
            };
            db.Devices.Add(device);
            await audit.LogAsync(tenantId, "DEVICE_REGISTERED", userId, null, "Device", device.Id.ToString(),
                $"HardwareId={request.HardwareId}", ipAddress, ct);
        }
        else
        {
            device.Name = request.Name ?? device.Name;
            device.Manufacturer = request.Manufacturer ?? device.Manufacturer;
            device.Model = request.Model ?? device.Model;
            device.OsVersion = request.OsVersion ?? device.OsVersion;
            device.AppVersion = request.AppVersion ?? device.AppVersion;
            device.BatteryLevel = request.BatteryLevel ?? device.BatteryLevel;
            device.Status = DeviceStatus.Online;
            device.LastSeenAt = DateTime.UtcNow;
            if (userId.HasValue)
                device.AssignedUserId = userId;
            await audit.LogAsync(tenantId, "DEVICE_HEARTBEAT", userId, device.Id, "Device", device.Id.ToString(),
                null, ipAddress, ct);
        }

        await db.SaveChangesAsync(ct);
        return ToDto(await db.Devices.Include(d => d.AssignedUser)
            .FirstAsync(d => d.Id == device.Id, ct));
    }

    public async Task<IReadOnlyList<DeviceDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var devices = await db.Devices
            .Include(d => d.AssignedUser)
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(ct);

        return devices.Select(ToDto).ToList();
    }

    public async Task<DeviceDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var device = await db.Devices.Include(d => d.AssignedUser)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);
        return device is null ? null : ToDto(device);
    }

    public async Task<bool> RevokeAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);
        if (device is null) return false;

        device.Status = DeviceStatus.Offline;
        device.AssignedUserId = null;
        // In a real scenario, we might want to flag it as 'Blacklisted' or 'Inactive'
        // For now, let's just set status to Maintenance or similar, but let's use status to block sync.
        device.Status = DeviceStatus.Maintenance;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "DEVICE_REVOKED", userId, device.Id, "Device", device.Id.ToString(), null, null, ct);
        return true;
    }

    private static DeviceDto ToDto(Device d) =>
        new(d.Id, d.HardwareId, d.Name, d.Manufacturer, d.Model, d.AppVersion, d.Status,
            d.BatteryLevel, d.AssignedUser?.FullName, d.RegisteredAt, d.LastSeenAt);
}

public record RegisterDeviceRequest(
    string HardwareId,
    string? Name,
    string? Manufacturer,
    string? Model,
    string? OsVersion,
    string? AppVersion,
    int? BatteryLevel);

public record SyncPushRequest(IReadOnlyList<SyncOperationItem> Operations);
public record SyncOperationItem(string ClientOperationId, string OperationType, string PayloadJson, DateTime? ClientTimestamp, int? Version);
public record SyncPushResultItem(string ClientOperationId, bool Success, string? ErrorMessage);
public record SyncPushResponse(IReadOnlyList<SyncPushResultItem> Results);

public class SyncService(
    AppDbContext db,
    AuditService audit,
    InventoryService inventory,
    IServiceProvider serviceProvider)
{
    public async Task<SyncPushResponse> PushAsync(
        Guid tenantId,
        Guid deviceId,
        Guid? userId,
        SyncPushRequest request,
        string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var results = new List<SyncPushResultItem>();

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingBatch = await db.SyncOperations
                .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.IdempotencyKey == idempotencyKey, ct);

            if (existingBatch is not null)
            {
                results.AddRange(request.Operations.Select(op => new SyncPushResultItem(op.ClientOperationId, true, "Повторен request е игнориран")));
                await audit.LogAsync(tenantId, "SYNC_PUSH_DUPLICATE", userId, deviceId, "Sync",
                    request.Operations.Count.ToString(), null, null, ct);
                return new SyncPushResponse(results);
            }

            var batchOperation = new SyncOperation
            {
                TenantId = tenantId,
                DeviceId = deviceId,
                ClientOperationId = $"idem:{idempotencyKey}",
                IdempotencyKey = idempotencyKey,
                OperationType = "SYNC_BATCH",
                Status = SyncOperationStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            };
            db.SyncOperations.Add(batchOperation);

            foreach (var op in request.Operations)
            {
                try
                {
                    await ProcessOperationAsync(tenantId, userId, op, ct);
                    results.Add(new SyncPushResultItem(op.ClientOperationId, true, null));
                }
                catch (Exception ex)
                {
                    results.Add(new SyncPushResultItem(op.ClientOperationId, false, ex.Message));
                }
            }

            await db.SaveChangesAsync(ct);
            await audit.LogAsync(tenantId, "SYNC_PUSH", userId, deviceId, "Sync",
                request.Operations.Count.ToString(), null, null, ct);
            return new SyncPushResponse(results);
        }

        foreach (var op in request.Operations)
        {
            var existing = await db.SyncOperations
                .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.ClientOperationId == op.ClientOperationId, ct);

            if (existing is not null)
            {
                results.Add(new SyncPushResultItem(op.ClientOperationId, existing.Status != SyncOperationStatus.Failed,
                    existing.ErrorMessage));
                continue;
            }

            var syncOp = new SyncOperation
            {
                TenantId = tenantId,
                DeviceId = deviceId,
                ClientOperationId = op.ClientOperationId,
                OperationType = op.OperationType,
                PayloadJson = op.PayloadJson,
                ClientTimestamp = op.ClientTimestamp,
                Version = op.Version ?? 1,
                Status = SyncOperationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await ProcessOperationAsync(tenantId, userId, op, ct);
                syncOp.Status = SyncOperationStatus.Completed;
                syncOp.ProcessedAt = DateTime.UtcNow;
                results.Add(new SyncPushResultItem(op.ClientOperationId, true, null));
            }
            catch (Exception ex)
            {
                syncOp.Status = SyncOperationStatus.Failed;
                syncOp.ErrorMessage = ex.Message;
                results.Add(new SyncPushResultItem(op.ClientOperationId, false, ex.Message));
            }

            db.SyncOperations.Add(syncOp);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "SYNC_PUSH", userId, deviceId, "Sync",
            request.Operations.Count.ToString(), null, null, ct);

        return new SyncPushResponse(results);
    }

    private async Task ProcessOperationAsync(Guid tenantId, Guid? userId, SyncOperationItem op, CancellationToken ct)
    {
        var syncOp = new SyncOperation
        {
            TenantId = tenantId,
            DeviceId = Guid.Empty,
            ClientOperationId = op.ClientOperationId,
            OperationType = op.OperationType,
            PayloadJson = op.PayloadJson,
            ClientTimestamp = op.ClientTimestamp,
            Version = op.Version ?? 1
        };

        switch (syncOp.OperationType)
        {
            case "STOCK_ADD":
                var stockRequest = System.Text.Json.JsonSerializer.Deserialize<AddStockRequest>(syncOp.PayloadJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (stockRequest != null)
                {
                    var existingStock = await db.InventoryStocks.IgnoreQueryFilters().FirstOrDefaultAsync(s =>
                        s.TenantId == tenantId &&
                        s.ItemId == stockRequest.ItemId &&
                        s.LocationId == stockRequest.LocationId &&
                        s.BatchNumber == stockRequest.BatchNumber &&
                        s.SerialNumber == stockRequest.SerialNumber, ct);

                    if (existingStock != null && syncOp.Version > 0 && syncOp.Version <= existingStock.Version)
                    {
                        if (syncOp.ClientTimestamp.HasValue && existingStock.UpdatedAt.HasValue && syncOp.ClientTimestamp.Value < existingStock.UpdatedAt.Value)
                        {
                             return;
                        }
                    }
                    await inventory.AddStockAsync(tenantId, stockRequest, userId ?? Guid.Empty, ct);
                }
                break;
            case "TRANSFER_LINE_UPDATE":
                var transferLineReq = System.Text.Json.JsonSerializer.Deserialize<UpdateTransferLineRequest>(syncOp.PayloadJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (transferLineReq != null)
                {
                    var lineId = Guid.Parse(syncOp.ClientOperationId);
                    var existingLine = await db.TransferOrderLines.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId, ct);
                    if (existingLine != null && syncOp.Version > 0 && syncOp.Version <= existingLine.Version)
                    {
                        if (syncOp.ClientTimestamp.HasValue && existingLine.UpdatedAt.HasValue && syncOp.ClientTimestamp.Value < existingLine.UpdatedAt.Value)
                        {
                            return;
                        }
                    }

                    var transferService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TransferService>(serviceProvider);
                    await transferService.UpdateLineAsync(tenantId, lineId, transferLineReq, userId ?? Guid.Empty, ct);
                }
                break;
            case "PICKING_LINE_UPDATE":
                var pickingLineReq = System.Text.Json.JsonSerializer.Deserialize<UpdatePickingLineRequest>(syncOp.PayloadJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (pickingLineReq != null)
                {
                    var lineId = Guid.Parse(syncOp.ClientOperationId);
                    var existingLine = await db.PickingStockLines.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId, ct);
                    if (existingLine != null && syncOp.Version > 0 && syncOp.Version <= existingLine.Version)
                    {
                        if (syncOp.ClientTimestamp.HasValue && existingLine.UpdatedAt.HasValue && syncOp.ClientTimestamp.Value < existingLine.UpdatedAt.Value)
                        {
                            return;
                        }
                    }

                    var pickingService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<PickingService>(serviceProvider);
                    await pickingService.UpdateStockLineAsync(tenantId, lineId, pickingLineReq.PickedQuantity, userId ?? Guid.Empty, pickingLineReq.OverrideReason, pickingLineReq.TemperatureCelsius, ct);
                }
                break;
            default:
                break;
        }
    }

    public async Task<object> GetStatusAsync(Guid tenantId, Guid deviceId, CancellationToken ct = default)
    {
        var pending = await db.SyncOperations
            .CountAsync(s => s.DeviceId == deviceId && s.Status == SyncOperationStatus.Pending, ct);

        return new { deviceId, pendingOperations = pending, serverTime = DateTime.UtcNow };
    }
}

public class UserManagementService(AppDbContext db, AuditService audit)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.Users
            .Include(u => u.Tenant)
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.Username)
            .Select(u => new UserDto(u.Id, u.Username, u.FullName, u.Role, u.TenantId, u.Tenant.Name, u.MustChangePassword))
            .ToListAsync(ct);

    public async Task<UserDto> CreateAsync(Guid tenantId, CreateUserRequest request, Guid adminId, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.TenantId == tenantId && u.Username == request.Username, ct))
            throw new InvalidOperationException("Потребителското име вече съществува");

        if (request.Password.Length < 8 || !request.Password.Any(char.IsUpper) || !request.Password.Any(char.IsDigit))
            throw new InvalidOperationException("Паролата трябва да бъде поне 8 символа и да съдържа главна буква и цифра");

        var user = new User
        {
            TenantId = tenantId,
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            PinHash = request.Pin is not null ? BCrypt.Net.BCrypt.HashPassword(request.Pin) : null,
            FullName = request.FullName,
            Role = request.Role,
            MustChangePassword = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "USER_CREATED", adminId, null, "User", user.Id.ToString(),
            $"Username={user.Username}", null, ct);

        var tenant = await db.Tenants.FindAsync([tenantId], ct);
        return new UserDto(user.Id, user.Username, user.FullName, user.Role, user.TenantId, tenant!.Name, user.MustChangePassword);
    }
}

public record CreateUserRequest(string Username, string Password, string FullName, UserRole Role, string? Pin);

public record LocationDto(Guid Id, string Code, string Name, string? Zone, string? Aisle, string? Rack, string? Level, string? Position, bool IsActive, DateTime CreatedAt);

public class LocationService(AppDbContext db, AuditService audit)
{
    public async Task<IReadOnlyList<LocationDto>> ListAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.Locations
            .Where(l => l.TenantId == tenantId)
            .OrderBy(l => l.Code)
            .Select(l => new LocationDto(l.Id, l.Code, l.Name, l.Zone, l.Aisle, l.Rack, l.Level, l.Position, l.IsActive, l.CreatedAt))
            .ToListAsync(ct);

    public async Task<LocationDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId, ct);
        return location is null ? null : new LocationDto(location.Id, location.Code, location.Name, location.Zone, location.Aisle, location.Rack, location.Level, location.Position, location.IsActive, location.CreatedAt);
    }

    public async Task<LocationDto> CreateAsync(Guid tenantId, CreateLocationRequest request, Guid userId, CancellationToken ct = default)
    {
        if (await db.Locations.AnyAsync(l => l.TenantId == tenantId && l.Code == request.Code, ct))
            throw new InvalidOperationException("Кодът на локацията вече съществува");

        var location = new Location
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            Zone = request.Zone,
            Aisle = request.Aisle,
            Rack = request.Rack,
            Level = request.Level,
            Position = request.Position,
            IsActive = true
        };

        db.Locations.Add(location);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "LOCATION_CREATED", userId, null, "Location", location.Id.ToString(),
            $"Code={location.Code}", null, ct);

        return new LocationDto(location.Id, location.Code, location.Name, location.Zone, location.Aisle, location.Rack, location.Level, location.Position, location.IsActive, location.CreatedAt);
    }

    public async Task<LocationDto> UpdateAsync(Guid tenantId, Guid id, UpdateLocationRequest request, Guid userId, CancellationToken ct = default)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId, ct);
        if (location is null)
            throw new KeyNotFoundException("Локацията не е намерена");

        if (location.Code != request.Code && await db.Locations.AnyAsync(l => l.TenantId == tenantId && l.Code == request.Code, ct))
            throw new InvalidOperationException("Кодът на локацията вече съществува");

        location.Code = request.Code;
        location.Name = request.Name;
        location.Zone = request.Zone;
        location.Aisle = request.Aisle;
        location.Rack = request.Rack;
        location.Level = request.Level;
        location.Position = request.Position;
        location.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "LOCATION_UPDATED", userId, null, "Location", location.Id.ToString(),
            $"Code={location.Code}", null, ct);

        return new LocationDto(location.Id, location.Code, location.Name, location.Zone, location.Aisle, location.Rack, location.Level, location.Position, location.IsActive, location.CreatedAt);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId, ct);
        if (location is null)
            throw new KeyNotFoundException("Локацията не е намерена");

        db.Locations.Remove(location);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "LOCATION_DELETED", userId, null, "Location", location.Id.ToString(),
            $"Code={location.Code}", null, ct);

        return true;
    }
}

public record CreateLocationRequest(string Code, string Name, string? Zone, string? Aisle, string? Rack, string? Level, string? Position);
public record UpdateLocationRequest(string Code, string Name, string? Zone, string? Aisle, string? Rack, string? Level, string? Position, bool IsActive);

public record ItemDto(Guid Id, string Sku, string Name, string? Description, string? Barcode, BarcodeSymbology BarcodeType, string? ImageUrl, decimal? Weight, string? UnitOfMeasure, bool IsActive, DateTime CreatedAt, string? DefaultPickingStrategy = "FIFO", int? MinShelfLifeDays = null);
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public class ItemService(AppDbContext db, AuditService audit)
{
    public async Task<PagedResult<ItemDto>> ListAsync(Guid tenantId, int page = 1, int pageSize = 50, string? search = null, string? sortBy = "sku", string? sortDirection = "asc", CancellationToken ct = default)
    {
        var query = db.Items
            .Where(i => i.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(i => i.Sku.Contains(normalized) || i.Name.Contains(normalized) || (i.Barcode != null && i.Barcode.Contains(normalized)));
        }

        query = sortBy?.ToLowerInvariant() switch
        {
            "name" => sortDirection == "desc" ? query.OrderByDescending(i => i.Name) : query.OrderBy(i => i.Name),
            "createdat" => sortDirection == "desc" ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
            _ => sortDirection == "desc" ? query.OrderByDescending(i => i.Sku) : query.OrderBy(i => i.Sku)
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ItemDto(i.Id, i.Sku, i.Name, i.Description, i.Barcode, i.BarcodeType, i.ImageUrl, i.Weight, i.UnitOfMeasure, i.IsActive, i.CreatedAt, i.DefaultPickingStrategy.ToString(), i.MinShelfLifeDays))
            .ToListAsync(ct);

        return new PagedResult<ItemDto>(items, totalCount, page, pageSize);
    }

    public async Task<ItemDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
        return item is null ? null : new ItemDto(item.Id, item.Sku, item.Name, item.Description, item.Barcode, item.BarcodeType, item.ImageUrl, item.Weight, item.UnitOfMeasure, item.IsActive, item.CreatedAt, item.DefaultPickingStrategy.ToString(), item.MinShelfLifeDays);
    }

    public async Task<ItemDto?> GetByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default)
    {
        var item = await db.Items.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Barcode == barcode, ct);
        return item is null ? null : new ItemDto(item.Id, item.Sku, item.Name, item.Description, item.Barcode, item.BarcodeType, item.ImageUrl, item.Weight, item.UnitOfMeasure, item.IsActive, item.CreatedAt, item.DefaultPickingStrategy.ToString(), item.MinShelfLifeDays);
    }

    public async Task<ItemDto> CreateAsync(Guid tenantId, CreateItemRequest request, Guid userId, CancellationToken ct = default)
    {
        // Use IgnoreQueryFilters because we might be calling this from a webhook without a logged-in user
        if (await db.Items.IgnoreQueryFilters().AnyAsync(i => i.TenantId == tenantId && i.Sku == request.Sku, ct))
            throw new InvalidOperationException("SKU вече съществува");

        var item = new Item
        {
            TenantId = tenantId,
            Sku = request.Sku,
            Name = request.Name,
            Description = request.Description,
            Barcode = request.Barcode,
            BarcodeType = (!string.IsNullOrEmpty(request.BarcodeType) && Enum.TryParse<BarcodeSymbology>(request.BarcodeType, true, out var symbology)) ? symbology : BarcodeSymbology.Unknown,
            ImageUrl = request.ImageUrl,
            Weight = request.Weight,
            UnitOfMeasure = request.UnitOfMeasure,
            DefaultPickingStrategy = (!string.IsNullOrEmpty(request.DefaultPickingStrategy) && Enum.TryParse<PickingStrategy>(request.DefaultPickingStrategy, true, out var strategy)) ? strategy : PickingStrategy.FIFO,
            MinShelfLifeDays = request.MinShelfLifeDays,
            IsActive = true
        };

        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ITEM_CREATED", userId, null, "Item", item.Id.ToString(),
            $"Sku={item.Sku}", null, ct);

        return new ItemDto(item.Id, item.Sku, item.Name, item.Description, item.Barcode, item.BarcodeType, item.ImageUrl, item.Weight, item.UnitOfMeasure, item.IsActive, item.CreatedAt, item.DefaultPickingStrategy.ToString(), item.MinShelfLifeDays);
    }

    public async Task<ItemDto> UpdateAsync(Guid tenantId, Guid id, UpdateItemRequest request, Guid userId, CancellationToken ct = default)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
        if (item is null)
            throw new KeyNotFoundException("Артикулът не е намерен");

        if (item.Sku != request.Sku && await db.Items.AnyAsync(i => i.TenantId == tenantId && i.Sku == request.Sku, ct))
            throw new InvalidOperationException("SKU вече съществува");

        item.Sku = request.Sku;
        item.Name = request.Name;
        item.Description = request.Description;
        item.Barcode = request.Barcode;
        if (!string.IsNullOrEmpty(request.BarcodeType) && Enum.TryParse<BarcodeSymbology>(request.BarcodeType, true, out var symbology))
        {
            item.BarcodeType = symbology;
        }
        else
        {
            item.BarcodeType = BarcodeSymbology.Unknown;
        }
        item.ImageUrl = request.ImageUrl;
        item.Weight = request.Weight;
        item.UnitOfMeasure = request.UnitOfMeasure;
        if (!string.IsNullOrEmpty(request.DefaultPickingStrategy) && Enum.TryParse<PickingStrategy>(request.DefaultPickingStrategy, true, out var pickingStrategy))
            item.DefaultPickingStrategy = pickingStrategy;
        item.MinShelfLifeDays = request.MinShelfLifeDays;
        item.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ITEM_UPDATED", userId, null, "Item", item.Id.ToString(),
            $"Sku={item.Sku}", null, ct);

        return new ItemDto(item.Id, item.Sku, item.Name, item.Description, item.Barcode, item.BarcodeType, item.ImageUrl, item.Weight, item.UnitOfMeasure, item.IsActive, item.CreatedAt, item.DefaultPickingStrategy.ToString(), item.MinShelfLifeDays);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId, ct);
        if (item is null)
            throw new KeyNotFoundException("Артикулът не е намерен");

        db.Items.Remove(item);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ITEM_DELETED", userId, null, "Item", item.Id.ToString(),
            $"Sku={item.Sku}", null, ct);

        return true;
    }
}

public record CreateItemRequest(string Sku, string Name, string? Description, string? Barcode, string? BarcodeType, string? ImageUrl, decimal? Weight, string? UnitOfMeasure, string? DefaultPickingStrategy, int? MinShelfLifeDays);
public record UpdateItemRequest(string Sku, string Name, string? Description, string? Barcode, string? BarcodeType, string? ImageUrl, decimal? Weight, string? UnitOfMeasure, bool IsActive, string? DefaultPickingStrategy, int? MinShelfLifeDays);

public record InventoryStockDto(Guid Id, Guid ItemId, string ItemName, Guid LocationId, string LocationName, decimal Quantity, decimal? ReservedQuantity, string? BatchNumber, string? SerialNumber, DateTime? ExpiryDate, DateTime? ProductionDate, DateTime? BestBeforeDate, DateTime? ReceiptDate, string Status, DateTime CreatedAt, DateTime? UpdatedAt);

public class InventoryService(AppDbContext db, AuditService audit, IServiceProvider serviceProvider)
{
    public async Task<IReadOnlyList<InventoryStockDto>> ListStockAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.InventoryStocks
            .Include(s => s.Item)
            .Include(s => s.Location)
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Item.Name)
            .Select(s => new InventoryStockDto(s.Id, s.ItemId, s.Item.Name, s.LocationId, s.Location.Name, s.Quantity, s.ReservedQuantity, s.BatchNumber, s.SerialNumber, s.ExpiryDate, s.ProductionDate, s.BestBeforeDate, s.ReceiptDate, s.Status.ToString(), s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);

    public async Task<InventoryStockDto> AddStockAsync(Guid tenantId, AddStockRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
            throw new InvalidOperationException("Количество трябва да бъде по-голямо от 0");

        var item = await db.Items.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == request.ItemId && i.TenantId == tenantId, ct);
        if (item is null)
            throw new KeyNotFoundException("Артикулът не е намерен");

        var location = await db.Locations.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == request.LocationId && l.TenantId == tenantId, ct);
        if (location is null)
            throw new KeyNotFoundException("Локацията не е намерена");

        if (request.ExpiryDate.HasValue)
        {
            if (request.ExpiryDate.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("Не може да се приема стока с изтекъл срок на годност");

            if (item.MinShelfLifeDays.HasValue)
            {
                var remainingDays = (request.ExpiryDate.Value - DateTime.UtcNow).TotalDays;
                if (remainingDays < item.MinShelfLifeDays.Value)
                    throw new InvalidOperationException($"Стоката не отговаря на минималния остатъчен срок на годност ({item.MinShelfLifeDays.Value} дни)");
            }
        }

        var batchNumber = string.IsNullOrWhiteSpace(request.BatchNumber) ? null : request.BatchNumber.Trim();
        var serialNumber = string.IsNullOrWhiteSpace(request.SerialNumber) ? null : request.SerialNumber.Trim();

        var existingStock = await db.InventoryStocks.IgnoreQueryFilters().FirstOrDefaultAsync(s =>
            s.TenantId == tenantId &&
            s.ItemId == request.ItemId &&
            s.LocationId == request.LocationId &&
            s.BatchNumber == batchNumber &&
            s.SerialNumber == serialNumber, ct);

        if (existingStock is not null)
        {
            // Simple version conflict check for Sync if version is provided in a more advanced request
            // For standard AddStock, we just increment.
            existingStock.Quantity += request.Quantity;
            existingStock.UpdatedAt = DateTime.UtcNow;
            existingStock.ExpiryDate ??= request.ExpiryDate; existingStock.ProductionDate ??= request.ProductionDate; existingStock.BestBeforeDate ??= request.BestBeforeDate; existingStock.Status = string.IsNullOrEmpty(request.Status) ? existingStock.Status : Enum.Parse<StockStatus>(request.Status, true);
            existingStock.Version++;

            await db.SaveChangesAsync(ct);
            await audit.LogAsync(tenantId, "STOCK_UPDATED", userId, null, "InventoryStock", existingStock.Id.ToString(),
                $"ItemId={existingStock.ItemId}, LocationId={existingStock.LocationId}, Quantity={existingStock.Quantity}", null, ct);

        await BroadcastUpdateAsync(tenantId, new { type = "STOCK_UPDATED", itemId = existingStock.ItemId, sku = item.Sku }, ct);

            return new InventoryStockDto(existingStock.Id, existingStock.ItemId, item.Name, existingStock.LocationId, location.Name, existingStock.Quantity, existingStock.ReservedQuantity, existingStock.BatchNumber, existingStock.SerialNumber, existingStock.ExpiryDate, existingStock.ProductionDate, existingStock.BestBeforeDate, existingStock.ReceiptDate, existingStock.Status.ToString(), existingStock.CreatedAt, existingStock.UpdatedAt);
        }

        var stock = new InventoryStock
        {
            TenantId = tenantId,
            ItemId = request.ItemId,
            LocationId = request.LocationId,
            Quantity = request.Quantity,
            ReservedQuantity = 0,
            BatchNumber = batchNumber,
            SerialNumber = serialNumber,
            ExpiryDate = request.ExpiryDate, ProductionDate = request.ProductionDate, BestBeforeDate = request.BestBeforeDate, ReceiptDate = DateTime.UtcNow, Status = string.IsNullOrEmpty(request.Status) ? StockStatus.Active : Enum.Parse<StockStatus>(request.Status, true),
            UpdatedAt = DateTime.UtcNow
        };

        db.InventoryStocks.Add(stock);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "STOCK_ADDED", userId, null, "InventoryStock", stock.Id.ToString(),
            $"ItemId={stock.ItemId}, LocationId={stock.LocationId}, Quantity={stock.Quantity}", null, ct);

        await BroadcastUpdateAsync(tenantId, new { type = "STOCK_ADDED", itemId = stock.ItemId, sku = item.Sku }, ct);

        return new InventoryStockDto(stock.Id, stock.ItemId, item.Name, stock.LocationId, location.Name, stock.Quantity, stock.ReservedQuantity, stock.BatchNumber, stock.SerialNumber, stock.ExpiryDate, stock.ProductionDate, stock.BestBeforeDate, stock.ReceiptDate, stock.Status.ToString(), stock.CreatedAt, stock.UpdatedAt);
    }

    public async Task BroadcastUpdateAsync(Guid tenantId, object payload, CancellationToken ct = default)
    {
        var hubContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IHubContext<CALAC.Infrastructure.Hubs.WarehouseHub>>(serviceProvider);
        if (hubContext?.Clients != null)
        {
            await hubContext.Clients.Group(tenantId.ToString()).SendAsync("WarehouseEvent", payload, ct);
        }
    }
}

public record AddStockRequest(Guid ItemId, Guid LocationId, decimal Quantity, string? BatchNumber, string? SerialNumber, DateTime? ExpiryDate, DateTime? ProductionDate, DateTime? BestBeforeDate, string? Status);

public record InventorySessionDto(Guid Id, string Name, string? Description, string Status, DateTime? StartedAt, DateTime? CompletedAt, Guid? StartedByUserId, string? StartedByUserName, DateTime CreatedAt);
public record InventoryCountDto(Guid Id, Guid SessionId, Guid ItemId, string ItemName, Guid LocationId, string LocationName, decimal SystemQuantity, decimal? CountedQuantity, string? BatchNumber, string? SerialNumber, Guid? CountedByUserId, string? CountedByUserName, DateTime? CountedAt, DateTime CreatedAt);

public class InventorySessionService(AppDbContext db, AuditService audit, NotificationAlertService alerts, IHubContext<DynamicHubProxy> hubContext)
{
    public async Task<InventorySessionDto> CreatePlannedAsync(Guid tenantId, CreatePlannedSessionRequest request, Guid userId, CancellationToken ct = default)
    {
        var session = new InventorySession
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Status = InventorySessionStatus.Draft
        };

        db.InventorySessions.Add(session);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "SESSION_PLANNED", userId, null, "InventorySession", session.Id.ToString(),
            $"Zone={request.Zone ?? "ALL"}, Category={request.Category ?? "ALL"}", null, ct);

        var stockQuery = db.InventoryStocks.Where(s => s.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(request.Zone))
        {
            stockQuery = stockQuery.Where(s => s.Location != null && s.Location.Zone != null && s.Location.Zone.Contains(request.Zone));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            stockQuery = stockQuery.Where(s => s.Item != null && s.Item.Sku.Contains(request.Category));
        }

        var stockList = await stockQuery.Include(s => s.Item).Include(s => s.Location).ToListAsync(ct);
        foreach (var stock in stockList)
        {
            db.InventoryCounts.Add(new InventoryCount
            {
                TenantId = tenantId,
                InventorySessionId = session.Id,
                ItemId = stock.ItemId,
                LocationId = stock.LocationId,
                ExpectedQuantity = stock.Quantity,
                BatchNumber = stock.BatchNumber,
                SerialNumber = stock.SerialNumber
            });
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(tenantId, session.Id, ct);
    }

    public async Task<IReadOnlyList<InventorySessionDto>> ListAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.InventorySessions
            .Include(s => s.StartedByUser)
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new InventorySessionDto(
                s.Id,
                s.Name,
                s.Description,
                s.Status.ToString(),
                s.StartedAt,
                s.CompletedAt,
                s.StartedByUserId,
                s.StartedByUser != null ? s.StartedByUser.FullName : null,
                s.CreatedAt))
            .ToListAsync(ct);

    public async Task<InventorySessionDto> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var session = await db.InventorySessions
            .Include(s => s.StartedByUser)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, ct);
        if (session is null)
            throw new KeyNotFoundException("Сесията не е намерена");

        return new InventorySessionDto(
            session.Id,
            session.Name,
            session.Description,
            session.Status.ToString(),
            session.StartedAt,
            session.CompletedAt,
            session.StartedByUserId,
            session.StartedByUser != null ? session.StartedByUser.FullName : null,
            session.CreatedAt);
    }

    public async Task<InventorySessionDto> CreateAsync(Guid tenantId, CreateSessionRequest request, Guid userId, CancellationToken ct = default)
    {
        var session = new InventorySession
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Status = InventorySessionStatus.Draft
        };

        db.InventorySessions.Add(session);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "SESSION_CREATED", userId, null, "InventorySession", session.Id.ToString(),
            $"Name={session.Name}", null, ct);

        return await GetAsync(tenantId, session.Id, ct);
    }

    public async Task<InventorySessionDto> StartAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var session = await db.InventorySessions.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, ct);
        if (session is null)
            throw new KeyNotFoundException("Сесията не е намерена");
        if (session.Status != InventorySessionStatus.Draft)
            throw new InvalidOperationException("Само сесии в статус Draft могат да бъдат започнати");

        session.Status = InventorySessionStatus.InProgress;
        session.StartedAt = DateTime.UtcNow;
        session.StartedByUserId = userId;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "SESSION_STARTED", userId, null, "InventorySession", session.Id.ToString(),
            null, null, ct);
        await alerts.CreateAsync(tenantId, "Инвентаризация стартира", $"Сесията '{session.Name}' е стартирана.", AlertLevel.Info, userId, ct);

        if (hubContext?.Clients != null)
        {
            await hubContext.Clients.Group(tenantId.ToString()).SendAsync("WarehouseEvent", new { type = "INVENTORY_STARTED", name = session.Name }, ct);
        }

        // Generate counts based on current stock
        var stockList = await db.InventoryStocks
            .Include(s => s.Item)
            .Include(s => s.Location)
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

        foreach (var stock in stockList)
        {
            var count = new InventoryCount
            {
                TenantId = tenantId,
                InventorySessionId = session.Id,
                ItemId = stock.ItemId,
                LocationId = stock.LocationId,
                ExpectedQuantity = stock.Quantity,
                BatchNumber = stock.BatchNumber,
                SerialNumber = stock.SerialNumber
            };
            db.InventoryCounts.Add(count);
        }
        await db.SaveChangesAsync(ct);

        return await GetAsync(tenantId, session.Id, ct);
    }

    public async Task<InventorySessionDto> CompleteAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var session = await db.InventorySessions
            .Include(s => s.InventoryCounts)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, ct);

        if (session is null)
            throw new KeyNotFoundException("Сесията не е намерена");
        if (session.Status != InventorySessionStatus.InProgress)
            throw new InvalidOperationException("Само сесии в статус InProgress могат да бъдат завършени");

        // Process stock adjustments
        foreach (var count in session.InventoryCounts)
        {
            if (!count.CountedQuantity.HasValue) continue;

            var stock = await db.InventoryStocks.FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                s.ItemId == count.ItemId &&
                s.LocationId == count.LocationId &&
                s.BatchNumber == count.BatchNumber &&
                s.SerialNumber == count.SerialNumber, ct);

            if (stock != null)
            {
                if (stock.Quantity != count.CountedQuantity.Value)
                {
                    await audit.LogAsync(tenantId, "STOCK_ADJUSTMENT", userId, null, "InventoryStock", stock.Id.ToString(),
                        $"Adjustment from {stock.Quantity} to {count.CountedQuantity.Value} for SKU {count.ItemId}", null, ct);

                    stock.Quantity = count.CountedQuantity.Value;
                    stock.UpdatedAt = DateTime.UtcNow;
                    stock.Version++;
                }
            }
            else if (count.CountedQuantity.Value > 0)
            {
                // Create new stock entry if it doesn't exist but was counted
                var newStock = new InventoryStock
                {
                    TenantId = tenantId,
                    ItemId = count.ItemId,
                    LocationId = count.LocationId,
                    Quantity = count.CountedQuantity.Value,
                    BatchNumber = count.BatchNumber,
                    SerialNumber = count.SerialNumber,
                    ExpiryDate = count.ExpiryDate,
                    ProductionDate = count.ProductionDate,
                    BestBeforeDate = count.BestBeforeDate,
                    Status = StockStatus.Active,
                    UpdatedAt = DateTime.UtcNow
                };
                db.InventoryStocks.Add(newStock);

                await audit.LogAsync(tenantId, "STOCK_CREATED_BY_COUNT", userId, null, "InventoryStock", newStock.Id.ToString(),
                    $"New stock created via inventory session for SKU {count.ItemId}", null, ct);
            }
        }

        session.Status = InventorySessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;
        session.CompletedByUserId = userId;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "SESSION_COMPLETED", userId, null, "InventorySession", session.Id.ToString(),
            null, null, ct);
        await alerts.CreateAsync(tenantId, "Инвентаризация завършена", $"Сесията '{session.Name}' е завършена и наличностите са обновени.", AlertLevel.Info, userId, ct);

        if (hubContext?.Clients != null)
        {
            await hubContext.Clients.Group(tenantId.ToString()).SendAsync("WarehouseEvent", new { type = "INVENTORY_COMPLETED", name = session.Name }, ct);
        }

        return await GetAsync(tenantId, session.Id, ct);
    }

    public async Task<IReadOnlyList<InventoryCountDto>> ListCountsAsync(Guid tenantId, Guid sessionId, CancellationToken ct = default) =>
        await db.InventoryCounts
            .Include(c => c.Item)
            .Include(c => c.Location)
            .Include(c => c.CountedByUser)
            .Where(c => c.TenantId == tenantId && c.InventorySessionId == sessionId)
            .OrderBy(c => c.Item.Name)
            .Select(c => new InventoryCountDto(
                c.Id,
                c.InventorySessionId,
                c.ItemId,
                c.Item.Name,
                c.LocationId,
                c.Location.Name,
                c.ExpectedQuantity ?? 0,
                c.CountedQuantity,
                c.BatchNumber,
                c.SerialNumber,
                c.CountedByUserId,
                c.CountedByUser != null ? c.CountedByUser.FullName : null,
                c.CountedAt,
                c.CreatedAt))
            .ToListAsync(ct);

    public async Task<InventoryCountDto> UpdateCountAsync(Guid tenantId, Guid id, UpdateCountRequest request, Guid userId, CancellationToken ct = default)
    {
        var count = await db.InventoryCounts
            .Include(c => c.Item)
            .Include(c => c.Location)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        if (count is null)
            throw new KeyNotFoundException("Броенето не е намерено");

        var session = await db.InventorySessions.FirstOrDefaultAsync(s => s.Id == count.InventorySessionId && s.TenantId == tenantId, ct);
        if (session?.Status != InventorySessionStatus.InProgress)
            throw new InvalidOperationException("Може да се редактират само броения от сесии в статус InProgress");

        count.CountedQuantity = request.CountedQuantity;
        count.CountedByUserId = userId;
        count.CountedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "COUNT_UPDATED", userId, null, "InventoryCount", count.Id.ToString(),
            $"ItemId={count.ItemId}, CountedQuantity={request.CountedQuantity}", null, ct);

        return await ListCountsAsync(tenantId, count.InventorySessionId, ct).ContinueWith(t => t.Result.First(c => c.Id == id), ct);
    }
}

public record CreateSessionRequest(string Name, string? Description);
public record CreatePlannedSessionRequest(string Name, string? Description, string? Zone, string? Category);
public record UpdateCountRequest(decimal CountedQuantity);

public record PickingOrderDto(
    Guid Id,
    string Name,
    string? Reference,
    string Strategy,
    string Status,
    Guid? AssignedUserId,
    string? AssignedUserName,
    Guid? StartedByUserId,
    string? StartedByUserName,
    Guid? CompletedByUserId,
    string? CompletedByUserName,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<PickingOrderLineDto> Lines);

public record PickingOrderLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid? SourceLocationId,
    string? SourceLocationName,
    Guid? TargetLocationId,
    string? TargetLocationName,
    decimal Quantity,
    decimal? PickedQuantity,
    string? Notes,
    IReadOnlyList<PickingStockLineDto> StockLines);

public record PickingStockLineDto(
    Guid Id,
    Guid InventoryStockId,
    string ItemName,
    string LocationName,
    decimal Quantity,
    string? BatchNumber,
    string? SerialNumber,
    DateTime? ExpiryDate,
    Guid? PickedByUserId,
    string? PickedByUserName,
    DateTime? PickedAt);

public record CreatePickingOrderRequest(
    string Name,
    string? Reference,
    string Strategy,
    Guid? AssignedUserId,
    string? Notes,
    IReadOnlyList<CreatePickingLineRequest> Lines);

public record CreatePickingLineRequest(
    Guid ItemId,
    Guid? SourceLocationId,
    Guid? TargetLocationId,
    decimal Quantity,
    string? Notes);

public record UpdatePickingLineRequest(
    Guid LineId,
    decimal PickedQuantity,
    string? OverrideReason = null,
    decimal? TemperatureCelsius = null);

public class PickingService(AppDbContext db, AuditService audit, NotificationAlertService alerts, IHubContext<DynamicHubProxy> hubContext, ShippingService shippingService)
{
    public async Task<IReadOnlyList<PickingOrderDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var orders = await db.PickingOrders
            .Include(p => p.Lines).ThenInclude(l => l.Item)
            .Include(p => p.Lines).ThenInclude(l => l.SourceLocation)
            .Include(p => p.Lines).ThenInclude(l => l.TargetLocation)
            .Include(p => p.Lines).ThenInclude(l => l.StockLines).ThenInclude(s => s.InventoryStock).ThenInclude(i => i.Item)
            .Include(p => p.Lines).ThenInclude(l => l.StockLines).ThenInclude(s => s.InventoryStock).ThenInclude(i => i.Location)
            .Include(p => p.Lines).ThenInclude(l => l.StockLines).ThenInclude(s => s.PickedByUser)
            .Include(p => p.AssignedUser)
            .Include(p => p.StartedByUser)
            .Include(p => p.CompletedByUser)
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<PickingOrderDto> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var order = await db.PickingOrders
            .Include(p => p.Lines).ThenInclude(l => l.Item)
            .Include(p => p.Lines).ThenInclude(l => l.SourceLocation)
            .Include(p => p.Lines).ThenInclude(l => l.TargetLocation)
            .Include(p => p.Lines).ThenInclude(l => l.StockLines).ThenInclude(s => s.InventoryStock).ThenInclude(i => i.Item)
            .Include(p => p.Lines).ThenInclude(l => l.StockLines).ThenInclude(s => s.InventoryStock).ThenInclude(i => i.Location)
            .Include(p => p.Lines).ThenInclude(l => l.StockLines).ThenInclude(s => s.PickedByUser)
            .Include(p => p.AssignedUser)
            .Include(p => p.StartedByUser)
            .Include(p => p.CompletedByUser)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);

        if (order is null)
            throw new KeyNotFoundException("Поръчката не е намерена");

        return MapToDto(order);
    }

    public async Task<PickingOrderDto> CreateAsync(Guid tenantId, CreatePickingOrderRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("Поръчката трябва да съдържа поне една линия");

        if (request.Lines.Any(l => l.Quantity <= 0))
            throw new InvalidOperationException("Всички количества в picking поръчката трябва да са по-големи от 0");

        PickingStrategy strategy = PickingStrategy.FIFO;
        if (!string.IsNullOrEmpty(request.Strategy))
        {
            strategy = Enum.Parse<PickingStrategy>(request.Strategy, true);
        }
        else
        {
            // Try to get default strategy from the first item in the order
            var firstItemReq = request.Lines.FirstOrDefault();
            if (firstItemReq != null)
            {
                var item = await db.Items.FindAsync([firstItemReq.ItemId], ct);
                if (item != null)
                {
                    strategy = item.DefaultPickingStrategy;
                }
            }
        }
        var order = new PickingOrder
        {
            TenantId = tenantId,
            Name = request.Name,
            Reference = request.Reference,
            Strategy = strategy,
            AssignedUserId = request.AssignedUserId,
            Notes = request.Notes,
            Status = PickingOrderStatus.Draft
        };

        db.PickingOrders.Add(order);
        await db.SaveChangesAsync(ct);

        foreach (var lineReq in request.Lines)
        {
            var line = new PickingOrderLine
            {
                TenantId = tenantId,
                PickingOrderId = order.Id,
                ItemId = lineReq.ItemId,
                SourceLocationId = lineReq.SourceLocationId,
                TargetLocationId = lineReq.TargetLocationId,
                Quantity = lineReq.Quantity,
                Notes = lineReq.Notes
            };
            db.PickingOrderLines.Add(line);
        }
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(tenantId, "PICKING_ORDER_CREATED", userId, null, "PickingOrder", order.Id.ToString(),
            $"Name={order.Name}", null, ct);

        return await GetAsync(tenantId, order.Id, ct);
    }

    public async Task<PickingOrderDto> StartAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var order = await db.PickingOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);

        if (order is null) throw new KeyNotFoundException("Поръчката не е намерена");
        if (order.Status != PickingOrderStatus.Draft) throw new InvalidOperationException("Само поръчки в статус Draft могат да бъдат стартирани");

        order.Status = PickingOrderStatus.InProgress;
        order.StartedAt = DateTime.UtcNow;
        order.StartedByUserId = userId;

        await db.SaveChangesAsync(ct);

        foreach (var line in order.Lines)
        {
            await GenerateStockLinesAsync(tenantId, line, order.Strategy, ct);
        }
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(tenantId, "PICKING_ORDER_STARTED", userId, null, "PickingOrder", order.Id.ToString(), null, null, ct);
        await alerts.CreateAsync(tenantId, "Picking стартира", $"Поръчката '{order.Name}' е стартирана.", AlertLevel.Warning, userId, ct);

        if (hubContext?.Clients != null)
        {
            await hubContext.Clients.Group(tenantId.ToString()).SendAsync("WarehouseEvent", new { type = "PICKING_STARTED", name = order.Name }, ct);
        }

        return await GetAsync(tenantId, order.Id, ct);
    }

    private async Task GenerateStockLinesAsync(Guid tenantId, PickingOrderLine line, PickingStrategy strategy, CancellationToken ct = default)
    {
        var availableStockQuery = db.InventoryStocks
            .Include(s => s.Item)
            .Include(s => s.Location)
            .Where(s => s.TenantId == tenantId && s.ItemId == line.ItemId && s.Quantity > 0);

        if (line.SourceLocationId.HasValue)
            availableStockQuery = availableStockQuery.Where(s => s.LocationId == line.SourceLocationId.Value);

        availableStockQuery = availableStockQuery.Where(s => s.Status == StockStatus.Active);

        var availableStock = strategy switch
        {
            PickingStrategy.FIFO => await availableStockQuery.OrderBy(s => s.ReceiptDate ?? s.CreatedAt).ToListAsync(ct),
            PickingStrategy.FEFO => await availableStockQuery
                .OrderBy(s => s.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(s => s.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(s => s.ReceiptDate ?? s.CreatedAt)
                .ToListAsync(ct),
            PickingStrategy.LIFO => await availableStockQuery.OrderByDescending(s => s.ReceiptDate ?? s.CreatedAt).ToListAsync(ct),
            PickingStrategy.FPFO => await availableStockQuery
                .OrderBy(s => s.ProductionDate.HasValue ? 0 : 1)
                .ThenBy(s => s.ProductionDate ?? DateTime.MaxValue)
                .ThenBy(s => s.ReceiptDate ?? s.CreatedAt)
                .ToListAsync(ct),
            _ => throw new ArgumentOutOfRangeException()
        };

        var remaining = line.Quantity;

        foreach (var stock in availableStock)
        {
            if (remaining <= 0) break;

            var takeQty = Math.Min(remaining, stock.Quantity);
            var stockLine = new PickingStockLine
            {
                TenantId = tenantId,
                PickingOrderLineId = line.Id,
                InventoryStockId = stock.Id,
                Quantity = takeQty
            };
            db.PickingStockLines.Add(stockLine);
            remaining -= takeQty;
        }

        if (remaining > 0)
            throw new InvalidOperationException($"Недостатъчна наличност за артикул {line.ItemId}");
    }

    public async Task<PickingOrderDto> CompleteAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var order = await db.PickingOrders
            .Include(p => p.Lines).ThenInclude(l => l.StockLines)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);

        if (order is null) throw new KeyNotFoundException("Поръчката не е намерена");
        if (order.Status != PickingOrderStatus.InProgress) throw new InvalidOperationException("Само поръчки в статус InProgress могат да бъдат завършени");

        var allPicked = order.Lines.All(l => l.StockLines.All(s => s.PickedAt.HasValue));
        if (!allPicked) throw new InvalidOperationException("Не всички артикули са взети");

        foreach (var line in order.Lines)
        {
            foreach (var stockLine in line.StockLines)
            {
                var stock = await db.InventoryStocks.FindAsync([stockLine.InventoryStockId], ct);
                if (stock != null)
                {
                    stock.Quantity -= stockLine.Quantity;
                    stock.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        order.Status = PickingOrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        order.CompletedByUserId = userId;

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(tenantId, "PICKING_ORDER_COMPLETED", userId, null, "PickingOrder", order.Id.ToString(), null, null, ct);
        await alerts.CreateAsync(tenantId, "Picking завършен", $"Поръчката '{order.Name}' е завършена.", AlertLevel.Info, userId, ct);

        if (hubContext?.Clients != null)
        {
            await hubContext.Clients.Group(tenantId.ToString()).SendAsync("WarehouseEvent", new { type = "PICKING_COMPLETED", name = order.Name }, ct);
        }

        // Автоматично генериране на пратка, ако има конфигурация за куриер (За демо цели приемаме първата активна)
        var courierConfig = await db.CourierConfigurations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsActive, ct);
        if (courierConfig != null)
        {
            var shipment = new Shipment
            {
                TenantId = tenantId,
                PickingOrderId = order.Id,
                CourierConfigurationId = courierConfig.Id,
                ReceiverName = "Клиент " + (order.Reference ?? order.Name),
                ReceiverAddress = "ул. Складова 1, гр. София", // Mock data
                ReceiverCity = "София",
                ReceiverPhone = "0888000000",
                TotalWeight = 1.5m, // Mock weight
                Status = ShipmentStatus.Draft
            };

            await shippingService.CreateShipmentAsync(shipment);
            // Опционално автоматично генериране на товарителница
            await shippingService.GenerateWaybillAsync(shipment.Id);
        }

        return await GetAsync(tenantId, order.Id, ct);
    }

    public async Task<PickingOrderDto> UpdateStockLineAsync(Guid tenantId, Guid stockLineId, decimal pickedQuantity, Guid userId, string? overrideReason = null, decimal? temperature = null, CancellationToken ct = default)
    {
        var stockLine = await db.PickingStockLines
            .Include(s => s.PickingOrderLine).ThenInclude(l => l.PickingOrder).ThenInclude(o => o.Lines).ThenInclude(ol => ol.StockLines)
            .FirstOrDefaultAsync(s => s.Id == stockLineId && s.TenantId == tenantId, ct);

        if (stockLine is null) throw new KeyNotFoundException("Редът не е намерен");
        if (stockLine.PickingOrderLine.PickingOrder.Status != PickingOrderStatus.InProgress)
            throw new InvalidOperationException("Може да се актуализират само редове от поръчки в статус InProgress");

        if (pickedQuantity > 0)
        {
            stockLine.Version++;
            stockLine.UpdatedAt = DateTime.UtcNow;

            var order = stockLine.PickingOrderLine.PickingOrder;
            var unpickedPrecedingLines = order.Lines
                .SelectMany(l => l.StockLines)
                .Where(s => !s.PickedAt.HasValue && s.Id != stockLineId)
                .ToList();

            if (order.Strategy == PickingStrategy.FEFO || order.Strategy == PickingStrategy.FIFO)
            {
                var betterOptionsExist = unpickedPrecedingLines.Any(s =>
                    s.PickingOrderLine.ItemId == stockLine.PickingOrderLine.ItemId &&
                    s.Id != stockLineId);

                if (betterOptionsExist && string.IsNullOrEmpty(overrideReason))
                {
                    throw new InvalidOperationException("Има други партиди за този артикул, които трябва да бъдат взети първи според стратегията (FEFO/FIFO). Моля, посочете причина за override.");
                }

                if (betterOptionsExist && !string.IsNullOrEmpty(overrideReason))
                {
                    stockLine.IsOverride = true;
                    stockLine.OverrideReason = overrideReason;
                    await audit.LogAsync(tenantId, "PICKING_OVERRIDE", userId, null, "PickingStockLine", stockLine.Id.ToString(), $"Reason: {overrideReason}", null, ct);
                }
            }

            stockLine.PickedByUserId = userId;
            stockLine.PickedAt = DateTime.UtcNow;

            if (temperature.HasValue)
            {
                await audit.LogAsync(tenantId, "PICKING_TEMP_RECORDED", userId, null, "PickingStockLine", stockLine.Id.ToString(), $"Temp: {temperature}°C", null, ct, temperature);
            }
        }
        else
        {
            stockLine.PickedByUserId = null;
            stockLine.PickedAt = null;
        }

        stockLine.PickingOrderLine.PickedQuantity = stockLine.PickingOrderLine.StockLines
            .Where(s => s.PickedAt.HasValue)
            .Sum(s => s.Quantity);

        await db.SaveChangesAsync(ct);
        return await GetAsync(tenantId, stockLine.PickingOrderLine.PickingOrderId, ct);
    }

    private static PickingOrderDto MapToDto(PickingOrder order) =>
        new(
            order.Id,
            order.Name,
            order.Reference,
            order.Strategy.ToString(),
            order.Status.ToString(),
            order.AssignedUserId,
            order.AssignedUser?.FullName,
            order.StartedByUserId,
            order.StartedByUser?.FullName,
            order.CompletedByUserId,
            order.CompletedByUser?.FullName,
            order.StartedAt,
            order.CompletedAt,
            order.Notes,
            order.CreatedAt,
            order.Lines.Select(MapLineToDto).ToList()
        );

    private static PickingOrderLineDto MapLineToDto(PickingOrderLine line) =>
        new(
            line.Id,
            line.ItemId,
            line.Item?.Name ?? "",
            line.SourceLocationId,
            line.SourceLocation?.Name,
            line.TargetLocationId,
            line.TargetLocation?.Name,
            line.Quantity,
            line.PickedQuantity,
            line.Notes,
            line.StockLines.Select(MapStockLineToDto).ToList()
        );

    private static PickingStockLineDto MapStockLineToDto(PickingStockLine line) =>
        new(
            line.Id,
            line.InventoryStockId,
            line.InventoryStock?.Item?.Name ?? "",
            line.InventoryStock?.Location?.Name ?? "",
            line.Quantity,
            line.InventoryStock?.BatchNumber,
            line.InventoryStock?.SerialNumber,
            line.InventoryStock?.ExpiryDate,
            line.PickedByUserId,
            line.PickedByUser?.FullName,
            line.PickedAt
        );
}

public record GoodsReceiptDto(
    Guid Id,
    string Name,
    string? Reference,
    string? SupplierName,
    string Status,
    Guid? ReceivedByUserId,
    string? ReceivedByUserName,
    Guid? CompletedByUserId,
    string? CompletedByUserName,
    DateTime? ReceivedAt,
    DateTime? CompletedAt,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<GoodsReceiptLineDto> Lines);

public record GoodsReceiptLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid LocationId,
    string LocationName,
    decimal ExpectedQuantity,
    decimal? ReceivedQuantity,
    string? BatchNumber,
    string? SerialNumber,
    DateTime? ExpiryDate,
    DateTime? ReceivedAt,
    string? Notes);

public record CreateGoodsReceiptRequest(
    string Name,
    string? Reference,
    string? SupplierName,
    string? Notes,
    IReadOnlyList<CreateGoodsReceiptLineRequest> Lines);

public record CreateGoodsReceiptLineRequest(
    Guid ItemId,
    Guid LocationId,
    decimal ExpectedQuantity,
    string? BatchNumber,
    string? SerialNumber,
    DateTime? ExpiryDate,
    string? Notes);

public record UpdateGoodsReceiptLineRequest(decimal ReceivedQuantity, decimal? TemperatureCelsius = null);

public class GoodsReceiptService(AppDbContext db, AuditService audit)
{
    public async Task<IReadOnlyList<GoodsReceiptDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var receipts = await db.GoodsReceipts
            .Include(g => g.ReceivedByUser)
            .Include(g => g.CompletedByUser)
            .Include(g => g.Lines).ThenInclude(l => l.Item)
            .Include(g => g.Lines).ThenInclude(l => l.Location)
            .Where(g => g.TenantId == tenantId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);

        return receipts.Select(MapToDto).ToList();
    }

    public async Task<GoodsReceiptDto> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var receipt = await db.GoodsReceipts
            .Include(g => g.ReceivedByUser)
            .Include(g => g.CompletedByUser)
            .Include(g => g.Lines).ThenInclude(l => l.Item)
            .Include(g => g.Lines).ThenInclude(l => l.Location)
            .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tenantId, ct);

        if (receipt is null)
            throw new KeyNotFoundException("Документът за приемане не е намерен");

        return MapToDto(receipt);
    }

    public async Task<GoodsReceiptDto> CreateAsync(Guid tenantId, CreateGoodsReceiptRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("Документът за приемане трябва да съдържа поне един ред");

        if (request.Lines.Any(l => l.ExpectedQuantity <= 0))
            throw new InvalidOperationException("Всички очаквани количества трябва да са по-големи от 0");

        var receipt = new GoodsReceipt
        {
            TenantId = tenantId,
            Name = request.Name,
            Reference = request.Reference,
            SupplierName = request.SupplierName,
            Notes = request.Notes,
            Status = GoodsReceiptStatus.Draft
        };

        db.GoodsReceipts.Add(receipt);
        await db.SaveChangesAsync(ct);

        foreach (var lineReq in request.Lines)
        {
            var line = new GoodsReceiptLine
            {
                TenantId = tenantId,
                GoodsReceiptId = receipt.Id,
                ItemId = lineReq.ItemId,
                LocationId = lineReq.LocationId,
                ExpectedQuantity = lineReq.ExpectedQuantity,
                BatchNumber = lineReq.BatchNumber,
                SerialNumber = lineReq.SerialNumber,
                ExpiryDate = lineReq.ExpiryDate,
                Notes = lineReq.Notes
            };
            db.GoodsReceiptLines.Add(line);
        }
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(tenantId, "GOODS_RECEIPT_CREATED", userId, null, "GoodsReceipt", receipt.Id.ToString(),
            $"Name={receipt.Name}", null, ct);

        return await GetAsync(tenantId, receipt.Id, ct);
    }

    public async Task<GoodsReceiptDto> StartAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var receipt = await db.GoodsReceipts.FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tenantId, ct);
        if (receipt is null)
            throw new KeyNotFoundException("Документът за приемане не е намерен");
        if (receipt.Status != GoodsReceiptStatus.Draft)
            throw new InvalidOperationException("Само документи в статус Draft могат да бъдат стартирани");

        receipt.Status = GoodsReceiptStatus.InProgress;
        receipt.ReceivedAt = DateTime.UtcNow;
        receipt.ReceivedByUserId = userId;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "GOODS_RECEIPT_STARTED", userId, null, "GoodsReceipt", receipt.Id.ToString(),
            null, null, ct);

        return await GetAsync(tenantId, receipt.Id, ct);
    }

    public async Task<GoodsReceiptDto> CompleteAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var receipt = await db.GoodsReceipts
            .Include(g => g.Lines)
            .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tenantId, ct);

        if (receipt is null)
            throw new KeyNotFoundException("Документът за приемане не е намерен");
        if (receipt.Status != GoodsReceiptStatus.InProgress)
            throw new InvalidOperationException("Само документи в статус InProgress могат да бъдат завършени");

        var allReceived = receipt.Lines.All(l => l.ReceivedQuantity.HasValue);
        if (!allReceived)
            throw new InvalidOperationException("Не всички артикули са приети");

        foreach (var line in receipt.Lines)
        {
            if (line.ReceivedQuantity.HasValue && line.ReceivedQuantity > 0)
            {
                var existingStock = await db.InventoryStocks
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ItemId == line.ItemId &&
                        s.LocationId == line.LocationId &&
                        s.BatchNumber == line.BatchNumber &&
                        s.SerialNumber == line.SerialNumber, ct);

                if (existingStock != null)
                {
                    existingStock.Quantity += line.ReceivedQuantity.Value;
                    existingStock.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var stock = new InventoryStock
                    {
                        TenantId = tenantId,
                        ItemId = line.ItemId,
                        LocationId = line.LocationId,
                        Quantity = line.ReceivedQuantity.Value,
                        ReservedQuantity = 0,
                        BatchNumber = line.BatchNumber,
                        SerialNumber = line.SerialNumber,
                        ExpiryDate = line.ExpiryDate,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.InventoryStocks.Add(stock);
                }
            }
        }

        receipt.Status = GoodsReceiptStatus.Completed;
        receipt.CompletedAt = DateTime.UtcNow;
        receipt.CompletedByUserId = userId;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "GOODS_RECEIPT_COMPLETED", userId, null, "GoodsReceipt", receipt.Id.ToString(),
            null, null, ct);

        return await GetAsync(tenantId, receipt.Id, ct);
    }

    public async Task<GoodsReceiptDto> UpdateLineAsync(Guid tenantId, Guid lineId, UpdateGoodsReceiptLineRequest request, Guid userId, CancellationToken ct = default)
    {
        var line = await db.GoodsReceiptLines
            .Include(l => l.GoodsReceipt)
            .Include(l => l.Item)
            .Include(l => l.Location)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId, ct);

        if (line is null)
            throw new KeyNotFoundException("Редът не е намерен");

        if (line.GoodsReceipt.Status != GoodsReceiptStatus.InProgress)
            throw new InvalidOperationException("Може да се актуализират само редове от документи в статус InProgress");

        line.ReceivedQuantity = request.ReceivedQuantity;
        line.ReceivedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "GOODS_RECEIPT_LINE_UPDATED", userId, null, "GoodsReceiptLine", line.Id.ToString(),
            $"ItemId={line.ItemId}, ReceivedQuantity={request.ReceivedQuantity}", null, ct, request.TemperatureCelsius);

        return await GetAsync(tenantId, line.GoodsReceiptId, ct);
    }

    private static GoodsReceiptDto MapToDto(GoodsReceipt receipt) =>
        new(
            receipt.Id,
            receipt.Name,
            receipt.Reference,
            receipt.SupplierName,
            receipt.Status.ToString(),
            receipt.ReceivedByUserId,
            receipt.ReceivedByUser?.FullName,
            receipt.CompletedByUserId,
            receipt.CompletedByUser?.FullName,
            receipt.ReceivedAt,
            receipt.CompletedAt,
            receipt.Notes,
            receipt.CreatedAt,
            receipt.Lines.Select(MapLineToDto).ToList()
        );

    private static GoodsReceiptLineDto MapLineToDto(GoodsReceiptLine line) =>
        new(
            line.Id,
            line.ItemId,
            line.Item?.Name ?? "",
            line.LocationId,
            line.Location?.Name ?? "",
            line.ExpectedQuantity,
            line.ReceivedQuantity,
            line.BatchNumber,
            line.SerialNumber,
            line.ExpiryDate,
            line.ReceivedAt,
            line.Notes
        );
}

public record TransferOrderDto(
    Guid Id,
    string Name,
    string? Reference,
    string Status,
    Guid? MovedByUserId,
    string? MovedByUserName,
    Guid? CompletedByUserId,
    string? CompletedByUserName,
    DateTime? MovedAt,
    DateTime? CompletedAt,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<TransferOrderLineDto> Lines);

public record TransferOrderLineDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid SourceLocationId,
    string SourceLocationName,
    Guid TargetLocationId,
    string TargetLocationName,
    decimal Quantity,
    decimal? MovedQuantity,
    DateTime? MovedAt,
    string? Notes);

public record CreateTransferOrderRequest(
    string Name,
    string? Reference,
    string? Notes,
    IReadOnlyList<CreateTransferLineRequest> Lines);

public record CreateTransferLineRequest(
    Guid ItemId,
    Guid SourceLocationId,
    Guid TargetLocationId,
    decimal Quantity,
    string? Notes);

public record UpdateTransferLineRequest(decimal MovedQuantity);

public class TransferService(AppDbContext db, AuditService audit)
{
    public async Task<IReadOnlyList<TransferOrderDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var orders = await db.TransferOrders
            .Include(t => t.MovedByUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.Lines).ThenInclude(l => l.Item)
            .Include(t => t.Lines).ThenInclude(l => l.SourceLocation)
            .Include(t => t.Lines).ThenInclude(l => l.TargetLocation)
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<TransferOrderDto> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var order = await db.TransferOrders
            .Include(t => t.MovedByUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.Lines).ThenInclude(l => l.Item)
            .Include(t => t.Lines).ThenInclude(l => l.SourceLocation)
            .Include(t => t.Lines).ThenInclude(l => l.TargetLocation)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);

        if (order is null)
            throw new KeyNotFoundException("Документът за трансфер не е намерен");

        return MapToDto(order);
    }

    public async Task<TransferOrderDto> CreateAsync(Guid tenantId, CreateTransferOrderRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("Документът за трансфер трябва да съдържа поне един ред");

        if (request.Lines.Any(l => l.Quantity <= 0))
            throw new InvalidOperationException("Всички количества за трансфер трябва да са по-големи от 0");

        var order = new TransferOrder
        {
            TenantId = tenantId,
            Name = request.Name,
            Reference = request.Reference,
            Notes = request.Notes,
            Status = TransferOrderStatus.Draft
        };

        db.TransferOrders.Add(order);
        await db.SaveChangesAsync(ct);

        foreach (var lineReq in request.Lines)
        {
            var line = new TransferOrderLine
            {
                TenantId = tenantId,
                TransferOrderId = order.Id,
                ItemId = lineReq.ItemId,
                SourceLocationId = lineReq.SourceLocationId,
                TargetLocationId = lineReq.TargetLocationId,
                Quantity = lineReq.Quantity,
                Notes = lineReq.Notes
            };
            db.TransferOrderLines.Add(line);
        }
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(tenantId, "TRANSFER_ORDER_CREATED", userId, null, "TransferOrder", order.Id.ToString(),
            $"Name={order.Name}", null, ct);

        return await GetAsync(tenantId, order.Id, ct);
    }

    public async Task<TransferOrderDto> StartAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var order = await db.TransferOrders.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);
        if (order is null)
            throw new KeyNotFoundException("Документът за трансфер не е намерен");
        if (order.Status != TransferOrderStatus.Draft)
            throw new InvalidOperationException("Само документи в статус Draft могат да бъдат стартирани");

        order.Status = TransferOrderStatus.InProgress;
        order.MovedAt = DateTime.UtcNow;
        order.MovedByUserId = userId;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "TRANSFER_ORDER_STARTED", userId, null, "TransferOrder", order.Id.ToString(),
            null, null, ct);

        return await GetAsync(tenantId, order.Id, ct);
    }

    public async Task<TransferOrderDto> CompleteAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var order = await db.TransferOrders
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);

        if (order is null)
            throw new KeyNotFoundException("Документът за трансфер не е намерен");
        if (order.Status != TransferOrderStatus.InProgress)
            throw new InvalidOperationException("Само документи в статус InProgress могат да бъдат завършени");

        var allMoved = order.Lines.All(l => l.MovedQuantity.HasValue);
        if (!allMoved)
            throw new InvalidOperationException("Не всички артикули са преместени");

        foreach (var line in order.Lines)
        {
            if (line.MovedQuantity.HasValue && line.MovedQuantity > 0)
            {
                var sourceStock = await db.InventoryStocks
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ItemId == line.ItemId &&
                        s.LocationId == line.SourceLocationId, ct);

                if (sourceStock == null || sourceStock.Quantity < line.MovedQuantity)
                    throw new InvalidOperationException($"Недостатъчна наличност за артикул {line.ItemId} в локация {line.SourceLocationId}");

                sourceStock.Quantity -= line.MovedQuantity.Value;
                sourceStock.UpdatedAt = DateTime.UtcNow;

                var targetStock = await db.InventoryStocks
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ItemId == line.ItemId &&
                        s.LocationId == line.TargetLocationId, ct);

                if (targetStock != null)
                {
                    targetStock.Quantity += line.MovedQuantity.Value;
                    targetStock.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var stock = new InventoryStock
                    {
                        TenantId = tenantId,
                        ItemId = line.ItemId,
                        LocationId = line.TargetLocationId,
                        Quantity = line.MovedQuantity.Value,
                        ReservedQuantity = 0,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.InventoryStocks.Add(stock);
                }
            }
        }

        order.Status = TransferOrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        order.CompletedByUserId = userId;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "TRANSFER_ORDER_COMPLETED", userId, null, "TransferOrder", order.Id.ToString(),
            null, null, ct);

        return await GetAsync(tenantId, order.Id, ct);
    }

    public async Task<TransferOrderDto> UpdateLineAsync(Guid tenantId, Guid lineId, UpdateTransferLineRequest request, Guid userId, CancellationToken ct = default)
    {
        var line = await db.TransferOrderLines
            .Include(l => l.TransferOrder)
            .Include(l => l.Item)
            .Include(l => l.SourceLocation)
            .Include(l => l.TargetLocation)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.TenantId == tenantId, ct);

        if (line is null)
            throw new KeyNotFoundException("Редът не е намерен");

        if (line.TransferOrder.Status != TransferOrderStatus.InProgress)
            throw new InvalidOperationException("Може да се актуализират само редове от документи в статус InProgress");

        line.MovedQuantity = request.MovedQuantity;
        line.MovedAt = DateTime.UtcNow;
        line.UpdatedAt = DateTime.UtcNow;
        line.Version++;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "TRANSFER_LINE_UPDATED", userId, null, "TransferOrderLine", line.Id.ToString(),
            $"ItemId={line.ItemId}, MovedQuantity={request.MovedQuantity}", null, ct);

        return await GetAsync(tenantId, line.TransferOrderId, ct);
    }

    private static TransferOrderDto MapToDto(TransferOrder order) =>
        new(
            order.Id,
            order.Name,
            order.Reference,
            order.Status.ToString(),
            order.MovedByUserId,
            order.MovedByUser?.FullName,
            order.CompletedByUserId,
            order.CompletedByUser?.FullName,
            order.MovedAt,
            order.CompletedAt,
            order.Notes,
            order.CreatedAt,
            order.Lines.Select(MapLineToDto).ToList()
        );

    private static TransferOrderLineDto MapLineToDto(TransferOrderLine line) =>
        new(
            line.Id,
            line.ItemId,
            line.Item?.Name ?? "",
            line.SourceLocationId,
            line.SourceLocation?.Name ?? "",
            line.TargetLocationId,
            line.TargetLocation?.Name ?? "",
            line.Quantity,
            line.MovedQuantity,
            line.MovedAt,
            line.Notes
        );
}

public record ErpConfigurationDto(
    Guid Id,
    string Name,
    string ProviderType,
    string? ApiUrl,
    string? DatabaseName,
    bool IsActive,
    bool AutoSyncItems,
    bool AutoSyncInventory,
    DateTime? LastSyncAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateErpConfigRequest(
    string Name,
    string ProviderType,
    string? ApiUrl,
    string? ApiKey,
    string? Username,
    string? Password,
    string? DatabaseName,
    bool AutoSyncItems,
    bool AutoSyncInventory,
    string? SettingsJson);

public record UpdateErpConfigRequest(
    string Name,
    string ProviderType,
    string? ApiUrl,
    string? ApiKey,
    string? Username,
    string? Password,
    string? DatabaseName,
    bool IsActive,
    bool AutoSyncItems,
    bool AutoSyncInventory,
    string? SettingsJson);

public class ErpConfigurationService(AppDbContext db, AuditService audit, IDataProtectionProvider dataProtection, IEnumerable<CALAC.Infrastructure.Services.ErpIntegration.IErpAdapter> erpAdapters)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("ERP_Secrets");

    private CALAC.Infrastructure.Services.ErpIntegration.IErpAdapter GetAdapter(ErpProviderType type)
    {
        var providerName = type.ToString();
        return erpAdapters.FirstOrDefault(a => a.ProviderName == providerName)
            ?? throw new NotSupportedException($"ERP provider {providerName} is not supported or not registered.");
    }

    public async Task<IReadOnlyList<ErpConfigurationDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await db.ErpConfigurations
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .Select(c => new ErpConfigurationDto(
                c.Id,
                c.Name,
                c.ProviderType.ToString(),
                c.ApiUrl,
                c.DatabaseName,
                c.IsActive,
                c.AutoSyncItems,
                c.AutoSyncInventory,
                c.LastSyncAt,
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<ErpConfigurationDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            return null;

        return new ErpConfigurationDto(
            config.Id,
            config.Name,
            config.ProviderType.ToString(),
            config.ApiUrl,
            config.DatabaseName,
            config.IsActive,
            config.AutoSyncItems,
            config.AutoSyncInventory,
            config.LastSyncAt,
            config.CreatedAt,
            config.UpdatedAt);
    }

    public async Task<ErpConfigurationDto> CreateAsync(Guid tenantId, CreateErpConfigRequest request, Guid userId, CancellationToken ct = default)
    {
        var providerType = Enum.Parse<ErpProviderType>(request.ProviderType, true);
        
        var config = new ErpConfiguration
        {
            TenantId = tenantId,
            Name = request.Name,
            ProviderType = providerType,
            ApiUrl = request.ApiUrl,
            ApiKey = string.IsNullOrEmpty(request.ApiKey) ? null : _protector.Protect(request.ApiKey),
            Username = request.Username,
            Password = string.IsNullOrEmpty(request.Password) ? null : _protector.Protect(request.Password),
            DatabaseName = request.DatabaseName,
            IsActive = true,
            AutoSyncItems = request.AutoSyncItems,
            AutoSyncInventory = request.AutoSyncInventory,
            SettingsJson = request.SettingsJson
        };

        db.ErpConfigurations.Add(config);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_CONFIG_CREATED", userId, null, "ErpConfiguration", config.Id.ToString(),
            $"Name={config.Name}, Provider={config.ProviderType}", null, ct);

        return await GetAsync(tenantId, config.Id, ct) ?? throw new InvalidOperationException("Failed to create ERP configuration");
    }

    public async Task<ErpConfigurationDto> UpdateAsync(Guid tenantId, Guid id, UpdateErpConfigRequest request, Guid userId, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        var providerType = Enum.Parse<ErpProviderType>(request.ProviderType, true);

        config.Name = request.Name;
        config.ProviderType = providerType;
        config.ApiUrl = request.ApiUrl;
        config.ApiKey = string.IsNullOrEmpty(request.ApiKey) ? null : _protector.Protect(request.ApiKey);
        config.Username = request.Username;
        config.Password = string.IsNullOrEmpty(request.Password) ? null : _protector.Protect(request.Password);
        config.DatabaseName = request.DatabaseName;
        config.IsActive = request.IsActive;
        config.AutoSyncItems = request.AutoSyncItems;
        config.AutoSyncInventory = request.AutoSyncInventory;
        config.SettingsJson = request.SettingsJson;
        config.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_CONFIG_UPDATED", userId, null, "ErpConfiguration", config.Id.ToString(),
            $"Name={config.Name}", null, ct);

        return await GetAsync(tenantId, config.Id, ct) ?? throw new InvalidOperationException("Failed to update ERP configuration");
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        db.ErpConfigurations.Remove(config);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_CONFIG_DELETED", userId, null, "ErpConfiguration", config.Id.ToString(),
            $"Name={config.Name}", null, ct);

        return true;
    }

    public async Task<bool> TestConnectionAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        var adapter = GetAdapter(config.ProviderType);
        return await adapter.TestConnectionAsync(ct);
    }

    public async Task SyncItemsAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        var adapter = GetAdapter(config.ProviderType);
        await adapter.SyncItemsAsync(ct);
        
        config.LastSyncAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_SYNC_ITEMS", userId, null, "ErpConfiguration", config.Id.ToString(),
            null, null, ct);
    }

    public async Task SyncInventoryAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var config = await db.ErpConfigurations
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        
        if (config is null)
            throw new KeyNotFoundException("ERP конфигурацията не е намерена");

        var adapter = GetAdapter(config.ProviderType);
        await adapter.SyncInventoryAsync(ct);
        
        config.LastSyncAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "ERP_SYNC_INVENTORY", userId, null, "ErpConfiguration", config.Id.ToString(),
            null, null, ct);
    }
}
