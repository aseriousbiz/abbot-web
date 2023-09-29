using Microsoft.Extensions.DependencyInjection;

namespace Serious.Abbot.Forms;

public static class FormsStartupExtensions
{
    public static void AddFormsServices(this IServiceCollection services)
    {
        services.AddTransient<IFormEngine, FormEngine>();
        services.AddTransient<ITemplateContextFactory, TemplateContextFactory>();
    }
}
