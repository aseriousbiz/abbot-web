using System.Collections.Generic;
using Microsoft.FeatureManagement;

namespace Serious.Abbot.FeatureManagement;

// This is where we need to use IFeatureManager.
// We ban it from the rest of the codebase to force the rest of the codebase to use this type.
#pragma warning disable RS0030

public class FeatureService
{
    readonly IFeatureManager _featureManager;

    // An IFeatureManagerSnapshot is a scoped service that _is_ an IFeatureManager, but acts on a snapshot of the feature state.
    public FeatureService(IFeatureManagerSnapshot featureManager)
    {
        _featureManager = featureManager;
    }

    public async Task<IReadOnlyList<FeatureState>> GetFeatureStateAsync(IFeatureActor actor)
    {
        var list = new List<FeatureState>();
        await foreach (var feature in GetFeatureNamesAsync())
        {
            var enabled = await IsEnabledAsync(feature, actor);
            list.Add(new(feature, enabled));
        }
        return list;
    }

    public IAsyncEnumerable<string> GetFeatureNamesAsync() =>
        _featureManager.GetFeatureNamesAsync();

    public async Task<bool> IsEnabledAsync(string feature, IFeatureActor actor) =>
        await _featureManager.IsEnabledAsync(feature, actor.GetTargetingContext());
}

public record FeatureState(string Flag, bool Enabled);
