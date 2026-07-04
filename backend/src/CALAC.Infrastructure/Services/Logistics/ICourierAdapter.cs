using CALAC.Domain.Entities;

namespace CALAC.Infrastructure.Services.Logistics;

public interface ICourierAdapter
{
    Task<WaybillResult> CreateWaybillAsync(Shipment shipment, CourierConfiguration config);
    Task<string> GetTrackingStatusAsync(string waybillNumber, CourierConfiguration config);
}

public class WaybillResult
{
    public string WaybillNumber { get; set; } = string.Empty;
    public string? LabelPdfUrl { get; set; }
    public string? LabelZpl { get; set; }
    public string? TrackingUrl { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
