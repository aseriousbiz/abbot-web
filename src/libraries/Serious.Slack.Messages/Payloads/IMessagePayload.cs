using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack.Payloads;

/// <summary>
/// Interface that describes incoming payloads that are in response to a message such as a
/// <see cref="BlockActionsPayload"/> (aka <c>block_actions</c>) or an <see cref="InteractiveMessagePayload"/>
/// (aka <c>interactive_message</c>).
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public interface IMessagePayload<out TMessage> : IInteractionPayload where TMessage : SlackMessage
{
    /// <summary>
    /// The source or original message.
    /// </summary>
    TMessage? Message { get; }

    /// <summary>
    /// Where it all happened — the user inciting this action clicked a button on a message contained within a channel,
    /// and this property presents the Id and Name of that channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    ChannelInfo Channel { get; }
}
