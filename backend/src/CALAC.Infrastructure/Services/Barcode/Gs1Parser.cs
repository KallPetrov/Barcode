using System.Globalization;

namespace CALAC.Infrastructure.Services.Barcode;

public record BarcodeData(
    string? Gtin = null,
    string? BatchNumber = null,
    DateTime? ExpiryDate = null,
    string? SerialNumber = null,
    decimal? Quantity = null,
    string? RawValue = null);

public interface IBarcodeParser
{
    BarcodeData Parse(string barcode);
}

public class Gs1Parser : IBarcodeParser
{
    public BarcodeData Parse(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return new BarcodeData();

        var data = new BarcodeData(RawValue: barcode);

        // Very basic GS1-128 / DataMatrix parser
        // In a real scenario, this would use a library or a robust regex-based state machine
        // AI 01: GTIN (14 chars)
        // AI 10: Batch (variable, up to 20)
        // AI 17: Expiry (6 chars: YYMMDD)
        // AI 21: Serial (variable, up to 20)

        var current = barcode;
        if (current.StartsWith("]C1")) current = current[3..]; // Remove GS1-128 prefix if present

        while (current.Length >= 2)
        {
            if (current.StartsWith("01"))
            {
                if (current.Length < 16) break;
                data = data with { Gtin = current.Substring(2, 14) };
                current = current[16..];
            }
            else if (current.StartsWith("17"))
            {
                if (current.Length < 8) break;
                var dateStr = current.Substring(2, 6);
                if (DateTime.TryParseExact(dateStr, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry))
                {
                    data = data with { ExpiryDate = expiry };
                }
                current = current[8..];
            }
            else if (current.StartsWith("10"))
            {
                // Simple assumption for demo: batch is until end or next common AI
                var rest = current[2..];
                var endIdx = rest.IndexOfAny(['\x1d', '(', ')']); // GS1 separator
                if (endIdx == -1)
                {
                    data = data with { BatchNumber = rest };
                    break;
                }
                data = data with { BatchNumber = rest[..endIdx] };
                current = rest[endIdx..];
            }
            else if (current.StartsWith("21"))
            {
                var rest = current[2..];
                var endIdx = rest.IndexOfAny(['\x1d', '(', ')']);
                if (endIdx == -1)
                {
                    data = data with { SerialNumber = rest };
                    break;
                }
                data = data with { SerialNumber = rest[..endIdx] };
                current = rest[endIdx..];
            }
            else
            {
                // Unknown or unhandled AI
                break;
            }
        }

        return data;
    }
}
