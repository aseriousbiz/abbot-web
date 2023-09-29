using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.BlockKit;

/// <summary>
/// Provides a way to filter the list of options in a conversations select menu
/// or conversations multi-select menu.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/composition-objects#filter_conversations"/> for
/// more information.
/// </remarks>
public class Filter
{
    /// <summary>
    /// Indicates which type of conversations should be included in the list.
    /// When this field is provided, any conversations that do not match will
    /// be excluded.
    /// <para>
    /// You should provide an array of strings from the following options:
    /// <c>im</c>, <c>mpim</c>, <c>private</c>, and <c>public</c>.
    /// If set, the array cannot be empty.
    /// </para>
    /// </summary>
    [JsonProperty("include")]
    [JsonPropertyName("include")]
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>
    /// Indicates whether to exclude external <see href="https://api.slack.com/enterprise/shared-channels">shared channels</see>
    /// from conversation lists. Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("exclude_external_shared_channels")]
    [JsonPropertyName("exclude_external_shared_channels")]
    public bool ExcludeExternalSharedChannels { get; init; }

    /// <summary>
    /// Indicates whether to exclude bot users from conversation lists. Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("exclude_bot_users")]
    [JsonPropertyName("exclude_bot_users")]
    public bool ExcludeBotUsers { get; init; }
}
