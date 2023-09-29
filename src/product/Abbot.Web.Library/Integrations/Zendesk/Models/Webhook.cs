using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class WebhookMessage : ApiMessage<Webhook>
{
    [JsonProperty("webhook")]
    [JsonPropertyName("webhook")]
    public override Webhook? Body { get; set; }
}

public class Webhook
{
    public static readonly string JsonRequestFormat = "json";
    public static readonly string ActiveStatus = "active";

    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("endpoint")]
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = null!;

    [JsonProperty("http_method")]
    [JsonPropertyName("http_method")]
    public string HttpMethod { get; set; } = null!;

    [JsonProperty("request_format")]
    [JsonPropertyName("request_format")]
    public string RequestFormat { get; set; } = JsonRequestFormat;

    [JsonProperty("status")]
    [JsonPropertyName("status")]
    public string Status { get; set; } = ActiveStatus;

    [JsonProperty("authentication")]
    [JsonPropertyName("authentication")]
    public WebhookAuthentication Authentication { get; set; } = null!;

    [JsonProperty("subscriptions")]
    [JsonPropertyName("subscriptions")]
    public IList<string> Subscriptions { get; set; } = new List<string>()
    {
        "conditional_ticket_events"
    };
}

public class WebhookAuthentication
{
    [JsonProperty("add_position")]
    [JsonPropertyName("add_position")]
    public string AddPosition { get; set; } = null!;

    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonProperty("data")]
    [JsonPropertyName("data")]
    public object Data { get; set; } = null!;
}

public class BearerTokenData
{
    [JsonProperty("token")]
    [JsonPropertyName("token")]
    public string Token { get; set; } = null!;
}
