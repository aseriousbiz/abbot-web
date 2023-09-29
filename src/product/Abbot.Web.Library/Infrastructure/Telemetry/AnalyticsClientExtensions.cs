using System.Collections.Generic;
using MassTransit.Internals;
using Microsoft.AspNetCore.Routing;
using Segment;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.Telemetry;

public enum AnalyticsFeature
{
    Activations,
    Announcements,
    AppHome,
    Conversations,
    Customers,
    CustomForms,
    Hubs,
    Integrations,
    Invitations,
    Reactions,
    Slack,
    Subscriptions,
    Services,
    Tasks,
    Playbooks,
}

public static class AnalyticsClientExtensions
{
    /// <summary>
    /// Sends a track event to Segment for the specified member and organization.
    /// </summary>
    /// <param name="analyticsClient">The <see cref="IAnalyticsClient"/>.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="feature">The name of the feature this event belongs to.</param>
    /// <param name="actor">The <see cref="Member"/> that is taking the action.</param>
    /// <param name="subjectOrganization">The <see cref="Organization"/> the action occurred in.</param>
    /// <param name="properties">Any additional properties we want to include with the event.</param>
    public static void Track(this IAnalyticsClient analyticsClient,
        string eventName,
        AnalyticsFeature feature,
        Member actor,
        Organization subjectOrganization,
        Dictionary<string, object?>? properties = null)
    {
        var combinedProperties = CombineDictionaries(subjectOrganization, feature.ToString(), properties);
        analyticsClient.Track($"{actor.Id}", eventName, combinedProperties);
    }

    /// <summary>
    /// Sends a track event to Segment for the specified member and organization.
    /// </summary>
    /// <param name="analyticsClient">The <see cref="IAnalyticsClient"/>.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="feature">The category we want to use for the event.</param>
    /// <param name="actor">The <see cref="Member"/> that is taking the action.</param>
    /// <param name="subjectOrganization">The <see cref="Organization"/> the action occurred in.</param>
    /// <param name="properties">Any additional properties we want to include with the event.</param>
    public static void Track(this IAnalyticsClient analyticsClient,
        string eventName,
        AnalyticsFeature feature,
        Member actor,
        Organization subjectOrganization,
        object? properties) => analyticsClient.Track(
            eventName,
            feature,
            actor,
            subjectOrganization,
            GetSegmentDictionaryFromObject(properties));

    /// <summary>
    /// Sends a screen event to Segment for the specified member and organization.
    /// </summary>
    /// <param name="analyticsClient">The <see cref="IAnalyticsClient"/>.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="feature">The name of the feature this event belongs to.</param>
    /// <param name="actor">The <see cref="Member"/> that is taking the action.</param>
    /// <param name="subjectOrganization">The <see cref="Organization"/> the action occurred in.</param>
    /// <param name="properties">Any additional properties we want to include with the event.</param>
    public static void Screen(
        this IAnalyticsClient analyticsClient,
        string eventName,
        AnalyticsFeature feature,
        Member actor,
        Organization subjectOrganization,
        Dictionary<string, object?>? properties = null)
    {
        var combinedProperties = CombineDictionaries(subjectOrganization, feature.ToString(), properties);
        analyticsClient.Screen($"{actor.Id}", eventName, combinedProperties);
    }

    /// <summary>
    /// Sends a screen event to Segment for the specified member and organization.
    /// </summary>
    /// <param name="analyticsClient">The <see cref="IAnalyticsClient"/>.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="feature">The name of the feature this event belongs to.</param>
    /// <param name="actor">The <see cref="Member"/> that is taking the action.</param>
    /// <param name="subjectOrganization">The <see cref="Organization"/> the action occurred in.</param>
    /// <param name="properties">Any additional properties we want to include with the event.</param>
    public static void Screen(
        this IAnalyticsClient analyticsClient,
        string eventName,
        AnalyticsFeature feature,
        Member actor,
        Organization subjectOrganization,
        object properties) => analyticsClient.Screen(
            eventName,
            feature,
            actor,
            subjectOrganization,
            GetSegmentDictionaryFromObject(properties));

    // Method to combine dictionaries.
    static IDictionary<string, object?> CombineDictionaries(
        Organization organization,
        string feature,
        Dictionary<string, object?>? properties = null)
    {
        IDictionary<string, object?> combinedProperties = new Dictionary<string, object?>
        {
            ["organization"] = $"{organization.Id}",
            ["organization_name"] = organization.Name,
            ["platform_id"] = organization.PlatformId,
            ["plan"] = organization.PlanType.ToString(),
            ["feature"] = feature,
        };

        if (properties is not null)
        {
            combinedProperties = combinedProperties.MergeLeft(properties);
        }

        return combinedProperties;
    }

    static Dictionary<string, object?>? GetSegmentDictionaryFromObject(object? properties)
    {
        if (properties is null)
        {
            return null;
        }

        if (properties is IDictionary<string, object?> dictionary)
        {
            return new(dictionary);
        }

        var routeValues = new RouteValueDictionary(properties);
        return new Dictionary<string, object?>(routeValues!);
    }
}
