using System.Collections;
using System.Collections.Generic;
using Ink.Runtime;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.Telemetry;

/// <summary>
/// Describes an analytics event.
/// </summary>
/// <param name="Feature">The <see cref="AnalyticsFeature"/> the event belongs to.</param>
/// <param name="Event">The name of the analytics event</param>
public record AnalyticsEventBuilder(AnalyticsFeature Feature, string Event)
{
    /// <summary>
    /// Gets or inits the properties associated with the analytics events.
    /// </summary>
    public IDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();

    public object? this[string key]
    {
        get => Properties[key];
        set => Properties[key] = value;
    }
}
