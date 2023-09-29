using System.Reflection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;

namespace Serious.TestHelpers;

public class FakeFeatureManager : IFeatureManagerSnapshot
{
    readonly Dictionary<(string Flag, string UserOrGroup), bool> _featureStates = new();

#pragma warning disable CS1998
    public async IAsyncEnumerable<string> GetFeatureNamesAsync()
#pragma warning restore CS1998
    {
        foreach (var property in typeof(FeatureFlags).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (property.PropertyType == typeof(string))
            {
                yield return property.Name;
            }
        }
    }

    public void Clear() => _featureStates.Clear();
    public void Set(string feature, bool enabled) => _featureStates[(feature, "")] = enabled;
    public void Set(string feature, string userOrGroup, bool enabled) =>
        _featureStates[(feature, userOrGroup)] = enabled;
    public void Set(string feature, Organization org, bool enabled) =>
        _featureStates[(feature, FeatureHelper.GroupForPlatformId(org.PlatformId))] = enabled;

    public Task<bool> IsEnabledAsync(string feature) => Task.FromResult(IsEnabled(feature, "") ?? true);

    public Task<bool> IsEnabledAsync<TContext>(string feature, TContext context)
    {
        if (context is TargetingContext targetingContext)
        {
            // Check the global, non-user-specific, state. If there is none, the default is ENABLED
            var globalEnabled = IsEnabled(feature, "") ?? true;
            if (globalEnabled)
            {
                return Task.FromResult(true);
            }

            // Check all the user/group IDs. Here, the default is DISABLED
            return Task.FromResult(
                (IsEnabled(feature, targetingContext.UserId) ?? false) ||
                (targetingContext.Groups?.Any(g => IsEnabled(feature, g) ?? false) ?? false));
        }

        return Task.FromResult(false);
    }

    bool? IsEnabled(string feature, string userOrGroup) =>
        _featureStates.TryGetValue((feature, userOrGroup), out var enabled) ? enabled : null;
}
