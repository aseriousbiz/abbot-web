using System;
using System.Collections.Generic;
using Serious.Abbot.Entities.Filters;
using Serious.Filters;

namespace Serious.Abbot.Entities;

/// <summary>
/// Holds the names of known Conversation Metrics.
/// </summary>
public static class ConversationMetrics
{
    // Changing these values should be done with extreme care.
    // The values are stored in each individual MetricObservation in the database.
    // If we want to rename a metric, we need to run a transition to move the data using the old name over to the new name.

    /// <summary>
    /// The time, in seconds, between when a conversation is created in the <see cref="ConversationState.New"/> state
    /// and when it moves to <see cref="ConversationState.Waiting"/>.
    /// </summary>
    public static readonly string TimeToFirstResponse = "conversation.time_to_first_response";

    /// <summary>
    /// The time, in seconds, between when a conversation is created in the <see cref="ConversationState.New"/> state
    /// and when it moves to <see cref="ConversationState.Waiting"/>, but only counting time when the room has coverage
    /// from first responders.
    /// </summary>
    /// <remarks>
    /// Effectively, we stop the timer during non-working hours, and then resume it when working hours start again.
    /// If a conversation is opened during non-working hours, and then responded to during non-working hours, the time
    /// will be zero as no working-hours time has elapsed.
    /// </remarks>
    public static readonly string TimeToFirstResponseDuringCoverage = "conversation.time_to_first_response.during_coverage";

    /// <summary>
    /// Whether or not the response is within the target for the room. Only applies to rooms that have a target set.
    /// </summary>
    public static readonly string ResponseWithinTarget = "conversation.response_within_target";

    /// <summary>
    /// The time, in seconds, between when a conversation moves from either <see cref="ConversationState.NeedsResponse"/>
    /// or <see cref="ConversationState.New"/> to <see cref="ConversationState.Waiting"/>.
    /// </summary>
    public static readonly string TimeToResponse = "conversation.time_to_response";

    /// <summary>
    /// The time, in seconds, between when a conversation moves from either <see cref="ConversationState.NeedsResponse"/>
    /// or <see cref="ConversationState.New"/> to <see cref="ConversationState.Waiting"/>, but only counting time when
    /// the room has coverage from first responders.
    /// </summary>
    /// <remarks>
    /// Effectively, we stop the timer during non-working hours, and then resume it when working hours start again.
    /// If a conversation is opened during non-working hours, and then responded to during non-working hours, the time
    /// will be zero as no working-hours time has elapsed.
    /// </remarks>
    public static readonly string TimeToResponseDuringCoverage = "conversation.time_to_response.during_coverage";

    /// <summary>
    /// The time, in seconds, between when a conversation is opened and when it moves to the
    /// <see cref="ConversationState.Closed"/> state.
    /// If a conversation is reopened, this metric will be reported again when it is closed using the original
    /// creation date.
    /// </summary>
    public static readonly string TimeToClose = "conversation.time_to_close";
}

/// <summary>
/// Represents a single metric observation about a Conversation.
/// </summary>
/// <param name="Timestamp">The UTC timestamp at which the observation occurred.</param>
/// <param name="Metric">The name of the metric that was observed.</param>
/// <param name="Value">The value that was observed.</param>
/// <param name="ConversationId">The ID of the <see cref="Conversation"/> the observation was made for.</param>
/// <param name="RoomId">The ID of the <see cref="Room"/> in which the conversation took place.</param>
/// <param name="OrganizationId">The ID of the <see cref="Organization"/> in which the conversation took place.</param>
// NOTE: This type is called MetricObservation because in theory we can expand it (by making ConversationId nullable) to
// include metrics associated with a room, or (by making RoomId nullable) an entire organization.
public record MetricObservation(
    DateTime Timestamp,
    string Metric,
    int ConversationId,
    int RoomId,
    int OrganizationId,
    double Value) : IFilterableEntity<MetricObservation>
{
    // This type tracks metrics associated with a conversation.
    // One important aspect here is that a metric record is _immutable_.
    // We don't expect to go back and update metric records, so we need to avoid capturing data that could change in the record itself.
    // We'd like to keep `JOIN`s to a minimum, so we store a few of the other immutable fields, like Room ID and Organization ID, in the row itself.

    // In many metric systems, you can identify the type of a metric which determines how values should be interpreted.
    // For example each entry in a "gauge" metric is the current value of whatever is being measured.
    // However, each entry in a "counter" metric is a quantity to increment the metric by.
    // We don't store the metric type because we aren't storing arbitrary metric types (like Prometheus or DataDog does).
    // We know the set of metrics that can be stored here, and we know the type of each metric.

    /// <summary>
    /// The unique ID of the Metric
    /// </summary>
    public int Id { get; init; }

    // We could attach a JSON blob of custom dimensions here if we needed to.

    // We don't bring these related entities back by default because we rarely actually bring back ConversationMetric values.
    // We mostly just use this for making aggregate queries.
    public Conversation? Conversation { get; init; }
    public Room? Room { get; init; }
    public Organization? Organization { get; init; }

    /// <inheritdoc cref="IFilterableEntity{TEntity, TContext}.GetFilterItemQueries"/>
    public static IEnumerable<IFilterItemQuery<MetricObservation>> GetFilterItemQueries() =>
        MetricObservationFilters.CreateFilters();
}
