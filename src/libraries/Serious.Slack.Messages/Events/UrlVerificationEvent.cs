using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.Events
{
    /// <summary>
    /// Represents a Slack Url Verification event https://api.slack.com/events/url_verification. This
    /// Verifies ownership of an Events API Request URL.
    /// </summary>
    /// <remarks>
    /// Once you receive the event, verify the request's authenticity and then respond in plaintext with the
    /// challenge attribute value.
    /// </remarks>
    [Element("url_verification")]
    public sealed record UrlVerificationEvent() : Element("url_verification")
    {
        /// <summary>
        /// The challenge text to return.
        /// </summary>
        [JsonProperty("challenge")]
        [JsonPropertyName("challenge")]
        public string Challenge { get; init; } = null!;

        /// <summary>
        /// The shared-private callback token that authenticates this callback to the application as having come from Slack.
        /// Match this against what you were given when the subscription was created. If it does not match, do not process
        /// the event and discard it.
        /// </summary>
        [JsonProperty("token")]
        [JsonPropertyName("token")]
        public string Token { get; init; } = null!;
    }
}
