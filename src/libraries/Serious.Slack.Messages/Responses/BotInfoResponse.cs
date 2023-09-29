using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The response from a call to the <c>bots.info</c> endpoint.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/methods/bots.info" /> for more info.
/// </remarks>
public class BotInfoResponse : InfoResponse<BotInfo>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// Information about the bot.
    /// </summary>
    [JsonProperty("bot")]
    [JsonPropertyName("bot")]

    public override BotInfo? Body { get; init; }
}
