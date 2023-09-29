using Microsoft.AspNetCore.Http;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Serious.Abbot.FeatureManagement;

public static class HttpContextFeatureManagementExtensions
{
    const string FeatureActor = nameof(FeatureActor);

    /// <summary>
    /// Retrieves the current <see cref="TargetingContext"/> from the <see cref="HttpContext.Items"/> collection.
    /// If there is none, returns a new <see cref="TargetingContext"/>.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>The resulting <see cref="TargetingContext"/>.</returns>
    public static IFeatureActor GetFeatureActor(this HttpContext context)
    {
        return context.Items[FeatureActor] as IFeatureActor ?? IFeatureActor.Empty;
    }

    /// <summary>
    /// Sets the current <see cref="IFeatureActor"/> in the <see cref="HttpContext.Items"/> collection.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <param name="actor">The <see cref="IFeatureActor"/> to set.</param>
    public static void SetFeatureActor(this HttpContext context, IFeatureActor actor)
    {
        context.Items[FeatureActor] = actor;
    }
}
