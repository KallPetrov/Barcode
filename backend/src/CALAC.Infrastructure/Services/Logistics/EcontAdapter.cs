using CALAC.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.Services.Logistics;

public class EcontAdapter : ICourierAdapter
{
    private readonly ILogger<EcontAdapter> _logger;

    public EcontAdapter(ILogger<EcontAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<WaybillResult> CreateWaybillAsync(Shipment shipment, CourierConfiguration config)
    {
        // В реална имплементация тук ще се прави HTTP POST към API-то на Еконт
        _logger.LogInformation("Creating Econt waybill for shipment {ShipmentId}", shipment.Id);

        // Симулация на успех
        return new WaybillResult
        {
            WaybillNumber = "1234567890",
            LabelPdfUrl = $"https://econt.com/labels/1234567890.pdf",
            TrackingUrl = $"https://econt.com/track/1234567890",
            Success = true
        };
    }

    public async Task<string> GetTrackingStatusAsync(string waybillNumber, CourierConfiguration config)
    {
        return "В процес на доставка (Симулация)";
    }
}
