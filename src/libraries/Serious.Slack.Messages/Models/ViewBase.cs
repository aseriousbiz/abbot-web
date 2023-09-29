using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;

namespace Serious.Slack;

/// <summary>
/// Views are app-customized visual areas within modals and Home tabs. This is the base class for views when
/// making a request to create a view.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/surfaces/views"/> for more information.
/// </remarks>
/// <param name="Type">The type of view to create, either <c>modal</c> for modals or <c>home</c> for Home Tabs.</param>

public abstract record ViewBase(string Type) : Element(Type)
{
    /// <summary>
    /// The max length for private metadata.
    /// </summary>
    public const int PrivateMetadataMaxLength = 3000;

    /// <summary>
    /// The max length for the callback id.
    /// </summary>
    public const int CallbackIdMaxLength = 255;

    /// <summary>
    /// A set of blocks (<see cref="ILayoutBlock"/>) to be displayed in the message.
    /// </summary>
    /// <remarks>
    /// This is an <see cref="IList{T}"/> instead of <see cref="IReadOnlyList{T}"/> so users can use collection
    /// initializers.
    /// </remarks>
    [JsonProperty("blocks")]
    [JsonPropertyName("blocks")]
    public IList<ILayoutBlock> Blocks { get; init; } = new List<ILayoutBlock>();

    /// <summary>
    /// An optional string that will be sent to your app in <c>view_submission</c> and <c>block_actions</c> events.
    /// Max length of 3000 characters.
    /// </summary>
    [JsonProperty("private_metadata")]
    [JsonPropertyName("private_metadata")]
    public string? PrivateMetadata { get; init; }

    /// <summary>
    /// An identifier to recognize interactions and submissions of this particular view. Don't use this to store
    /// sensitive information (use <see cref="PrivateMetadata"/> instead). Max length of 255 characters.
    /// </summary>
    [JsonProperty("callback_id")]
    [JsonPropertyName("callback_id")]
    public string? CallbackId { get; init; }
}

/// <summary>
/// When creating or updating the App Home view, this defines what the view should contain.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/surfaces/views"/> for more information.
/// </remarks>
public record AppHomeViewUpdatePayload() : ViewBase("home");
