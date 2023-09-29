using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Payloads;

/// <summary>
/// A mention of a user group.
/// </summary>
[Element("usergroup")]
public record UserGroupMention() : StyledElement("usergroup")
{
    /// <summary>
    /// A mention of a user group.
    /// </summary>
    /// <remarks>
    /// <see href="https://api.slack.com/types/usergroup"/>
    /// </remarks>
    [JsonProperty("user_group_id")]
    [JsonPropertyName("user_group_id")]
    public string UserGroupId { get; init; } = null!;
}
