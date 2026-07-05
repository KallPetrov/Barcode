using CALAC.Domain.Entities;

namespace CALAC.Infrastructure.Services;

public interface ILabelTemplateEngine
{
    string RenderItemLabel(Item item, decimal quantity, string? template = null);
    string RenderLocationLabel(Location location, string? template = null);
}

public class LabelTemplateEngine : ILabelTemplateEngine
{
    private const string DefaultItemTemplate = @"^XA
^CI28
^FO50,50^A0N,36,36^FD{{Name}}^FS
^FO50,100^A0N,24,24^FDSKU: {{Sku}}^FS
^FO50,150^BY2
^BCN,100,Y,N,N
^FD{{Barcode}}^FS
^PQ{{Quantity}}
^XZ";

    private const string DefaultLocationTemplate = @"^XA
^FO50,50^A0N,50,50^FD{{Code}}^FS
^FO50,120^B3N,N,100,Y,N
^FD{{Code}}^FS
^XZ";

    public string RenderItemLabel(Item item, decimal quantity, string? template = null)
    {
        var tpl = template ?? DefaultItemTemplate;
        return tpl.Replace("{{Name}}", item.Name)
                  .Replace("{{Sku}}", item.Sku)
                  .Replace("{{Barcode}}", item.Barcode ?? item.Sku)
                  .Replace("{{Quantity}}", ((int)quantity).ToString());
    }

    public string RenderLocationLabel(Location location, string? template = null)
    {
        var tpl = template ?? DefaultLocationTemplate;
        return tpl.Replace("{{Code}}", location.Code);
    }
}
