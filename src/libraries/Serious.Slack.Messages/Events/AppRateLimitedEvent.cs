using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Special event sent when the app is rate limited. See <see href="https://api.slack.com/apis/connections/events-api#the-events-api__responding-to-events__rate-limiting-event"/>
/// for more information.
/// </summary>
/// <remarks>
/// Event deliveries currently max out at 30,000 per workspace per 60 minutes. If your app would receive more than
/// one workspace's 30,000 events in a 60 minute window, you'll receive app_rate_limited events describing the
/// conditions every minute.
/// </remarks>
[Element("app_rate_limited")]
public record AppRateLimitedEvent() : Element("app_rate_limited")
{
    /// <summary>
    /// The same shared token used to verify other events in the Events API
    /// </summary>
    [JsonProperty("token")]
    [JsonPropertyName("token")]
    public string Token { get; set; } = null!;

    /// <summary>
    /// A rounded epoch time value indicating the minute your application became rate limited for this workspace.
    /// 1518467820 is at 2018-02-12 20:37:00 UTC.
    /// </summary>
    [JsonProperty("minute_rate_limited")]
    [JsonPropertyName("minute_rate_limited")]
    public long MinuteRateLimited { get; set; }

    /// <summary>
    /// Subscriptions between your app and the workspace with this ID are being rate limited
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string TeamId { get; set; } = null!;

    /// <summary>
    /// your application's ID, especially useful if you have multiple applications working with the Events API
    /// </summary>
    [JsonProperty("api_app_id")]
    [JsonPropertyName("api_app_id")]
    public string ApiAppId { get; set; } = null!;
}
