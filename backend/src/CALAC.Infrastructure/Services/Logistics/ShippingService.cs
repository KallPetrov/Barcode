using CALAC.Domain.Entities;
using CALAC.Domain.Enums;
using CALAC.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.Services.Logistics;

public class ShippingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ShippingService> _logger;
    private readonly IEnumerable<ICourierAdapter> _adapters;
    private readonly IDataProtector _protector;

    public ShippingService(AppDbContext db, ILogger<ShippingService> logger, IEnumerable<ICourierAdapter> adapters, IDataProtectionProvider dataProtection)
    {
        _db = db;
        _logger = logger;
        _adapters = adapters;
        _protector = dataProtection.CreateProtector("PII_Data");
    }

    public async Task<Shipment> CreateShipmentAsync(Shipment shipment)
    {
        // Encrypt PII data before saving
        shipment.ReceiverName = _protector.Protect(shipment.ReceiverName);
        shipment.ReceiverPhone = _protector.Protect(shipment.ReceiverPhone);
        shipment.ReceiverEmail = _protector.Protect(shipment.ReceiverEmail);
        shipment.ReceiverAddress = _protector.Protect(shipment.ReceiverAddress);

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

        // Decrypt PII data for the courier adapter
        shipment.ReceiverName = _protector.Unprotect(shipment.ReceiverName);
        shipment.ReceiverPhone = _protector.Unprotect(shipment.ReceiverPhone);
        shipment.ReceiverEmail = _protector.Unprotect(shipment.ReceiverEmail);
        shipment.ReceiverAddress = _protector.Unprotect(shipment.ReceiverAddress);

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
        var shipments = await _db.Shipments
            .Include(s => s.CourierConfiguration)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        foreach (var s in shipments)
        {
            try
            {
                s.ReceiverName = _protector.Unprotect(s.ReceiverName);
                s.ReceiverPhone = _protector.Unprotect(s.ReceiverPhone);
                s.ReceiverEmail = _protector.Unprotect(s.ReceiverEmail);
                s.ReceiverAddress = _protector.Unprotect(s.ReceiverAddress);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt PII for shipment {ShipmentId}", s.Id);
            }
        }

        return shipments;
    }
}
