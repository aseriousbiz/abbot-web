using Microsoft.Extensions.DependencyInjection;

namespace Serious.Abbot.Onboarding;

public static class OnboardingStartupExtensions
{
    public static void AddOnboardingServices(this IServiceCollection services)
    {
        services.AddScoped<OnboardingService>();
    }
}
