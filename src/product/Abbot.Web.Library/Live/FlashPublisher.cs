using System.Collections.Generic;
using System.Linq;
using MassTransit;
using MassTransit.SignalR.Contracts;
using MassTransit.SignalR.Utils;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Serious.Logging;

namespace Serious.Abbot.Live;

/// <summary>
/// Provides a way to publish "Flashes".
/// A "Flash" is a request to trigger an event across a set of connected browser clients.
/// </summary>
public interface IFlashPublisher
{
    /// <summary>
    /// Publishes the specified <paramref name="flash"/> to all members of <paramref name="group"/>, with optional <paramref name="arguments"/>.
    /// </summary>
    /// <param name="flash">The name of the flash to publish. This is the event that will be triggered on each browser.</param>
    /// <param name="group">The group to publish the flash to. Only members of this group will receive the flash.</param>
    /// <param name="arguments">Optional arguments to provide, which will be available in the event "details" on the browser client.</param>
    Task PublishAsync(FlashName flash, FlashGroup group, params object[] arguments);
}

public class FlashPublisher : IFlashPublisher
{
    static readonly ILogger<FlashPublisher> Log =
        ApplicationLoggerFactory.CreateLogger<FlashPublisher>();

    readonly IPublishEndpoint _publishEndpoint;
    readonly IReadOnlyList<IHubProtocol> _hubProtocols;

    public FlashPublisher(IPublishEndpoint publishEndpoint, IEnumerable<IHubProtocol> hubProtocols)
    {
        _publishEndpoint = publishEndpoint;
        _hubProtocols = hubProtocols.ToList();
    }

    public virtual async Task PublishAsync(FlashName flash,
        FlashGroup group,
        params object[] arguments)
    {
        try
        {
            // We have to serialize the message for _all_ active SignalR Hub Protocols.
            // Because if we have multiple protocols active (MessagePack, JSON, etc.),
            // some connections may be using JSON, while others use MessagePack.
            // Fortunately, MassTransit has a .ToProtocolDictionary helper for us.
            var messages = _hubProtocols.ToProtocolDictionary("dispatchFlash", new object[]
            {
                new
                {
                    flash.Name,
                    Arguments = arguments,
                }
            });

            await _publishEndpoint.Publish<Group<FlashHub>>(new {
                GroupName = group.Name,
                Messages = messages,
            });
        }
        catch (Exception ex)
        {
            Log.FlashFailed(ex, flash, group);
        }
    }
}

static partial class FlashPublisherLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to publish {FlashName} to {FlashGroup}")]
    public static partial void FlashFailed(
        this ILogger<FlashPublisher> logger,
        Exception ex,
        FlashName flashName,
        FlashGroup flashGroup);
}
