using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;

namespace Serious.Slack.Payloads;

/// <summary>
/// Base interface for payloads received in response to user interactions with Slack.
/// </summary>
public interface IPayload : IElement
{
    /// <summary>
    /// Token used to verify the payload. May be deprecated.
    /// </summary>
    [JsonProperty("token")]
    [JsonPropertyName("token")]
    string? Token { get; }

    /// <summary>
    /// The Id and Domain for the workspace/team where this action occurred.
    /// </summary>
    [JsonProperty("team")]
    [JsonPropertyName("team")]
    TeamIdentifier Team { get; }

    /// <summary>
    /// The user who interacted to trigger this request.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    UserIdentifier? User { get; }

    /// <summary>
    /// Whether or not this message is part of an Enterprise installation.
    /// </summary>
    /// <remarks>
    /// Undocumented, but seen in the payload.
    /// </remarks>
    [JsonProperty("is_enterprise_install")]
    [JsonPropertyName("is_enterprise_install")]
    bool IsEnterpriseInstall { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    string? IElement.TeamId => Team.Id;

#pragma warning restore CA1033 // Interface methods should be callable by child types
}

/// <summary>
/// Base class for slack interaction payloads.
/// </summary>
/// <remarks>
/// The type should be one of <c>interactive_message</c>, <c>dialog_submission</c>, <c>block_actions</c>,
/// <c>view_submission</c> or <c>view_closed</c>.
/// </remarks>
public abstract record Payload : Element, IPayload
{
    /// <summary>
    /// Constructs the <see cref="Payload"/> with the specified type.
    /// </summary>
    /// <param name="type">The payload type.</param>
    protected Payload(string type) : base(type)
    {
    }

    /// <summary>
    /// Token used to verify the payload. May be deprecated.
    /// </summary>
    [JsonProperty("token")]
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    /// <summary>
    /// The Id and Domain for the workspace/team where this action occurred.
    /// </summary>
    [JsonProperty("team")]
    [JsonPropertyName("team")]
    public TeamIdentifier Team { get; init; } = null!;

    /// <summary>
    /// The user who interacted to trigger this request.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public UserIdentifier? User { get; init; }

    /// <summary>
    /// The App Id.
    /// </summary>
    [JsonProperty("api_app_id")]
    [JsonPropertyName("api_app_id")]
    public virtual string? ApiAppId { get; init; }

    /// <summary>
    /// Whether or not this message is part of an Enterprise installation.
    /// </summary>
    /// <remarks>
    /// Undocumented, but seen in the payload.
    /// </remarks>
    [JsonProperty("is_enterprise_install")]
    [JsonPropertyName("is_enterprise_install")]
    public bool IsEnterpriseInstall { get; init; }
}
