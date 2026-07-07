using CALAC.Domain.Enums;
using CALAC.Infrastructure.Services.Barcode;
using Xunit;

namespace CALAC.Tests;

public class BarcodeTests
{
    [Fact]
    public void Gs1Parser_ParsesGs1128_Correcty()
    {
        // Arrange
        var parser = new Gs1Parser();
        var barcode = "]C101012345678901231725123110BATCH123";

        // Act
        var result = parser.Parse(barcode);

        // Assert
        Assert.Equal(BarcodeSymbology.Gs1128, result.Symbology);
        Assert.Equal("01234567890123", result.Gtin);
        Assert.Equal(new DateTime(2025, 12, 31), result.ExpiryDate);
        Assert.Equal("BATCH123", result.BatchNumber);
    }

    [Fact]
    public void Gs1Parser_ParsesDataMatrix_Correcty()
    {
        // Arrange
        var parser = new Gs1Parser();
        var barcode = "]d2010123456789012321SERIAL999";

        // Act
        var result = parser.Parse(barcode);

        // Assert
        Assert.Equal(BarcodeSymbology.DataMatrix, result.Symbology);
        Assert.Equal("01234567890123", result.Gtin);
        Assert.Equal("SERIAL999", result.SerialNumber);
    }

    [Theory]
    [InlineData("Upc", BarcodeSymbology.Upc)]
    [InlineData("Ean", BarcodeSymbology.Ean)]
    [InlineData("QrCode", BarcodeSymbology.QrCode)]
    public void Gs1Parser_RespectsPassedSymbology(string symbologyStr, BarcodeSymbology expected)
    {
        // Arrange
        var parser = new Gs1Parser();
        var barcode = "12345678";

        // Act
        var result = parser.Parse(barcode, symbologyStr);

        // Assert
        Assert.Equal(expected, result.Symbology);
    }
}
