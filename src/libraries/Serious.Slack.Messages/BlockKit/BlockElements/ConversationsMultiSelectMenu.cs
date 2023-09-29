using System;
using System.Collections.Generic;
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
/// Works with block types: <see cref="Section"/> and <see cref="Input"/>.
///<para>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#conversation_multi_select"/>
/// </para>
/// </remarks>
[Element("multi_conversations_select")]
public sealed record ConversationsMultiSelectMenu() : MultiSelectMenu("multi_conversations_select"), IMultiValueElement
{
    /// <summary>
    /// An array of one or more IDs of any valid conversations to be pre-selected when
    /// the menu loads. If <see cref="DefaultToCurrentConversation"/> is <c>true</c>,
    /// this will be ignored.
    /// </summary>
    [JsonProperty("initial_conversations")]
    [JsonPropertyName("initial_conversations")]
    public IReadOnlyList<string>? InitialConversations { get; init; }

    /// <summary>
    /// Pre-populates the select menu with the conversation that the user was viewing when
    /// they opened the modal, if available. Default is <c>false</c>.
    /// </summary>
    [JsonProperty("default_to_current_conversation")]
    [JsonPropertyName("default_to_current_conversation")]
    public bool DefaultToCurrentConversation { get; init; }

    /// <summary>
    /// A <see cref="BlockKit.Filter"/> that reduces the list of available conversations
    /// using the specified criteria.
    /// </summary>
    [JsonProperty("filter")]
    [JsonPropertyName("filter")]
    public Filter? Filter { get; init; }

    /// <summary>
    /// The selected conversations.
    /// </summary>
    [JsonProperty("selected_conversations")]
    [JsonPropertyName("selected_conversations")]
    public override IReadOnlyList<string> SelectedValues { get; init; } = Array.Empty<string>();

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    IReadOnlyList<string> IMultiValueElement.Values
    {
        get => SelectedValues;
        init => SelectedValues = value;
    }
}
