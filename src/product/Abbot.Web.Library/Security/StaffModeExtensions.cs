using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.Security;

namespace Microsoft.AspNetCore.Http;

public static class StaffModeExtensions
{
    public static bool IsStaffMode(this ViewContext context) => context.HttpContext.IsStaffMode();

    public static bool IsStaffMode(this HttpContext context)
    {
        if (context.Features.Get<StaffModeFeature>() is { } staffModeFeature)
        {
            return staffModeFeature.IsStaffMode
                   && !context.Request.Query.ContainsKey("hide-staff");
        }

        return false;
    }

    public static void SetStaffModePreference(this HttpContext context, bool enabled)
    {
        if (context.Features.Get<StaffModeFeature>() is { } staffModeFeature)
        {
            staffModeFeature.SetStaffModePreference(enabled);
        }
    }

    public static void UseStaffMode(this IApplicationBuilder app)
    {
        app.Use(async (context, next) => {
            // Install StaffMode into the feature collection
            context.Features.Set(new StaffModeFeature(context));
            await next();
        });
    }

    public static void AddStaffMode(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, StaffModeAuthorizationHandler>();
    }
}
