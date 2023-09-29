using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Event raised when a user changes their profile etc.
/// </summary>
[Element("user_change")]
public record UserChangeEvent : EventBody
{
    /// <summary>
    /// Constructs a <see cref="UserChangeEvent"/>.
    /// </summary>
    public UserChangeEvent() : base("user_change")
    {
    }

    /// <summary>
    /// Constructs a new instance of the <see cref="UserChangeEvent"/> class with the <c>type</c>.
    /// </summary>
    /// <param name="type">Either <c>user_change</c> or <c>team_join</c>.</param>
    protected UserChangeEvent(string type) : base(type)
    {
    }

    /// <summary>
    /// The user that was changed in this event.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public new UserInfo User { get; init; } = null!;

    /// <summary>
    /// I dunno what this is. -@haacked
    /// </summary>
    [JsonProperty("cache_ts")]
    [JsonPropertyName("cache_ts")]
    public long CacheTimestamp { get; init; }
}

/// <summary>
/// Event raised when a user joins a team.
/// </summary>
[Element("team_join")]
public record TeamJoinEvent() : UserChangeEvent("team_join");
