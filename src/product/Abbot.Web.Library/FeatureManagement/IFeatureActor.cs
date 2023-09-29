using Microsoft.FeatureManagement.FeatureFilters;

namespace Serious.Abbot.FeatureManagement;

/// <summary>
/// An interface to an object that can be used to check if a feature is enabled.
/// </summary>
public interface IFeatureActor
{
    /// <summary>
    /// Returns an <see cref="IFeatureActor"/> that provides no targeting context.
    /// This actor will never match conditional feature flags, only globally-enabled flags will be checked.
    /// </summary>
    public static readonly IFeatureActor Empty = new EmptyFeatureActor();

    /// <summary>
    /// Gets the <see cref="TargetingContext"/> that should be used to evaluate this actor against the feature filters
    /// </summary>
    TargetingContext GetTargetingContext();

    public class EmptyFeatureActor : IFeatureActor
    {
        public TargetingContext GetTargetingContext() => new();
    }
}
