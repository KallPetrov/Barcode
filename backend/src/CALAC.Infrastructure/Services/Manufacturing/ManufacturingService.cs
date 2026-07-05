using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CALAC.Infrastructure.Services.Manufacturing;

public record CreateBomRequest(Guid FinishedItemId, string Name, decimal FinishedQuantity, List<BomLineRequest> Lines);
public record BomLineRequest(Guid ComponentItemId, decimal Quantity);
public record WorkOrderDto(Guid Id, string OrderNumber, string Status, decimal PlannedQuantity, decimal ProducedQuantity);

public class ManufacturingService(AppDbContext db, AuditService audit, InventoryService inventory)
{
    public async Task<WorkOrderDto> CreateWorkOrderAsync(Guid tenantId, Guid bomId, decimal quantity, Guid userId, CancellationToken ct = default)
    {
        var bom = await db.BillOfMaterials.FirstOrDefaultAsync(b => b.Id == bomId && b.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("BOM not found");

        var wo = new WorkOrder
        {
            TenantId = tenantId,
            OrderNumber = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
            BillOfMaterialId = bomId,
            PlannedQuantity = quantity,
            Status = WorkOrderStatus.Released
        };

        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "WORK_ORDER_CREATED", userId, null, "WorkOrder", wo.Id.ToString(), $"BOM={bom.Name}, Qty={quantity}", null, ct);

        return Map(wo);
    }

    public async Task ProduceAsync(Guid tenantId, Guid workOrderId, decimal quantity, string? batchNumber, DateTime? expiryDate, Guid userId, CancellationToken ct = default)
    {
        var wo = await db.WorkOrders
            .Include(w => w.BillOfMaterial).ThenInclude(b => b.Lines)
            .FirstOrDefaultAsync(w => w.Id == workOrderId && w.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Work Order not found");

        if (wo.Status == WorkOrderStatus.Completed || wo.Status == WorkOrderStatus.Cancelled)
            throw new InvalidOperationException("Work Order is already finished");

        // 1. Add finished product first to get the Stock ID
        var finishedItem = await db.Items.FindAsync([wo.BillOfMaterial.FinishedItemId], ct);
        var location = await db.Locations.FirstOrDefaultAsync(l => l.TenantId == tenantId && l.IsActive, ct)
            ?? throw new InvalidOperationException("No production location available");

        var finishedStock = await inventory.AddStockAsync(tenantId, new AddStockRequest(
            wo.BillOfMaterial.FinishedItemId,
            location.Id,
            quantity,
            batchNumber,
            null,
            expiryDate,
            DateTime.UtcNow,
            null,
            StockStatus.Active.ToString()
        ), userId, ct);

        // 2. Consume components and link to produced stock
        foreach (var line in wo.BillOfMaterial.Lines)
        {
            var requiredQty = (line.Quantity / wo.BillOfMaterial.FinishedQuantity) * quantity;
            await ConsumeComponentAsync(tenantId, wo.Id, line.ComponentItemId, requiredQty, finishedStock.Id, userId, ct);
        }

        wo.ProducedQuantity += quantity;
        if (wo.ProducedQuantity >= wo.PlannedQuantity)
        {
            wo.Status = WorkOrderStatus.Completed;
            wo.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            wo.Status = WorkOrderStatus.InProgress;
            wo.StartedAt ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, "WORK_ORDER_PRODUCE", userId, null, "WorkOrder", wo.Id.ToString(), $"Qty={quantity}, Batch={batchNumber}", null, ct);
    }

    private async Task ConsumeComponentAsync(Guid tenantId, Guid workOrderId, Guid itemId, decimal quantity, Guid producedStockId, Guid userId, CancellationToken ct)
    {
        var remaining = quantity;
        // Use FEFO/FIFO to pick which batches to consume
        var strategy = (await db.Items.FindAsync([itemId], ct))?.DefaultPickingStrategy ?? PickingStrategy.FIFO;

        var availableStockQuery = db.InventoryStocks
            .Where(s => s.TenantId == tenantId && s.ItemId == itemId && s.Quantity > 0 && s.Status == StockStatus.Active);

        var availableStock = strategy switch
        {
            PickingStrategy.FEFO => await availableStockQuery.OrderBy(s => s.ExpiryDate ?? DateTime.MaxValue).ThenBy(s => s.CreatedAt).ToListAsync(ct),
            _ => await availableStockQuery.OrderBy(s => s.CreatedAt).ToListAsync(ct)
        };

        foreach (var stock in availableStock)
        {
            if (remaining <= 0) break;

            var toConsume = Math.Min(remaining, stock.Quantity);

            // Record consumption and link to produced stock
            db.WorkOrderConsumptions.Add(new WorkOrderConsumption
            {
                TenantId = tenantId,
                WorkOrderId = workOrderId,
                ComponentItemId = itemId,
                InventoryStockId = stock.Id,
                ProducedInventoryStockId = producedStockId,
                Quantity = toConsume
            });

            stock.Quantity -= toConsume;
            stock.UpdatedAt = DateTime.UtcNow;
            remaining -= toConsume;
        }

        if (remaining > 0)
            throw new InvalidOperationException($"Insufficient stock for component {itemId}. Missing {remaining}");
    }

    private static WorkOrderDto Map(WorkOrder wo) => new(wo.Id, wo.OrderNumber, wo.Status.ToString(), wo.PlannedQuantity, wo.ProducedQuantity);
}
