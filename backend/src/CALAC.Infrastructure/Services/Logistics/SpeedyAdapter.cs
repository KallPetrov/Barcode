using CALAC.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.Services.Logistics;

public class SpeedyAdapter : ICourierAdapter
{
    private readonly ILogger<SpeedyAdapter> _logger;

    public SpeedyAdapter(ILogger<SpeedyAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<WaybillResult> CreateWaybillAsync(Shipment shipment, CourierConfiguration config)
    {
        // В реална имплементация тук ще се прави HTTP POST към API-то на Спиди
        _logger.LogInformation("Creating Speedy waybill for shipment {ShipmentId}", shipment.Id);

        // Симулация на успех
        return new WaybillResult
        {
            WaybillNumber = "9876543210",
            LabelPdfUrl = $"https://speedy.bg/labels/9876543210.pdf",
            TrackingUrl = $"https://speedy.bg/track/9876543210",
            Success = true
        };
    }

    public async Task<string> GetTrackingStatusAsync(string waybillNumber, CourierConfiguration config)
    {
        return "Приета в офис (Симулация)";
    }
}
