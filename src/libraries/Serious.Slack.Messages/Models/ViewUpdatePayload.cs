using System;
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
public record ViewUpdatePayload(string Type) : Element(Type)
{
    /// <summary>
    /// Constructs a new <see cref="ViewUpdatePayload"/> with the <c>modal</c> type.
    /// </summary>
    public ViewUpdatePayload() : this("modal") { }

    readonly PlainText _title = null!;

    /// <summary>
    /// The plain-text title that appears in the top-left of the modal with a max length of 24 characters.
    /// </summary>
    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public PlainText Title
    {
        get => _title;
        init {
            if (value.Text.Length > 24)
            {
                throw new ArgumentOutOfRangeException(nameof(Title), "Title must be less than 25 characters.");
            }
            _title = value;
        }
    }

    /// <summary>
    /// An optional <c>plain_text</c> text object (<see cref="PlainText"/>) that defines the text displayed in the
    /// close button at the bottom-right of the view. Max length of 24 characters.
    /// </summary>
    [JsonProperty("close")]
    [JsonPropertyName("close")]
    public PlainText? Close { get; init; }

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

    /// <summary>
    /// When set to <c>true</c>, clicking on the close button will clear all views in a modal and close it.
    /// Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("clear_on_close")]
    [JsonPropertyName("clear_on_close")]
    public bool ClearOnClose { get; init; }

    /// <summary>
    /// Indicates whether Slack will send your request URL a <c>view_closed</c> event when a user clicks
    /// the <c>close</c> button. Defaults to <c>false</c>.
    /// </summary>
    [JsonProperty("notify_on_close")]
    [JsonPropertyName("notify_on_close")]
    public bool NotifyOnClose { get; init; }

    /// <summary>
    /// An optional <c>plain_text</c> text object (<see cref="PlainText"/>) that defines that defines the text
    /// displayed in the submit button at the bottom-right of the view. <c>submit</c> is required when an input
    /// block is within the blocks array. Max length of 24 characters.
    /// </summary>
    [JsonProperty("submit")]
    [JsonPropertyName("submit")]
    public PlainText? Submit { get; init; }

    /// <summary>
    /// When set to <c>true</c>, disables the <c>submit</c> button until the user has completed one or more inputs.
    /// This property is for <see href="https://api.slack.com/reference/workflows/configuration-view">configuration modals</see>.
    /// </summary>
    [JsonProperty("submit_disabled")]
    [JsonPropertyName("submit_disabled")]
    public bool SubmitDisabled { get; init; }

    /// <summary>
    /// A custom identifier that must be unique for all views on a per-team basis.
    /// </summary>
    [JsonProperty("external_id")]
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; init; }

    /// <summary>
    /// Returns a "lossy" copy of the supplied <see cref="ViewUpdatePayload"/>.
    /// </summary>
    /// <remarks>
    /// The copy is lossy in that if a derived type is passed, only properties of <see cref="ViewUpdatePayload"/>
    /// are copied.
    /// </remarks>
    /// <param name="payload">The payload to copy.</param>
    /// <returns>A <see cref="ViewUpdatePayload"/> with the properties copied.</returns>
    public static ViewUpdatePayload Copy(ViewUpdatePayload payload) => new(payload);
}
