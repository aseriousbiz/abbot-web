using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// A response from the <c>emoji.list</c> API.
/// </summary>
public class EmojiListResponse : InfoResponse<IReadOnlyDictionary<string, string>>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// A dictionary of emojis.
    /// </summary>
    [JsonProperty("emoji")]
    [JsonPropertyName("emoji")]
    public override IReadOnlyDictionary<string, string>? Body { get; init; } = new Dictionary<string, string>();
}
