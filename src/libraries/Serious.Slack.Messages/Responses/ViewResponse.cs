using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// A response to the views.* Slack API method.
/// </summary>
public class ViewResponse : InfoResponse<View>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The view returned by the API.
    /// </summary>
    [JsonProperty("view")]
    [JsonPropertyName("view")]
    public override View? Body { get; init; }
}
