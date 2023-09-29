using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;

namespace Serious.Slack;

/// <summary>
/// The request information when posting to a <c>response_url</c> to update a message
/// </summary>
/// <param name="Text">The updated text.</param>
/// <param name="Blocks">When editing a message, the blocks of the updated message.</param>
public record ResponseUrlUpdateMessageRequest(
    [property:JsonProperty("text")]
    [property:JsonPropertyName("text")]
    string? Text = null,
    [property:JsonProperty("blocks")]
    [property:JsonPropertyName("blocks")]
    IReadOnlyList<ILayoutBlock>? Blocks = null)
{
    /// <summary>
    /// Whether to replace the original message or not. This is set to <c>true</c>.
    /// </summary>
    [JsonProperty("replace_original")]
    [JsonPropertyName("replace_original")]
    public bool ReplaceOriginal { get; init; } = true;

    /// <summary>
    /// Creates an <see cref="ResponseUrlUpdateMessageRequest"/> from a <see cref="MessageRequest"/>.
    /// </summary>
    /// <param name="messageRequest">The message request.</param>
    public static ResponseUrlUpdateMessageRequest FromMessageRequest(MessageRequest messageRequest)
    {
        return new ResponseUrlUpdateMessageRequest(messageRequest.Text, messageRequest.Blocks);
    }
}

/// <summary>
/// The request information when posting to a <c>response_url</c> to delete a message
/// </summary>
public record ResponseUrlDeleteMessageRequest
{
    /// <summary>
    /// Whether to delete the original message. This is set to <c>true</c> (otherwise why call this method?).
    /// </summary>
    [JsonProperty("delete_original")]
    [JsonPropertyName("delete_original")]
    public bool DeleteOriginal { get; init; } = true;
}
