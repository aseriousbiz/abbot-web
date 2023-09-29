using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Payloads;
using Serious.Slack.BlockKit;
using Serious.Slack.Converters;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack.Payloads;

/// <summary>
/// A <see cref="BlockActionsPayload"/> in response to an interaction with a message.
/// </summary>
[Element("block_actions", Discriminator = "message")]
public record MessageBlockActionsPayload : BlockActionsPayload, IMessageBlockActionsPayload
{
    /// <summary>
    /// Information about the message this payload contains.
    /// </summary>
    [JsonProperty("container")]
    [JsonPropertyName("container")]
    public MessageContainer Container { get; init; } = null!;

    /// <summary>
    /// Where it all happened — the user inciting this action clicked a button on a message contained within a channel,
    /// and this property presents the Id and Name of that channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public ChannelInfo Channel { get; init; } = null!;

    /// <summary>
    /// The source message the user initiated the interaction from. This will include the full state of the
    /// message, or the view within a modal or Home tab. If the source was an ephemeral message, this field will
    /// not be included. This may be <c>null</c> for ephemeral messages.
    /// </summary>
    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public SlackMessage? Message { get; init; }

    /// <summary>
    /// When a message contains stateful interactive components, this contains the state of those components.
    /// </summary>
    [JsonProperty("state")]
    [JsonPropertyName("state")]
    public BlockActionsState? State { get; init; }
}

/// <summary>
/// Information about the message the event payload contains.
/// </summary>
/// <param name="MessageTimestamp">The Slack timestamp of the message used to identify the message.</param>
/// <param name="IsEphemeral">Whether or not the message is ephemeral.</param>
/// <param name="ChannelId">The Slack Id of the channel the message was sent to.</param>
public record MessageContainer(
    [property: JsonProperty("message_ts")][property: JsonPropertyName("message_ts")] string? MessageTimestamp,
    [property: JsonProperty("is_ephemeral")][property: JsonPropertyName("is_ephemeral")] bool IsEphemeral,
    [property: JsonProperty("channel_id")][property: JsonPropertyName("channel_id")] string? ChannelId);

/// <summary>
/// Contains the state of any stateful interactive components in the message or view.
/// </summary>
/// <param name="Values">
/// A dictionary of objects keyed with the <c>block_ids</c> of all blocks containing stateful, interactive components.
/// Within each <c>block_id</c> object is another object keyed by the <c>action_id</c> of the child block element.
/// This final child object will contain the type and value of the action block element.
/// </param>
public record BlockActionsState(IReadOnlyDictionary<string, Dictionary<string, IPayloadElement>> Values)
{
    /// <summary>
    /// Tries to retrieve the payload element corresponding to the <paramref name="blockId"/> and
    /// <paramref name="actionId"/> cast to to the specified type.
    /// </summary>
    /// <param name="blockId">The ID of the block containing the element to retrieve.</param>
    /// <param name="actionId">The action ID of the element to retrieve. Can be <c>null</c> if (and only if) the block contains only a single element.</param>
    /// <param name="element">The resulting element.</param>
    /// <typeparam name="TElement">The expected type of the element.</typeparam>
    /// <returns><c>true</c> if the element exists and can be cast to the specified type.</returns>
    public bool TryGetAs<TElement>(string blockId, string? actionId, [NotNullWhen(true)] out TElement? element)
        where TElement : IPayloadElement
    {
        if (!Values.TryGetValue(blockId, out var elements))
        {
            element = default;
            return false;
        }

        if (elements.Count == 1 && elements.Single().Value is TElement typedElement)
        {
            element = typedElement;
            return true;
        }

        if (actionId is not null && elements.TryGetValue(actionId, out var payloadElement)
            && payloadElement is TElement typedPayloadElement)
        {
            element = typedPayloadElement;
            return true;
        }

        element = default;
        return false;
    }

    /// <summary>
    /// Tries to retrieve the payload element corresponding to the <paramref name="blockId"/> and
    /// <paramref name="actionId"/> cast to to the specified type. If it can't be found or cast to the type, it
    /// throws an exception.
    /// </summary>
    /// <param name="blockId">The block Id.</param>
    /// <param name="actionId">The action Id.</param>
    /// <typeparam name="TElement">The expected type of the element.</typeparam>
    /// <returns>The resulting element.</returns>
    /// <exception cref="KeyNotFoundException">
    /// An element of type <typeparamref name="TElement"/> was not found for the given
    /// <paramref name="blockId"/> and <paramref name="actionId"/>.
    /// </exception>
    public TElement GetAs<TElement>(string blockId, string? actionId)
        where TElement : IPayloadElement
    {
        if (TryGetAs<TElement>(blockId, actionId, out var element))
        {
            return element;
        }

        throw new KeyNotFoundException($"Could not find element of type {typeof(TElement)} with blockId '{blockId}' and actionId '{actionId}'");
    }

    readonly RecordBinder _recordBinder = new();

    object? GetBoundValue(BindAttribute? bindAttribute)
    {
        bool TryGetElement(BindAttribute attribute, [NotNullWhen(true)] out IPayloadElement? payloadElement)
            => TryGetAs(attribute.BlockId, bindAttribute.ActionId, out payloadElement);

        if (bindAttribute is not null && TryGetElement(bindAttribute, out var element))
        {
            object? value = element switch
            {
                IValueElement valueElement => valueElement.Value,
                IMultiValueElement multiValueElement => multiValueElement.Values,
                _ => null
            };
            return value;
        }

        return null;
    }

    /// <summary>
    /// Instantiates a new instance of the specified <typeparamref name="TActionsState"/> type and populates it
    /// by examining the <see cref="BindAttribute" /> applied to the properties of the object.
    /// </summary>
    /// <remarks>
    /// We don't enforce that the type is a record, but we do make the assumption that the properties and constructor
    /// arguments line up exactly. We can make this smarter in the future.
    /// </remarks>
    /// <typeparam name="TActionsState">The record type with a single constructor to map this <see cref="BlockActionsState"/> to.</typeparam>
    /// <returns><c>true</c> if the specified type can be populated from this <see cref="BlockActionsState"/>.</returns>
    public bool TryBindRecord<TActionsState>([NotNullWhen(true)] out TActionsState? bound)
        where TActionsState : class
    {
        return _recordBinder.TryBindRecord(GetBoundValue, out bound);
    }

    /// <summary>
    /// Instantiates a new instance of the specified <typeparamref name="TActionsState"/> type and populates it
    /// by examining the <see cref="BindAttribute" /> applied to the properties of the object.
    /// </summary>
    /// <remarks>
    /// We don't enforce that the type is a record, but we do make the assumption that the properties and constructor
    /// arguments line up exactly. We can make this smarter in the future.
    /// </remarks>
    /// <typeparam name="TActionsState">The record type with a single constructor to map this <see cref="BlockActionsState"/> to.</typeparam>
    /// <returns>The <typeparamref name="TActionsState"/> populated from this <see cref="BlockActionsState"/>.</returns>
    /// <exception cref="InvalidOperationException">Could not bind to <typeparamref name="TActionsState"/>.</exception>
    public TActionsState BindRecord<TActionsState>()
        where TActionsState : class
    {
        if (TryBindRecord<TActionsState>(out var bound))
        {
            return bound;
        }

        throw new InvalidOperationException($"Could not bind to type {typeof(TActionsState)}.");
    }
}
