using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.Services.Analytics;

public class AnomalyDetectionService(AppDbContext db, ILogger<AnomalyDetectionService> logger)
{
    public async Task<List<InventoryAnomaly>> DetectDiscrepancyAnomaliesAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Analyze historical counts vs expected
        var counts = await db.InventoryCounts
            .Where(c => c.TenantId == tenantId && c.CountedQuantity.HasValue)
            .Select(c => new
            {
                c.ItemId,
                Discrepancy = (double)Math.Abs((c.CountedQuantity ?? 0) - (c.ExpectedQuantity ?? 0))
            })
            .ToListAsync(ct);

        if (counts.Count < 10) return []; // Not enough data for statistical analysis

        var mean = counts.Average(c => c.Discrepancy);
        var stdDev = Math.Sqrt(counts.Average(c => Math.Pow(c.Discrepancy - mean, 2)));

        if (stdDev == 0) return [];

        var anomalies = counts
            .Select(c => new { c.ItemId, ZScore = (c.Discrepancy - mean) / stdDev })
            .Where(c => c.ZScore > 3.0) // Traditional Z-score threshold for anomalies
            .GroupBy(c => c.ItemId)
            .Select(g => new InventoryAnomaly
            {
                ItemId = g.Key,
                Confidence = 0.9,
                Reason = $"High discrepancy Z-score: {g.Max(x => x.ZScore):F2}"
            })
            .ToList();

        return anomalies;
    }
}

public class InventoryAnomaly
{
    public Guid ItemId { get; set; }
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}
