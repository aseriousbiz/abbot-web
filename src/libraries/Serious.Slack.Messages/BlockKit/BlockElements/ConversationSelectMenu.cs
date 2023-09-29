using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// This multi-select menu will populate its options with a list of public and private
/// channels, DMs, and MPIMs visible to the current user in the active workspace.
/// </summary>
/// <remarks>
/// Works with block types: <see cref="Section"/> <see cref="Actions" /> <see cref="Input"/>.
///<para>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#conversation_select"/>
/// </para>
/// </remarks>
[Element("conversations_select")]
public sealed record ConversationSelectMenu() : SingleSelectMenu("conversations_select"), IValueElement
{
    /// <summary>
    /// The ID of any valid conversation to be pre-selected when the menu loads.
    /// If <see cref="DefaultToCurrentConversation"/> is <c>true</c>, this will be ignored.
    /// </summary>
    [JsonProperty("initial_conversation")]
    [JsonPropertyName("initial_conversation")]
    public string? InitialConversation { get; init; }

    /// <summary>
    /// Pre-populates the select menu with the conversation that the user was viewing when
    /// they opened the modal, if available. Default is <c>false</c>.
    /// </summary>
    [JsonProperty("default_to_current_conversation")]
    [JsonPropertyName("default_to_current_conversation")]
    public bool DefaultToCurrentConversation { get; init; }

    /// <summary>
    /// When set to <c>true</c>, the view_submission payload from the menu's parent
    /// view will contain a <c>response_url</c>. This <c>response_url</c> can be used
    /// for message responses. The target conversation for the message will be determined
    /// by the value of this select menu.
    /// </summary>
    /// <remarks>
    /// This field only works with menus in input blocks in modals.
    /// </remarks>
    [JsonProperty("response_url_enabled")]
    [JsonPropertyName("response_url_enabled")]
    public bool ResponseUrlEnabled { get; init; }

    /// <summary>
    /// A <see cref="BlockKit.Filter"/> that reduces the list of available conversations
    /// using the specified criteria.
    /// </summary>
    [JsonProperty("filter")]
    [JsonPropertyName("filter")]
    public Filter? Filter { get; init; }

    /// <summary>
    /// The selected conversation.
    /// </summary>
    [JsonProperty("selected_conversation")]
    [JsonPropertyName("selected_conversation")]
    public override string? SelectedValue { get; init; }

    /// <summary>
    /// Provides a generic way to get the value of this element.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    string? IValueElement.Value
    {
        get => SelectedValue;
        init => SelectedValue = value;
    }
}
