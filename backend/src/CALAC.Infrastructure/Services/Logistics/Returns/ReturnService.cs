using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services.Logistics.Returns;

public record CreateReturnRequest(string? OrderReference, string? CustomerName, List<ReturnLineRequest> Lines);
public record ReturnLineRequest(Guid ItemId, decimal Quantity, string? BatchNumber, string? SerialNumber, DateTime? ExpiryDate, string? Reason);

public class ReturnService(AppDbContext db, AuditService audit, InventoryService inventory)
{
    public async Task<ReturnOrder> CreateReturnAsync(Guid tenantId, CreateReturnRequest request, Guid userId, CancellationToken ct = default)
    {
        var order = new ReturnOrder
        {
            TenantId = tenantId,
            ReturnNumber = $"RET-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
            OriginalOrderReference = request.OrderReference,
            CustomerName = request.CustomerName,
            Status = ReturnOrderStatus.Draft
        };

        foreach (var line in request.Lines)
        {
            order.Lines.Add(new ReturnOrderLine
            {
                TenantId = tenantId,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                BatchNumber = line.BatchNumber,
                SerialNumber = line.SerialNumber,
                ExpiryDate = line.ExpiryDate,
                Reason = line.Reason
            });
        }

        db.ReturnOrders.Add(order);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "RETURN_CREATED", userId, null, "ReturnOrder", order.Id.ToString(), null, null, ct);

        return order;
    }

    public async Task ProcessReturnAsync(Guid tenantId, Guid returnOrderId, Guid userId, CancellationToken ct = default)
    {
        var order = await db.ReturnOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == returnOrderId && o.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Return Order not found");

        if (order.Status == ReturnOrderStatus.Restocked)
            throw new InvalidOperationException("Return is already processed");

        // Assuming a default Returns/Quarantine location
        var returnLocation = await db.Locations.FirstOrDefaultAsync(l => l.TenantId == tenantId && (l.Zone == "RETURNS" || l.Code == "RET-1"), ct)
            ?? await db.Locations.FirstOrDefaultAsync(l => l.TenantId == tenantId && l.IsActive, ct)
            ?? throw new InvalidOperationException("No return location available");

        foreach (var line in order.Lines)
        {
            if (line.ShouldRestock)
            {
                await inventory.AddStockAsync(tenantId, new AddStockRequest(
                    line.ItemId,
                    returnLocation.Id,
                    line.Quantity,
                    line.BatchNumber,
                    line.SerialNumber,
                    line.ExpiryDate,
                    null,
                    null,
                    StockStatus.Quarantined.ToString() // Default to Quarantined for inspection
                ), userId, ct);
            }
        }

        order.Status = ReturnOrderStatus.Restocked;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "RETURN_PROCESSED", userId, null, "ReturnOrder", order.Id.ToString(), "Stock reintegrated as Quarantined", null, ct);
    }
}
