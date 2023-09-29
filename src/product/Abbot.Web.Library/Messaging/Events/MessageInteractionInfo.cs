using System;
using System.Linq;
using Serious.Payloads;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;

namespace Serious.Abbot.Events;

/// <summary>
/// Contains information about an interactive event such as when the user clicks on a button in a message.
/// </summary>
public record MessageInteractionInfo
{
    /// <summary>
    /// Creates an <see cref="MessageInteractionInfo"/> from an <see cref="IInteractionPayload"/>.
    /// </summary>
    /// <param name="interactionPayload">Contains information about an interaction with a Block Kit message.</param>
    /// <param name="arguments">The arguments passed to the skill.</param>
    /// <param name="callbackInfo"></param>
    public MessageInteractionInfo(IInteractionPayload interactionPayload, string arguments, CallbackInfo callbackInfo)
    {
        Arguments = arguments;
        CallbackInfo = callbackInfo;

        (ActivityId, ResponseUrl) = interactionPayload switch
        {
            InteractiveMessagePayload payload => (payload.MessageTimestamp, payload.ResponseUrl),
            IMessageBlockActionsPayload payload => (payload.Container.MessageTimestamp ?? string.Empty, payload.ResponseUrl),
            MessageActionPayload payload => (payload.MessageTimestamp, payload.ResponseUrl),
            _ => (string.Empty, null),
        };

#pragma warning disable CA1508
        Ephemeral = interactionPayload is IMessageBlockActionsPayload { Container.IsEphemeral: true };
#pragma warning restore CA1508

        TriggerId = interactionPayload.TriggerId;

        SourceMessage = interactionPayload is IMessagePayload<SlackMessage> messagePayload
            ? messagePayload.Message
            : null;

        ActionElement = interactionPayload is IBlockActionsPayload blockActionsPayload
            ? blockActionsPayload.Actions.SingleOrDefault()
            : null;
    }

    public string ActivityId { get; }

    /// <summary>
    /// The arguments passed to the skill.
    /// </summary>
    public string Arguments { get; }

    /// <summary>
    /// Information about the skill to call.
    /// </summary>
    public CallbackInfo CallbackInfo { get; }

    /// <summary>
    /// Contains a trigger ID that can be used to perform operations in response to this event.
    /// </summary>
    public string TriggerId { get; }

    /// <summary>
    /// If the interaction is a block_actions payload, then this will contain the element that was interacted with.
    /// </summary>
    public IPayloadElement? ActionElement { get; }

    /// <summary>
    /// If the interaction is with a block kit element in a message, this contains the source message.
    /// </summary>
    public SlackMessage? SourceMessage { get; }

    /// <summary>
    /// If <c>true</c>, this is an interaction with an ephemeral message.
    /// </summary>
    public bool Ephemeral { get; }

    /// <summary>
    /// The response url to edit or update the message.
    /// </summary>
    public Uri? ResponseUrl { get; }
}
