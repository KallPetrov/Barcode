using System.Text;
using CALAC.Domain.Entities;

namespace CALAC.Infrastructure.Services;

public interface IZplService
{
    string GenerateItemLabel(Item item, decimal quantity = 1);
    string GenerateLocationLabel(Location location);
}

public class ZplService(ILabelTemplateEngine templateEngine) : IZplService
{
    public string GenerateItemLabel(Item item, decimal quantity = 1)
    {
        return templateEngine.RenderItemLabel(item, quantity);
    }

    public string GenerateLocationLabel(Location location)
    {
        return templateEngine.RenderLocationLabel(location);
    }
}
