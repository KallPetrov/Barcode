using System.Security.Claims;
using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using CALAC.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService auth, AuditService audit) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record PinLoginRequest(string Username, string Pin);
    public record RefreshTokenRequest(string RefreshToken);
    public record ChangePasswordRequest(string NewPassword);
    public record LoginResponse(string Token, string RefreshToken, UserDto User, bool MustChangePassword);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.Username, request.Password, ct);
        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        await audit.LogAsync(result.User!.TenantId, "LOGIN", result.User.Id, null, "User", result.User.Id.ToString(),
            null, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        return Ok(new LoginResponse(result.Token!, result.RefreshToken!, result.User, result.MustChangePassword));
    }

    [HttpPost("login/pin")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginWithPin([FromBody] PinLoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginWithPinAsync(request.Username, request.Pin, ct);
        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        await audit.LogAsync(result.User!.TenantId, "LOGIN_PIN", result.User.Id, null, "User", result.User.Id.ToString(),
            null, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        return Ok(new LoginResponse(result.Token!, result.RefreshToken!, result.User, result.MustChangePassword));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await auth.RefreshTokenAsync(request.RefreshToken, ct);
        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(new LoginResponse(result.Token!, result.RefreshToken!, result.User!, result.MustChangePassword));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var result = await auth.ChangePasswordAsync(userId, request.NewPassword, ct);
        if (!result) return NotFound();

        await audit.LogAsync(Guid.Parse(User.FindFirstValue("tenant_id")!), "PASSWORD_CHANGED", userId, null, "User", userId.ToString(),
            null, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        return Ok();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await auth.RevokeTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct, [FromServices] AppDbContext db)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        var user = await db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound();

        return Ok(new UserDto(
            user.Id,
            user.Username,
            user.FullName,
            user.Role,
            user.TenantId,
            user.Tenant.Name,
            user.MustChangePassword));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController(DeviceService devices) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpPost("register")]
    public async Task<ActionResult<DeviceDto>> Register([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        var device = await devices.RegisterOrUpdateAsync(TenantId, UserId, request,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(device);
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<DeviceDto>> Heartbeat([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        var device = await devices.RegisterOrUpdateAsync(TenantId, UserId, request,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(device);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> List(CancellationToken ct) =>
        Ok(await devices.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<DeviceDto>> Get(Guid id, CancellationToken ct)
    {
        var device = await devices.GetAsync(TenantId, id, ct);
        return device is null ? NotFound() : Ok(device);
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var result = await devices.RevokeAsync(TenantId, id, UserId, ct);
        return result ? Ok() : NotFound();
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController(SyncService sync, DeviceService devices) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpPost("push")]
    public async Task<ActionResult<SyncPushResponse>> Push([FromBody] SyncPushRequest request, CancellationToken ct)
    {
        if (request.Operations.Count == 0)
            return BadRequest(new { error = "Няма операции за синхронизация" });

        var hardwareId = Request.Headers["X-Device-Id"].FirstOrDefault();
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault()
            ?? Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(hardwareId))
            return BadRequest(new { error = "Липсва X-Device-Id header" });

        var device = (await devices.ListAsync(TenantId, ct))
            .FirstOrDefault(d => d.HardwareId == hardwareId);

        if (device is null)
            return NotFound(new { error = "Устройството не е регистрирано" });

        if (device.Status == DeviceStatus.Maintenance)
            return Forbid();

        var result = await sync.PushAsync(TenantId, device.Id, UserId, request, idempotencyKey, ct);
        return Ok(result);
    }

    [HttpGet("status")]
    public async Task<ActionResult<object>> Status(CancellationToken ct)
    {
        var hardwareId = Request.Headers["X-Device-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(hardwareId))
            return BadRequest(new { error = "Липсва X-Device-Id header" });

        var device = (await devices.ListAsync(TenantId, ct))
            .FirstOrDefault(d => d.HardwareId == hardwareId);

        if (device is null)
            return NotFound(new { error = "Устройството не е регистрирано" });

        return Ok(await sync.GetStatusAsync(TenantId, device.Id, ct));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController(UserManagementService users) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken ct) =>
        Ok(await users.ListAsync(TenantId, ct));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var user = await users.CreateAsync(TenantId, request, AdminId, ct);
            return CreatedAtAction(nameof(List), user);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/webhooks/erp")]
public class ErpWebhooksController(ItemService items, InventoryService inventory, AppDbContext db) : ControllerBase
{
    public record WebhookPayload(string Type, string Sku, string? Name, string? Barcode, decimal? Quantity, string? LocationCode, Guid TenantId);

    [HttpPost]
    [AllowAnonymous] // In production, this should be secured with a signature or a secret token in header
    public async Task<IActionResult> Receive([FromBody] WebhookPayload payload, CancellationToken ct)
    {
        // Simple authentication check for demo purposes
        var tenant = await db.Tenants.FindAsync([payload.TenantId], ct);
        if (tenant == null) return Unauthorized();

        if (payload.Type == "ITEM_UPDATE")
        {
            var existing = await items.GetByBarcodeAsync(payload.TenantId, payload.Barcode ?? payload.Sku, ct);
            if (existing == null)
            {
                await items.CreateAsync(payload.TenantId, new CreateItemRequest(payload.Sku, payload.Name ?? payload.Sku, null, payload.Barcode, null, null, null, null), Guid.Empty, ct);
            }
        }
        else if (payload.Type == "STOCK_UPDATE" && payload.Quantity.HasValue && !string.IsNullOrEmpty(payload.LocationCode))
        {
             var item = await items.GetByBarcodeAsync(payload.TenantId, payload.Barcode ?? payload.Sku, ct);
             var location = (await db.Locations.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.TenantId == payload.TenantId && l.Code == payload.LocationCode, ct));

             if (item != null && location != null)
             {
                 await inventory.AddStockAsync(payload.TenantId, new AddStockRequest(item.Id, location.Id, payload.Quantity.Value, null, null, null), Guid.Empty, ct);
             }
        }

        return Ok(new { status = "processed" });
    }
}

[ApiController]
[Route("api/batch-picking")]
[Authorize(Roles = "Admin,Supervisor")]
public class BatchPickingController(BatchPickingService batchPicking) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpPost("waves")]
    public async Task<ActionResult<BatchWaveDto>> CreateWave([FromBody] CreateBatchWaveRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await batchPicking.CreateWaveAsync(TenantId, request, UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/tenant-branding")]
[Authorize(Roles = "Admin,Supervisor")]
public class TenantBrandingController(TenantBrandingService branding) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<TenantBrandingDto>> Get(CancellationToken ct)
        => Ok(await branding.GetOrCreateAsync(TenantId, ct));

    [HttpPut]
    public async Task<ActionResult<TenantBrandingDto>> Upsert([FromBody] UpsertTenantBrandingRequest request, CancellationToken ct)
        => Ok(await branding.UpsertAsync(TenantId, request, UserId, ct));
}

[ApiController]
[Route("api/subscriptions")]
[Authorize(Roles = "Admin,Supervisor")]
public class SubscriptionController(SubscriptionService subscriptions) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpPost("activate")]
    public async Task<ActionResult<SubscriptionDto>> Activate([FromBody] ActivateSubscriptionRequest request, CancellationToken ct)
        => Ok(await subscriptions.ActivatePlanAsync(TenantId, request, UserId, ct));
}

[ApiController]
[Route("api/forecasting")]
[Authorize(Roles = "Admin,Supervisor")]
public class ForecastingController(ForecastingService forecasting) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet("items/{itemId:guid}")]
    public async Task<ActionResult<ForecastResult>> Get(Guid itemId, [FromQuery] int lookbackPeriods = 3, [FromQuery] decimal smoothingFactor = 0.5m, CancellationToken ct = default)
    {
        try
        {
            return Ok(await forecasting.GetForecastAsync(TenantId, itemId, lookbackPeriods, smoothingFactor, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

[ApiController]
[Route("api/partner/api-keys")]
[Authorize(Roles = "Admin,Supervisor")]
public class PartnerApiKeysController(PartnerApiKeyService keys) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpPost]
    public async Task<ActionResult<PartnerApiKeyDto>> Create([FromBody] CreatePartnerApiKeyRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await keys.CreateAsync(TenantId, request, UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/webhooks/subscriptions")]
[Authorize(Roles = "Admin,Supervisor")]
public class WebhookSubscriptionsController(WebhookSubscriptionService subscriptions, AuditService audit) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WebhookSubscriptionDto>>> List(CancellationToken ct)
    {
        var items = await subscriptions.ListAsync(TenantId, ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<WebhookSubscriptionDto>> Create([FromBody] CreateWebhookSubscriptionRequest request, CancellationToken ct)
    {
        try
        {
            var item = await subscriptions.CreateAsync(TenantId, request, UserId, ct);
            return CreatedAtAction(nameof(List), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor")]
public class DashboardController(DeviceService devices, WorkTaskService workTasks, NotificationAlertService alerts, AppDbContext db) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet("stats")]
    public async Task<ActionResult<object>> Stats(CancellationToken ct)
    {
        var allDevices = await devices.ListAsync(TenantId, ct);
        var online = allDevices.Count(d => d.Status == DeviceStatus.Online);
        var offline = allDevices.Count(d => d.Status == DeviceStatus.Offline);

        var activeTasks = await db.WorkTasks.CountAsync(t => t.TenantId == TenantId && t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled, ct);
        var urgentTasks = await db.WorkTasks.CountAsync(t => t.TenantId == TenantId && t.Priority >= WorkTaskPriority.High && t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled, ct);
        var activeInventorySessions = await db.InventorySessions.CountAsync(s => s.TenantId == TenantId && s.Status == InventorySessionStatus.InProgress, ct);
        var activePickings = await db.PickingOrders.CountAsync(p => p.TenantId == TenantId && p.Status == PickingOrderStatus.InProgress, ct);

        var recentActivity = await db.AuditLogs
            .Where(a => a.TenantId == TenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(6)
            .Select(a => new
            {
                action = a.Action,
                entityType = a.EntityType,
                details = a.Details,
                createdAt = a.CreatedAt
            })
            .ToListAsync(ct);

        var recentAlerts = await db.NotificationAlerts
            .Where(a => a.TenantId == TenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(4)
            .Select(a => new
            {
                id = a.Id,
                title = a.Title,
                message = a.Message,
                level = a.Level.ToString(),
                isRead = a.IsRead,
                createdAt = a.CreatedAt
            })
            .ToListAsync(ct);

        var operatorEfficiency = await db.OperatorPerformanceSnapshots
            .Where(p => p.TenantId == TenantId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new { p.UserId, p.EfficiencyRate, p.TasksCompleted })
            .ToListAsync(ct);

        return Ok(new
        {
            totalDevices = allDevices.Count,
            onlineDevices = online,
            offlineDevices = offline,
            activeTasks,
            urgentTasks,
            activeInventorySessions,
            activePickings,
            recentActivity,
            unreadAlertsCount = recentAlerts.Count(a => !a.isRead),
            recentAlerts,
            operatorEfficiency,
            serverTime = DateTime.UtcNow
        });
    }
}

[ApiController]
[Route("api/[controller]")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [HttpGet("~/health")]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new { status = "healthy", service = "CALAC.Api", timestamp = DateTime.UtcNow });

    [HttpGet("pda-heartbeat")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> PdaHeartbeat(CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-10);
        var inactiveDevices = await db.Devices
            .Where(d => d.Status == DeviceStatus.Online && (!d.LastSeenAt.HasValue || d.LastSeenAt.Value < threshold))
            .Select(d => new { d.Id, d.Name, d.HardwareId, d.LastSeenAt })
            .ToListAsync(ct);

        return Ok(new {
            status = inactiveDevices.Any() ? "Warning" : "Healthy",
            inactiveCount = inactiveDevices.Count,
            inactiveDevices
        });
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor")]
public class AuditController(AuditService auditService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> List(CancellationToken ct)
        => Ok(await auditService.ListAsync(TenantId, ct));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LabelsController(IZplService zpl, ItemService items, LocationService locations) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet("item/{id:guid}")]
    public async Task<ActionResult<string>> GetItemLabel(Guid id, [FromQuery] int qty = 1, CancellationToken ct = default)
    {
        var item = await items.GetAsync(TenantId, id, ct);
        if (item == null) return NotFound();

        // Manual conversion for now since we don't have a shared AutoMapper/Mapster yet
        var domainItem = new Item { Name = item.Name, Sku = item.Sku, Barcode = item.Barcode };
        return Ok(zpl.GenerateItemLabel(domainItem, qty));
    }

    [HttpGet("location/{id:guid}")]
    public async Task<ActionResult<string>> GetLocationLabel(Guid id, CancellationToken ct = default)
    {
        var location = await locations.GetAsync(TenantId, id, ct);
        if (location == null) return NotFound();

        var domainLocation = new Location { Code = location.Code };
        return Ok(zpl.GenerateLocationLabel(domainLocation));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController(LocationService locationService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationDto>>> List(CancellationToken ct) =>
        Ok(await locationService.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LocationDto>> Get(Guid id, CancellationToken ct)
    {
        var location = await locationService.GetAsync(TenantId, id, ct);
        return location is null ? NotFound() : Ok(location);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<LocationDto>> Create([FromBody] CreateLocationRequest request, CancellationToken ct)
    {
        try
        {
            var location = await locationService.CreateAsync(TenantId, request, UserId, ct);
            return CreatedAtAction(nameof(Get), new { id = location.Id }, location);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<LocationDto>> Update(Guid id, [FromBody] UpdateLocationRequest request, CancellationToken ct)
    {
        try
        {
            var location = await locationService.UpdateAsync(TenantId, id, request, UserId, ct);
            return Ok(location);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await locationService.DeleteAsync(TenantId, id, UserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ItemsController(ItemService itemService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<PagedResult<ItemDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, [FromQuery] string? sortBy = "sku", [FromQuery] string? sortDirection = "asc", CancellationToken ct = default) =>
        Ok(await itemService.ListAsync(TenantId, page, pageSize, search, sortBy, sortDirection, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemDto>> Get(Guid id, CancellationToken ct)
    {
        var item = await itemService.GetAsync(TenantId, id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<ItemDto>> GetByBarcode(string barcode, CancellationToken ct)
    {
        var item = await itemService.GetByBarcodeAsync(TenantId, barcode, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<ItemDto>> Create([FromBody] CreateItemRequest request, CancellationToken ct)
    {
        try
        {
            var item = await itemService.CreateAsync(TenantId, request, UserId, ct);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<ItemDto>> Update(Guid id, [FromBody] UpdateItemRequest request, CancellationToken ct)
    {
        try
        {
            var item = await itemService.UpdateAsync(TenantId, id, request, UserId, ct);
            return Ok(item);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await itemService.DeleteAsync(TenantId, id, UserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController(InventoryService inventoryService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet("stock")]
    public async Task<ActionResult<IReadOnlyList<InventoryStockDto>>> ListStock(CancellationToken ct) =>
        Ok(await inventoryService.ListStockAsync(TenantId, ct));

    [HttpPost("stock")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<InventoryStockDto>> AddStock([FromBody] AddStockRequest request, CancellationToken ct)
    {
        try
        {
            var stock = await inventoryService.AddStockAsync(TenantId, request, UserId, ct);
            return CreatedAtAction(nameof(ListStock), stock);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventorySessionsController(InventorySessionService sessionService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventorySessionDto>>> List(CancellationToken ct) =>
        Ok(await sessionService.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventorySessionDto>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await sessionService.GetAsync(TenantId, id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/counts")]
    public async Task<ActionResult<IReadOnlyList<InventoryCountDto>>> ListCounts(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await sessionService.ListCountsAsync(TenantId, id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<InventorySessionDto>> Create([FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        var session = await sessionService.CreateAsync(TenantId, request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    [HttpPost("planned")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<InventorySessionDto>> CreatePlanned([FromBody] CreatePlannedSessionRequest request, CancellationToken ct)
    {
        var session = await sessionService.CreatePlannedAsync(TenantId, request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    [HttpPut("{id:guid}/start")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<InventorySessionDto>> Start(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await sessionService.StartAsync(TenantId, id, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/complete")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<InventorySessionDto>> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await sessionService.CompleteAsync(TenantId, id, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("counts/{id:guid}")]
    public async Task<ActionResult<InventoryCountDto>> UpdateCount(Guid id, [FromBody] UpdateCountRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await sessionService.UpdateCountAsync(TenantId, id, request, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GoodsReceiptsController(GoodsReceiptService goodsReceiptService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GoodsReceiptDto>>> List(CancellationToken ct)
        => Ok(await goodsReceiptService.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GoodsReceiptDto>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await goodsReceiptService.GetAsync(TenantId, id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<GoodsReceiptDto>> Create([FromBody] CreateGoodsReceiptRequest request, CancellationToken ct)
    {
        var receipt = await goodsReceiptService.CreateAsync(TenantId, request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = receipt.Id }, receipt);
    }

    [HttpPut("{id:guid}/start")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<ActionResult<GoodsReceiptDto>> Start(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await goodsReceiptService.StartAsync(TenantId, id, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/complete")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<GoodsReceiptDto>> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await goodsReceiptService.CompleteAsync(TenantId, id, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("lines/{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<ActionResult<GoodsReceiptDto>> UpdateLine(Guid id, [FromBody] UpdateGoodsReceiptLineRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await goodsReceiptService.UpdateLineAsync(TenantId, id, request, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransfersController(TransferService transferService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransferOrderDto>>> List(CancellationToken ct)
        => Ok(await transferService.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransferOrderDto>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await transferService.GetAsync(TenantId, id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<TransferOrderDto>> Create([FromBody] CreateTransferOrderRequest request, CancellationToken ct)
    {
        var order = await transferService.CreateAsync(TenantId, request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id:guid}/start")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<ActionResult<TransferOrderDto>> Start(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await transferService.StartAsync(TenantId, id, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/complete")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<TransferOrderDto>> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await transferService.CompleteAsync(TenantId, id, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("lines/{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<ActionResult<TransferOrderDto>> UpdateLine(Guid id, [FromBody] UpdateTransferLineRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await transferService.UpdateLineAsync(TenantId, id, request, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ErpConfigurationsController(ErpConfigurationService erpService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ErpConfigurationDto>>> List(CancellationToken ct)
        => Ok(await erpService.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ErpConfigurationDto>> Get(Guid id, CancellationToken ct)
    {
        var config = await erpService.GetAsync(TenantId, id, ct);
        if (config is null)
            return NotFound();
        return Ok(config);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ErpConfigurationDto>> Create([FromBody] CreateErpConfigRequest request, CancellationToken ct)
    {
        var config = await erpService.CreateAsync(TenantId, request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = config.Id }, config);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ErpConfigurationDto>> Update(Guid id, [FromBody] UpdateErpConfigRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await erpService.UpdateAsync(TenantId, id, request, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await erpService.DeleteAsync(TenantId, id, UserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/test")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<bool>> TestConnection(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await erpService.TestConnectionAsync(TenantId, id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/sync-items")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SyncItems(Guid id, CancellationToken ct)
    {
        try
        {
            await erpService.SyncItemsAsync(TenantId, id, UserId, ct);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/sync-inventory")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SyncInventory(Guid id, CancellationToken ct)
    {
        try
        {
            await erpService.SyncInventoryAsync(TenantId, id, UserId, ct);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkTasksController(WorkTaskService workTaskService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkTaskDto>>> List(CancellationToken ct)
        => Ok(await workTaskService.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkTaskDto>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            var task = await workTaskService.GetAsync(TenantId, id, ct);
            return task is null ? NotFound() : Ok(task);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<WorkTaskDto>> Create([FromBody] CreateWorkTaskRequest request, CancellationToken ct)
    {
        try
        {
            var task = await workTaskService.CreateAsync(TenantId, request, UserId, ct);
            return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<ActionResult<WorkTaskDto>> Update(Guid id, [FromBody] UpdateWorkTaskRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await workTaskService.UpdateAsync(TenantId, id, request, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertsController(NotificationAlertService alertService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationAlertDto>>> List(CancellationToken ct)
        => Ok(await alertService.ListAsync(TenantId, ct));

    [HttpPost("expiry-check")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<IReadOnlyList<NotificationAlertDto>>> ExpiryCheck(CancellationToken ct)
        => Ok(await alertService.CreateExpiryAlertsAsync(TenantId, UserId, ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<NotificationAlertDto>> Create([FromBody] CreateAlertRequest request, CancellationToken ct)
    {
        var alert = await alertService.CreateAsync(TenantId, request.Title, request.Message, request.Level, UserId, ct);
        return CreatedAtAction(nameof(List), new { id = alert.Id }, alert);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<ActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        try
        {
            await alertService.MarkReadAsync(TenantId, id, UserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public record CreateAlertRequest(string Title, string Message, AlertLevel Level);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RemindersController(ReminderService reminderService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReminderDto>>> List(CancellationToken ct)
        => Ok(await reminderService.ListAsync(TenantId, ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<ReminderDto>> Create([FromBody] CreateReminderRequest request, CancellationToken ct)
    {
        var reminder = await reminderService.CreateAsync(TenantId, request.Title, request.Message, request.UserId, request.RelatedEntityId, request.RelatedEntityType, request.DueAt, UserId, ct);
        return CreatedAtAction(nameof(List), new { id = reminder.Id }, reminder);
    }

    [HttpPut("{id:guid}/complete")]
    public async Task<ActionResult> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            await reminderService.CompleteAsync(TenantId, id, UserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public record CreateReminderRequest(string Title, string? Message, Guid? UserId, Guid? RelatedEntityId, string? RelatedEntityType, DateTime DueAt);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KpiReportingController(KpiReportingService kpiService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet("overview")]
    public async Task<ActionResult> Overview(CancellationToken ct)
        => Ok(await kpiService.GetOverviewAsync(TenantId, ct));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OperatorHistoryController(OperatorActionHistoryService historyService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OperatorActionHistoryItem>>> List(CancellationToken ct)
        => Ok(await historyService.ListAsync(TenantId, ct));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SlaController(SlaService slaService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet("overview")]
    public async Task<ActionResult<SlaOverviewDto>> Overview(CancellationToken ct)
        => Ok(await slaService.GetOverviewAsync(TenantId, ct));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OperatorPerformanceController(OperatorPerformanceService performanceService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OperatorPerformanceDto>>> List(CancellationToken ct)
        => Ok(await performanceService.ListAsync(TenantId, ct));

    [HttpPost("generate")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<OperatorPerformanceDto>> Generate([FromBody] GeneratePerformanceRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await performanceService.GenerateAsync(TenantId, request.Period, request.UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public record GeneratePerformanceRequest(string Period, Guid UserId);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PickingController(PickingService pickingService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PickingOrderDto>>> List(CancellationToken ct)
        => Ok(await pickingService.ListAsync(TenantId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PickingOrderDto>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await pickingService.GetAsync(TenantId, id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<PickingOrderDto>> Create([FromBody] CreatePickingOrderRequest request, CancellationToken ct)
    {
        try
        {
            var order = await pickingService.CreateAsync(TenantId, request, UserId, ct);
            return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/start")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<ActionResult<PickingOrderDto>> Start(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await pickingService.StartAsync(TenantId, id, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/complete")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<ActionResult<PickingOrderDto>> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await pickingService.CompleteAsync(TenantId, id, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("stockline/{id:guid}")]
    [Authorize(Roles = "Admin,Supervisor,Operator")]
    public async Task<ActionResult<PickingOrderDto>> UpdateStockLine(Guid id, [FromBody] UpdatePickingLineRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await pickingService.UpdateStockLineAsync(TenantId, id, request.PickedQuantity, UserId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
