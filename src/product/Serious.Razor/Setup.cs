using System;
using Microsoft.Extensions.DependencyInjection;
using Serious.Razor.Components.Services;

[assembly: CLSCompliant(false)]
namespace Serious.Razor;

public static class Setup
{
    public static void AddMarkdownTextArea(this IServiceCollection services)
    {
        services.AddTransient<IMarkdownService, MarkdownService>();
    }
}
