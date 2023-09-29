using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack.Payloads;

/// <summary>
/// Base class for a message payload. Either <see cref="BlockActionsPayload"/> or
/// <see cref="InteractiveMessagePayload"/>
/// </summary>
public abstract record MessagePayload<TMessage> : InteractionPayload, IMessagePayload<TMessage> where TMessage : SlackMessage
{
    /// <summary>
    /// Constructs a <see cref="MessagePayload{TMessage}"/>.
    /// </summary>
    /// <param name="type"></param>
    protected MessagePayload(string type) : base(type)
    {
    }

    /// <summary>
    /// Where it all happened — the user inciting this action clicked a button on a message contained within a channel,
    /// and this property presents the Id and Name of that channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public ChannelInfo Channel { get; init; } = null!;

    /// <summary>
    /// The source or original message.
    /// </summary>
    public abstract TMessage Message { get; set; }
}
