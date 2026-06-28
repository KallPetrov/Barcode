using System.Text;
using CALAC.Domain.Entities;

namespace CALAC.Infrastructure.Services;

public interface IZplService
{
    string GenerateItemLabel(Item item, decimal quantity = 1);
    string GenerateLocationLabel(Location location);
}

public class ZplService : IZplService
{
    public string GenerateItemLabel(Item item, decimal quantity = 1)
    {
        var sb = new StringBuilder();
        sb.AppendLine("^XA");
        sb.AppendLine("^CI28"); // UTF-8
        sb.AppendLine("^FO50,50^A0N,36,36^FD" + item.Name + "^FS");
        sb.AppendLine("^FO50,100^A0N,24,24^FDSKU: " + item.Sku + "^FS");
        sb.AppendLine("^FO50,150^BY2");
        sb.AppendLine("^BCN,100,Y,N,N");
        sb.AppendLine("^FD" + (item.Barcode ?? item.Sku) + "^FS");
        sb.AppendLine("^PQ" + (int)quantity);
        sb.AppendLine("^XZ");
        return sb.ToString();
    }

    public string GenerateLocationLabel(Location location)
    {
        var sb = new StringBuilder();
        sb.AppendLine("^XA");
        sb.AppendLine("^FO50,50^A0N,50,50^FD" + location.Code + "^FS");
        sb.AppendLine("^FO50,120^B3N,N,100,Y,N");
        sb.AppendLine("^FD" + location.Code + "^FS");
        sb.AppendLine("^XZ");
        return sb.ToString();
    }
}
