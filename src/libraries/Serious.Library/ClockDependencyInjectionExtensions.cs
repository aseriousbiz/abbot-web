using Serious;

namespace Microsoft.Extensions.DependencyInjection;

public static class ClockDependencyInjectionExtensions
{
    public static void AddSystemClock(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
    }

    public static void AddTimeTravelClock(this IServiceCollection services)
    {
        services.AddSingleton<IClock, TimeTravelClock>();
    }
}
