using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;
using Serious.Slack.Converters;

namespace Serious.Slack.Abstractions;

/// <summary>
/// The root interface for all Slack elements that have a <c>type</c> property in their JSON payload. Examples include
/// <see cref="ButtonElement"/>, <see cref="ImageElement"/>, <see cref="Section"/>, etc.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(ElementConverter))]
public interface IElement
{
    /// <summary>
    /// The element <c>type</c>.
    /// </summary>
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    string Type { get; }

    /// <summary>
    /// The unique identifier for the application this event is intended for. Your application's ID can be found in
    /// the URL of the your application console. If your Request URL manages multiple applications, use this field
    /// along with the token field to validate and route incoming requests.
    /// </summary>
    [JsonProperty("api_app_id")]
    [JsonPropertyName("api_app_id")]
    string? ApiAppId => null;

    /// <summary>
    /// The unique identifier for the workspace/team where this event occurred.
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    string? TeamId => null;
}

/// <summary>
/// Interface for types that are deserialized from Json and may contain additional properties.
/// </summary>
public interface IPropertyBag
{
    /// <summary>
    /// Additional properties that are not defined in the interface, but were present in the JSON payload
    /// used to deserialize this object.
    /// </summary>
    IDictionary<string, object> AdditionalProperties { get; }
}
