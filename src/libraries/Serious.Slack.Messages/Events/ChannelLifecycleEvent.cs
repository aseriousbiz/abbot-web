using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Base class for channel lifecycle events (archive, unarchive, etc.).
/// </summary>
/// <param name="Type">The type of the event represented by this object.</param>
public abstract record ChannelLifecycleEvent(string Type) : EventBody(Type)
{
    /// <summary>
    /// The ID of the channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = null!;
}

/// <summary>
/// Sent to all open connections for a user when that user moves the read cursor in a channel.
/// </summary>
[Element(("channel_marked"))]
public record ChannelMarked() : ChannelLifecycleEvent("channel_marked");

/// <summary>
/// The event raised when the app is removed from the channel.
/// <see href="https://api.slack.com/events/channel_left"/>.
/// </summary>
[Element("channel_left")]
public record ChannelLeftEvent() : ChannelLifecycleEvent("channel_left");

/// <summary>
/// The event raised when a channel is deleted
/// <see href="https://api.slack.com/events/channel_deleted"/>.
/// </summary>
[Element("channel_deleted")]
public record ChannelDeletedEvent() : ChannelLifecycleEvent("channel_deleted");

/// <summary>
/// The event raised when a channel is archived
/// <see href="https://api.slack.com/events/channel_archive"/>.
/// </summary>
[Element("channel_archive")]
public record ChannelArchiveEvent() : ChannelLifecycleEvent("channel_archive");

/// <summary>
/// The event raised when a channel is unarchived
/// <see href="https://api.slack.com/events/channel_unarchive"/>.
/// </summary>
[Element("channel_unarchive")]
public record ChannelUnarchiveEvent() : ChannelLifecycleEvent("channel_unarchive");

/// <summary>
/// The event raised in Slack when a channel is shared.
/// <see href="https://api.slack.com/events/channel_shared"/>.
/// </summary>
[Element(("channel_shared"))]
public record ChannelSharedEvent() : ChannelLifecycleEvent("channel_shared")
{
    /// <summary>
    /// Team ID of the workspace that has joined the channel. Note that this ID may start with E, indicating that it
    /// is the ID of the organization that has been shared with the channel.
    /// </summary>
    [JsonProperty("connected_team_id")]
    [JsonPropertyName("connected_team_id")]
    public string ConnectedTeamId { get; init; } = null!;
}

/// <summary>
/// The event raised in Slack when a channel is shared.
/// <see href="https://api.slack.com/events/channel_shared"/>.
/// </summary>
[Element(("channel_unshared"))]
public record ChannelUnsharedEvent() : ChannelLifecycleEvent("channel_unshared")
{
    /// <summary>
    /// Team ID of the workspace that has joined the channel. Note that this ID may start with E, indicating that it
    /// is the ID of the organization that has been shared with the channel.
    /// </summary>
    [JsonProperty("previously_connected_team_id")]
    [JsonPropertyName("previously_connected_team_id")]
    public string PreviouslyConnectedTeamId { get; init; } = null!;

    /// <summary>
    /// <code>true</code> if the channel is still externally shared, and <code>false</code> otherwise.
    /// </summary>
    public bool IsExternallyShared { get; init; }
}

/// <summary>
/// The event raised when the app is removed from a private channel.
/// <see href="https://api.slack.com/events/group_left"/>.
/// </summary>
[Element("group_left")]
public record GroupLeftEvent() : ChannelLifecycleEvent("group_left");

/// <summary>
/// The event raised when a private channel is deleted
/// <see href="https://api.slack.com/events/group_deleted"/>.
/// </summary>
[Element("group_deleted")]
public record GroupDeletedEvent() : ChannelLifecycleEvent("group_deleted");

/// <summary>
/// The event raised when a private channel is archived
/// <see href="https://api.slack.com/events/group_archive"/>.
/// </summary>
[Element("group_archive")]
public record GroupArchiveEvent() : ChannelLifecycleEvent("group_archive");

/// <summary>
/// The event raised when a private channel is unarchived
/// <see href="https://api.slack.com/events/group_unarchive"/>.
/// </summary>
[Element("group_unarchive")]
public record GroupUnarchiveEvent() : ChannelLifecycleEvent("group_unarchive");
