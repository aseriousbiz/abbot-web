using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.FeatureManagement;

namespace Microsoft.FeatureManagement;

public static class FeatureManagerExtensions
{
    /// <summary>
    /// Checks whether any/all the given features are enabled for the given actor.
    /// </summary>
    /// <param name="featureService">The <see cref="FeatureService"/>.</param>
    /// <param name="features">The features to check.</param>
    /// <param name="actor">A <see cref="IFeatureActor"/> representing the actor being checked.</param>
    /// <param name="requirement">Whether any or all <paramref name="features"/> are required.</param>
    /// <returns><see langword="true"/> if any/all features are enabled, otherwise <see langword="false"/>.</returns>
    public static async Task<bool> IsEnabledAsync(
        this FeatureService featureService,
        IEnumerable<string> features,
        IFeatureActor actor,
        RequirementType requirement)
    {
        return requirement == RequirementType.All
            ? await AllAsync(features, async n => await featureService.IsEnabledAsync(n, actor).ConfigureAwait(false))
            : await AnyAsync(features, async n => await featureService.IsEnabledAsync(n, actor).ConfigureAwait(false));
    }

    static async Task<bool> AnyAsync<TSource>(IEnumerable<TSource> source, Func<TSource, Task<bool>> predicate)
    {
        foreach (var item in source)
        {
            if (await predicate(item).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<bool> AllAsync<TSource>(IEnumerable<TSource> source, Func<TSource, Task<bool>> predicate)
    {
        foreach (var item in source)
        {
            if (!await predicate(item).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }
}
