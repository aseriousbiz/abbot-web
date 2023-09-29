using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Infrastructure.AppStartup;

public static class FeatureFlagStartupExtensions
{
    public static void AddFeatureFlagConfiguration(this IConfigurationBuilder builder)
    {
        var settings = builder.Build();
        if (settings["AppConfig:Endpoint"] is { Length: > 0 } endpoint)
        {
            builder.AddAzureAppConfiguration(options => {
                options.Connect(new(endpoint.Require()), new DefaultAzureCredential());

                options.Select(KeyFilter.Any);
                options.UseFeatureFlags();
            });
        }
    }

    public static void AddFeatureFlagServices(this IServiceCollection services, IConfiguration rootConfig)
    {
        services.AddFeatureManagement(rootConfig.GetSection("FeatureManagement"))
            .AddFeatureFilter<ContextualTargetingFilter>();
        services.AddScoped<FeatureService>();

        if (rootConfig["AppConfig:Endpoint"] is { Length: > 0 })
        {
            services.AddAzureAppConfiguration();
        }
    }

    public static void UseFeatureFlagMiddleware(this IApplicationBuilder app)
    {
        var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
        if (config["AppConfig:Endpoint"] is { Length: > 0 })
        {
            app.UseAzureAppConfiguration();
        }
    }
}
