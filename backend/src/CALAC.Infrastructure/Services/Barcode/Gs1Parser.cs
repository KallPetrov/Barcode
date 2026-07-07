using System.Globalization;

namespace CALAC.Infrastructure.Services.Barcode;

using CALAC.Domain.Enums;

public record BarcodeData(
    string? Gtin = null,
    string? BatchNumber = null,
    DateTime? ExpiryDate = null,
    string? SerialNumber = null,
    decimal? Quantity = null,
    string? RawValue = null,
    BarcodeSymbology Symbology = BarcodeSymbology.Unknown);

public interface IBarcodeParser
{
    BarcodeData Parse(string barcode, string? symbology = null);
}

public class Gs1Parser : IBarcodeParser
{
    public BarcodeData Parse(string barcode, string? symbology = null)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return new BarcodeData();

        var detectedSymbology = BarcodeSymbology.Unknown;
        if (!string.IsNullOrEmpty(symbology))
        {
            Enum.TryParse<BarcodeSymbology>(symbology, true, out detectedSymbology);
        }

        var data = new BarcodeData(RawValue: barcode, Symbology: detectedSymbology);

        // Handle GS1 prefixes and symbology detection from AIM identifiers
        var current = barcode;
        if (current.StartsWith("]C1"))
        {
            data = data with { Symbology = BarcodeSymbology.Gs1128 };
            current = current[3..];
        }
        else if (current.StartsWith("]d2"))
        {
            data = data with { Symbology = BarcodeSymbology.DataMatrix };
            current = current[3..];
        }
        else if (current.StartsWith("]Q1"))
        {
            data = data with { Symbology = BarcodeSymbology.QrCode };
            current = current[3..];
        }
        else if (current.StartsWith("]e0"))
        {
            data = data with { Symbology = BarcodeSymbology.Gs1DataBar };
            current = current[3..];
        }

        // Very basic GS1-128 / DataMatrix parser
        // AI 01: GTIN (14 chars)
        // AI 10: Batch (variable, up to 20)
        // AI 17: Expiry (6 chars: YYMMDD)
        // AI 21: Serial (variable, up to 20)
        // AI 30: Quantity (variable)

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
                var rest = current[2..];
                var endIdx = rest.IndexOfAny(['\x1d', '(', ')']);
                if (endIdx == -1)
                {
                    data = data with { BatchNumber = rest };
                    break;
                }
                data = data with { BatchNumber = rest[..endIdx] };
                current = rest[(endIdx + 1)..]; // Skip the separator
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
                current = rest[(endIdx + 1)..]; // Skip the separator
            }
            else if (current.StartsWith("30"))
            {
                var rest = current[2..];
                var endIdx = rest.IndexOfAny(['\x1d', '(', ')']);
                string qtyStr;
                if (endIdx == -1)
                {
                    qtyStr = rest;
                    current = string.Empty;
                }
                else
                {
                    qtyStr = rest[..endIdx];
                    current = rest[(endIdx + 1)..];
                }
                if (decimal.TryParse(qtyStr, out var qty))
                {
                    data = data with { Quantity = qty };
                }
                if (current == string.Empty) break;
            }
            else
            {
                // Skip one char and try again or break? GS1 AIs are at least 2 chars.
                // If we don't recognize the AI, we might be in trouble without FNC1 separators.
                break;
            }
        }

        return data;
    }
}
