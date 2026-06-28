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
    public record LoginResponse(string Token, UserDto User);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.Username, request.Password, ct);
        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        await audit.LogAsync(result.User!.TenantId, "LOGIN", result.User.Id, null, "User", result.User.Id.ToString(),
            null, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        return Ok(new LoginResponse(result.Token!, result.User));
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

        return Ok(new LoginResponse(result.Token!, result.User));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserDto> Me()
    {
        return Ok(new UserDto(
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!),
            User.Identity!.Name!,
            User.FindFirstValue("full_name") ?? "",
            Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!),
            Guid.Parse(User.FindFirstValue("tenant_id")!),
            ""));
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
        if (string.IsNullOrEmpty(hardwareId))
            return BadRequest(new { error = "Липсва X-Device-Id header" });

        var device = (await devices.ListAsync(TenantId, ct))
            .FirstOrDefault(d => d.HardwareId == hardwareId);

        if (device is null)
            return NotFound(new { error = "Устройството не е регистрирано" });

        var result = await sync.PushAsync(TenantId, device.Id, UserId, request, ct);
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
            serverTime = DateTime.UtcNow
        });
    }
}

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new { status = "healthy", service = "CALAC.Api" });
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
    public async Task<ActionResult<IReadOnlyList<ItemDto>>> List(CancellationToken ct) =>
        Ok(await itemService.ListAsync(TenantId, ct));

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
