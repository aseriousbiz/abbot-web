using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Scripting;
using Serious.Logging;
using Serious.Slack;
using Serious.Tasks;

namespace Serious.Abbot.Messaging;

public class Reactor
{
    readonly IReactionsApiClient _reactionsApiClient;
    readonly ILogger<Reactor> _logger;

    public Reactor(IReactionsApiClient reactionsApiClient, ILogger<Reactor> logger)
    {
        _reactionsApiClient = reactionsApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Applies the given reaction to the given message, and removes it when the returned disposable is disposed.
    /// </summary>
    /// <param name="name">The reaction to apply.</param>
    /// <param name="messageContext">A <see cref="MessageContext"/> identifying the message to react to.</param>
    /// <returns>An <see cref="IAsyncDisposable"/> that will remove the reaction when disposed.</returns>
    public Task<IAsyncDisposable?> ReactDuringAsync(string name, MessageContext messageContext)
        => ReactDuringAsync(
            name,
            messageContext.Organization,
            messageContext.Room.PlatformRoomId,
            messageContext.MessageId.Require());

    /// <summary>
    /// Applies the given reaction to the given message, and removes it when the returned disposable is disposed.
    /// </summary>
    /// <param name="name">The reaction to apply.</param>
    /// <param name="organization">The <see cref="Organization"/> in which the message exists.</param>
    /// <param name="platformRoomId">The platform-specific room ID of the room in which the message exists.</param>
    /// <param name="messageId">The platform-specific message ID of the message to react to.</param>
    /// <returns>An <see cref="IAsyncDisposable"/> that will remove the reaction when disposed.</returns>
    public async Task<IAsyncDisposable?> ReactDuringAsync(string name, Organization organization,
        string platformRoomId, string messageId)
    {
        var added = await AddReactionAsync(name, organization, platformRoomId, messageId);
        return added
            ? AsyncDisposable.Create(async () => {
                await RemoveReactionAsync(name, organization, platformRoomId, messageId);
            })
            : null;
    }

    /// <summary>
    /// Applies the given reaction to the given message.
    /// </summary>
    /// <param name="name">The reaction to apply.</param>
    /// <param name="messageContext">A <see cref="MessageContext"/> identifying the message to react to.</param>
    /// <returns>A boolean indicating whether the reaction was successfully applied.</returns>
    public Task<bool> AddReactionAsync(string name, MessageContext messageContext)
        => AddReactionAsync(
            name,
            messageContext.Organization,
            messageContext.Room.PlatformRoomId,
            messageContext.MessageId.Require());

    /// <summary>
    /// Applies the given reaction to the given message.
    /// </summary>
    /// <param name="name">The reaction to apply.</param>
    /// <param name="organization">The <see cref="Organization"/> in which the message exists.</param>
    /// <param name="platformRoomId">The platform-specific room ID of the room in which the message exists.</param>
    /// <param name="messageId">The platform-specific message ID of the message to react to.</param>
    /// <returns>A boolean indicating whether the reaction was successfully applied.</returns>
    public async Task<bool> AddReactionAsync(string name, Organization organization,
        string platformRoomId, string messageId)
    {
        if (!organization.TryGetUnprotectedApiToken(out var apiToken)
            || !organization.HasRequiredScope("reactions:write"))
        {
            return false;
        }

        var response = await _reactionsApiClient.AddReactionAsync(
            apiToken,
            name,
            platformRoomId,
            messageId);

        try
        {
            if (!response.Ok)
            {
                _logger.ErrorAddingReaction(response.ToString());
            }

            return response.Ok;
        }
        catch (Exception e)
        {
            _logger.ExceptionAddingReaction(e);
        }

        return false;
    }

    /// <summary>
    /// Removes the given reaction from the given message.
    /// </summary>
    /// <param name="name">The reaction to remove.</param>
    /// <param name="messageContext">A <see cref="MessageContext"/> identifying the message to remove the reaction from.</param>
    /// <returns>A boolean indicating whether the reaction was successfully removed.</returns>
    public Task<bool> RemoveReactionAsync(string name, MessageContext messageContext)
        => RemoveReactionAsync(
            name,
            messageContext.Organization,
            messageContext.Room.PlatformRoomId,
            messageContext.MessageId.Require());

    /// <summary>
    /// Removes the given reaction from the given message.
    /// </summary>
    /// <param name="name">The reaction to remove.</param>
    /// <param name="organization">The <see cref="Organization"/> in which the message exists.</param>
    /// <param name="platformRoomId">The platform-specific room ID of the room in which the message exists.</param>
    /// <param name="messageId">The platform-specific message ID of the message to remove the reaction from.</param>
    /// <returns>A boolean indicating whether the reaction was successfully removed.</returns>
    public async Task<bool> RemoveReactionAsync(string name, Organization organization,
        string platformRoomId, string messageId)
    {
        if (!organization.TryGetUnprotectedApiToken(out var apiToken)
            || !organization.HasRequiredScope("reactions:write"))
        {
            return false;
        }

        var response = await _reactionsApiClient.RemoveReactionAsync(
            apiToken,
            name,
            platformRoomId,
            messageId);

        try
        {
            if (!response.Ok)
            {
                _logger.ErrorRemovingReaction(response.ToString());
            }

            return response.Ok;
        }
        catch (Exception e)
        {
            _logger.ExceptionRemovingReaction(e);
        }

        return false;
    }
}

static partial class ReactorLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Could not add reaction: {Error}.")]
    public static partial void ErrorAddingReaction(
        this ILogger<Reactor> logger,
        string error);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Could not add reaction.")]
    public static partial void ExceptionAddingReaction(
        this ILogger<Reactor> logger,
        Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Could not remove reaction: {Error}.")]
    public static partial void ErrorRemovingReaction(
        this ILogger<Reactor> logger,
        string error);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Could not remove reaction.")]
    public static partial void ExceptionRemovingReaction(
        this ILogger<Reactor> logger,
        Exception exception);
}
