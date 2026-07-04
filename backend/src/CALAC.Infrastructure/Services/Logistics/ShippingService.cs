using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.Services.Logistics;

public class ShippingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ShippingService> _logger;
    private readonly IEnumerable<ICourierAdapter> _adapters;

    public ShippingService(AppDbContext db, ILogger<ShippingService> logger, IEnumerable<ICourierAdapter> adapters)
    {
        _db = db;
        _logger = logger;
        _adapters = adapters;
    }

    public async Task<Shipment> CreateShipmentAsync(Shipment shipment)
    {
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();
        return shipment;
    }

    public async Task<WaybillResult> GenerateWaybillAsync(Guid shipmentId)
    {
        var shipment = await _db.Shipments
            .Include(s => s.CourierConfiguration)
            .FirstOrDefaultAsync(s => s.Id == shipmentId)
            ?? throw new Exception("Shipment not found");

        var adapter = GetAdapter(shipment.CourierConfiguration.CourierType);
        var result = await adapter.CreateWaybillAsync(shipment, shipment.CourierConfiguration);

        if (result.Success)
        {
            shipment.WaybillNumber = result.WaybillNumber;
            shipment.LabelPdfUrl = result.LabelPdfUrl;
            shipment.LabelZpl = result.LabelZpl;
            shipment.TrackingUrl = result.TrackingUrl;
            shipment.Status = ShipmentStatus.LabelGenerated;
            shipment.ShippedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        return result;
    }

    private ICourierAdapter GetAdapter(CourierType type)
    {
        return type switch
        {
            CourierType.Econt => _adapters.OfType<EcontAdapter>().First(),
            CourierType.Speedy => _adapters.OfType<SpeedyAdapter>().First(),
            _ => throw new NotSupportedException($"Courier {type} is not supported")
        };
    }

    public async Task<List<Shipment>> GetShipmentsAsync()
    {
        return await _db.Shipments
            .Include(s => s.CourierConfiguration)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }
}
