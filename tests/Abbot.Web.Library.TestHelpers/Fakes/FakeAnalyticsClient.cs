using MassTransit.Internals;
using Microsoft.AspNetCore.Routing;
using Segment;
using Segment.Model;
using Segment.Stats;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeAnalyticsClient : IAnalyticsClient
{
    public void Dispose()
    {
    }

    public Dictionary<string, Dictionary<string, object>> Identified { get; } = new();
    public Dictionary<(string, string), Dictionary<string, object>> Grouped { get; } = new();
    public Dictionary<(string, string), Dictionary<string, object>> Tracked { get; } = new();
    public Dictionary<(string, string), Dictionary<string, object>> Pages { get; } = new();
    public Dictionary<(string, string), Dictionary<string, object>> Screens { get; } = new();

    void AddOrMerge<TKey>(TKey key, Dictionary<TKey, Dictionary<string, object>> source, IDictionary<string, object>? incoming = null) where TKey : notnull
    {
        incoming ??= new Dictionary<string, object>();
        if (source.TryGetValue(key, out var existing))
        {
            foreach (var kvp in incoming)
            {
                existing[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            source[key] = new Dictionary<string, object>(incoming);
        }
    }

    public void Identify(string userId, IDictionary<string, object> traits)
    {
        AddOrMerge(userId, Identified, traits);
    }

    public void Identify(string userId, IDictionary<string, object> traits, Options options)
    {
        AddOrMerge(userId, Identified, traits);
    }

    public void Group(string userId, string groupId, Options options)
    {
        AddOrMerge((userId, groupId), Grouped);
    }

    public void Group(string userId, string groupId, IDictionary<string, object> traits)
    {
        AddOrMerge((userId, groupId), Grouped, traits);
    }

    public void Group(string userId, string groupId, IDictionary<string, object> traits, Options options)
    {
        AddOrMerge((userId, groupId), Grouped, traits);
    }

    public void Track(string userId, string eventName)
    {
        AddOrMerge((userId, eventName), Tracked);
    }

    public void Track(string userId, string eventName, IDictionary<string, object> properties)
    {
        AddOrMerge((userId, eventName), Tracked, properties);
    }

    public void Track(string userId, string eventName, Options options)
    {
        AddOrMerge((userId, eventName), Tracked, new Dictionary<string, object>());
    }

    public void Track(string userId, string eventName, IDictionary<string, object> properties, Options options)
    {
        AddOrMerge((userId, eventName), Tracked, properties);
    }

    public void Alias(string previousId, string userId)
    {
        throw new NotImplementedException();
    }

    public void Alias(string previousId, string userId, Options options)
    {
        throw new NotImplementedException();
    }

    public void Page(string userId, string name)
    {
        AddOrMerge((userId, name), Pages);
    }

    public void Page(string userId, string name, Options options)
    {
        AddOrMerge((userId, name), Pages);
    }

    public void Page(string userId, string name, string category)
    {
        AddOrMerge((userId, name), Pages);
    }

    public void Page(string userId, string name, IDictionary<string, object> properties)
    {
        AddOrMerge((userId, name), Pages, properties);
    }

    public void Page(string userId, string name, IDictionary<string, object> properties, Options options)
    {
        AddOrMerge((userId, name), Pages, properties);
    }

    public void Page(string userId, string name, string category, IDictionary<string, object> properties, Options options)
    {
        properties["category"] = category;
        AddOrMerge((userId, name), Pages, properties);
    }

    public void Screen(string userId, string name)
    {
        AddOrMerge((userId, name), Screens);
    }

    public void Screen(string userId, string name, Options options)
    {
        AddOrMerge((userId, name), Screens);
    }

    public void Screen(string userId, string name, string category)
    {
        AddOrMerge((userId, name), Screens, new Dictionary<string, object> { { "category", category } });
    }

    public void Screen(string userId, string name, IDictionary<string, object> properties)
    {
        AddOrMerge((userId, name), Screens, properties);
    }

    public void Screen(string userId, string name, IDictionary<string, object> properties, Options options)
    {
        AddOrMerge((userId, name), Screens, properties);
    }

    public void Screen(string userId, string name, string category, IDictionary<string, object> properties, Options options)
    {
        properties["category"] = category;
        AddOrMerge((userId, name), Screens, properties);
    }

    public void Flush()
    {
    }

    public Statistics Statistics { get; set; } = null!;

    public string WriteKey { get; } = null!;

    public Config Config { get; } = null!;

    public event FailedHandler? Failed { add { } remove { } }

    public event SucceededHandler? Succeeded { add { } remove { } }

    public void AssertTracked(string eventName, AnalyticsFeature feature, Member member, params object[] properties) =>
        AssertTracked(eventName, feature, member, member.Organization, properties);

    public void AssertTracked(string eventName, AnalyticsFeature feature, Member member, Organization organization, params object[] properties)
    {
        AssertDictionary(Tracked, organization, member, feature, eventName, properties);
    }

    public void AssertScreen(string eventName, AnalyticsFeature feature, Member member, params object[] properties) =>
        AssertScreen(eventName, feature, member, member.Organization, properties);

    public void AssertScreen(string eventName, AnalyticsFeature feature, Member member, Organization organization, params object[] properties)
    {
        AssertDictionary(Screens, organization, member, feature, eventName, properties);
    }

    static void AssertDictionary(
        IReadOnlyDictionary<(string, string), Dictionary<string, object>> dictionary,
        Organization organization,
        Member expectedMember,
        AnalyticsFeature expectedFeature,
        string expectedEventName,
        object[] expectedProperties)
    {
        var expectedKey = (expectedMember.Id.ToString(), expectedEventName);
        var actualProperties = Assert.Contains(expectedKey, dictionary);

        var expectedPropertiesDictionary = new RouteValueDictionary
        {
            { "organization", $"{organization.Id}" },
            { "organization_name", organization.Name },
            { "platform_id", organization.PlatformId },
            { "plan", organization.PlanType.ToString() },
            { "feature", expectedFeature.ToString() },
        }.MergeLeft(expectedProperties.Select(p => new RouteValueDictionary(p)).ToArray());

        Assert.Equal(
            expectedPropertiesDictionary.Keys.OrderBy(k => k),
            actualProperties.Keys.OrderBy(k => k)
        );

        foreach (var kvp in expectedPropertiesDictionary)
        {
            var actualValue = Assert.Contains(kvp.Key, (IDictionary<string, object>)actualProperties);
            Assert.Equal((kvp.Key, kvp.Value), (kvp.Key, actualValue));
        }
    }
}
