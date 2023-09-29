using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serious.Abbot;
using Serious.Abbot.Live;

namespace Microsoft.AspNetCore.Routing;

public static class LiveStartupExtensions
{
    public static void AddAbbotLiveServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR();
        services.Configure<LiveOptions>(configuration.GetSection("Live"));
        services.AddScoped<IFlashPublisher, FlashPublisher>();
    }

    public static void MapLive(this IEndpointRouteBuilder endpointRouteBuilder, IOptions<LiveOptions> options)
    {
        if (options.Value.Path is { Length: > 0 } prefix)
        {
            var groupBuilder = endpointRouteBuilder.MapGroup(prefix);
            MapLiveHubs(groupBuilder);
        }
        else
        {
            MapLiveHubs(endpointRouteBuilder);
        }
    }

    static void MapLiveHubs(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapHub<FlashHub>("/flash")
            .RequireCors(policy =>
                policy.WithOrigins(AllowedHosts.Web.Select(o => $"https://{o}").ToArray())
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .AllowAnyMethod())
            .RequireLiveHostIfConfigured();
    }

    static T RequireLiveHostIfConfigured<T>(this T conventionBuilder) where T : IEndpointConventionBuilder
    {
        conventionBuilder.Add(builder => {
            var options = builder.ApplicationServices.GetRequiredService<IOptions<LiveOptions>>();
            if (options.Value.Host is { Length: > 0 } host)
            {
                builder.Metadata.Add(new HostAttribute(host));
            }
        });
        return conventionBuilder;
    }
}
