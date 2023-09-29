using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Humanizer;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Skills;

[Skill("hubs",
    Description = "Manage Conversation Hubs",
    RequirePlanFeature = PlanFeature.ConversationTracking,
    RequireFeatureFlag = FeatureFlags.Hubs)]
public class HubsSkill : ISkill
{
    readonly IHubRepository _hubRepository;
    readonly IRoomRepository _roomRepository;
    readonly IClock _clock;
    readonly ISlackResolver _slackResolver;

    public HubsSkill(IHubRepository hubRepository, IRoomRepository roomRepository, IClock clock, ISlackResolver slackResolver)
    {
        _hubRepository = hubRepository;
        _roomRepository = roomRepository;
        _clock = clock;
        _slackResolver = slackResolver;
    }

    public async Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var (command, rest) = messageContext.Arguments.Pop();
        await (command switch
        {
            "create" => CreateHubAsync(messageContext),
            "attach" => AttachRoomAsync(messageContext, rest),
            "detach" => DetachRoomAsync(messageContext, rest),
            "rooms" => ListRoomsAsync(messageContext),
            "default" => DefaultHubAsync(messageContext, rest),
            "help" => ReplyWithUsageAsync(messageContext, null),
            "" => ReplyWithUsageAsync(messageContext),
            var x => ReplyWithUsageAsync(messageContext, $"Unknown command: {x}"),
        });
    }

    async Task DefaultHubAsync(MessageContext messageContext, IArguments rest)
    {
        var (op, arg) = rest.Pop();
        await (op switch
        {
            "" => GetDefaultHubAsync(messageContext),
            "set" when arg is [] =>
                SetDefaultHubAsync(messageContext),
            "set" when arg is [IRoomArgument room] =>
                SetDefaultHubAsync(messageContext, room),
            "set" =>
                ReplyWithUsageAsync(messageContext, $"Unexpected {"argument".ToQuantity(arg.Count, ShowQuantityAs.None)}: {arg}"),
            "unset" when arg is [] =>
                UnsetDefaultHubAsync(messageContext),
            "unset" =>
                ReplyWithUsageAsync(messageContext, $"Unexpected {"argument".ToQuantity(arg.Count, ShowQuantityAs.None)}: {arg}"),
            _ => ReplyWithUsageAsync(messageContext, $"Unknown operation: {op}"),
        });
    }

    async Task GetDefaultHubAsync(MessageContext messageContext)
    {
        var hub = await _hubRepository.GetDefaultHubAsync(messageContext.Organization);
        if (hub is null)
        {
            await messageContext.SendActivityAsync("No default Hub found.");
        }
        else
        {
            await messageContext.SendActivityAsync($"Default Hub is {hub.Room.ToMention()}");
        }
    }

    async Task SetDefaultHubAsync(MessageContext messageContext, IRoomArgument? roomArg = null)
    {
        var room = roomArg is null
            ? messageContext.Room
            : await _slackResolver.ResolveRoomAsync(roomArg.Room.Id, messageContext.Organization, false);

        if (room is null)
        {
            await messageContext.SendActivityAsync(
                $"Could not identify room `{roomArg}`. If it's a private room, make sure I am a member and try again.");
            return;
        }

        var hub = await _hubRepository.GetHubAsync(room);
        if (hub is null)
        {
            // Should we just auto-create?
            await messageContext.SendActivityAsync(
                $"Hub not found for {room.ToMention()}. " +
                (room.Id == messageContext.Room.Id ?
                    "Use `create` command first." :
                    "Use `create` command in that room first."));
            return;
        }

        var prevHub = await _hubRepository.SetDefaultHubAsync(hub, messageContext.FromMember);
        await messageContext.SendActivityAsync(
            prevHub is null
                ? $"Set default Hub to {room.ToMention()}."
                : hub == prevHub
                    ? $"{room.ToMention()} is already the default Hub."
                    : $"Changed default Hub from {prevHub.Room.ToMention()} to {room.ToMention()}.");
    }

    async Task UnsetDefaultHubAsync(MessageContext messageContext)
    {
        var hub = await _hubRepository.ClearDefaultHubAsync(messageContext.Organization, messageContext.FromMember);
        if (hub is null)
        {
            await messageContext.SendActivityAsync($"No default Hub set.");
            return;
        }

        await messageContext.SendActivityAsync($"{hub.Room.ToMention()} is no longer the default Hub.");
    }

    async Task ListRoomsAsync(MessageContext messageContext)
    {
        // Make sure we're in a Hub
        var hub = await _hubRepository.GetHubAsync(messageContext.Room);
        if (hub is null)
        {
            await messageContext.SendActivityAsync("This command must be run from within a Hub room.");
            return;
        }

        var defaultHub = await _hubRepository.GetDefaultHubAsync(messageContext.Organization);
        if (hub.Id == defaultHub?.Id)
        {
            await messageContext.SendActivityAsync("This is the default Hub.");
            return;
        }

        var rooms = await _hubRepository.GetAttachedRoomsAsync(hub);
        if (rooms is [])
        {
            await messageContext.SendActivityAsync(
                $"""
                This Hub has no rooms attached. Use `attach` command.
                """);
            return;
        }

        var roomList = new StringBuilder();
        await messageContext.SendActivityAsync(
            $"""
            The following rooms are attached to this Hub:
            {rooms.Select(room => room.ToMention()).ToMarkdownList()}
            """);
    }

    async Task AttachRoomAsync(MessageContext messageContext, IArguments rest)
    {
        var (forceArg, args) = rest.FindAndRemove(a => a.Value == "--force");

        // Make sure we're in a Hub
        var hub = await _hubRepository.GetHubAsync(messageContext.Room);
        if (hub is null)
        {
            await messageContext.SendActivityAsync("This command must be run from within a Hub room.");
            return;
        }

        // Get the room to attach
#pragma warning disable CA1826
        if (args.FirstOrDefault() is not IRoomArgument roomArg)
#pragma warning restore CA1826
        {
            await messageContext.SendActivityAsync("You must specify a room to attach to this Hub.");
            return;
        }

        var room = await _slackResolver.ResolveRoomAsync(roomArg.Room.Id, messageContext.Organization, false);
        if (room is null)
        {
            await messageContext.SendActivityAsync(
                $"Could not identify room `{roomArg.Value}`. If it's a private room, make sure I am a member and try again.");
            return;
        }

        if (forceArg.Value is "--force" && room.Hub is { } existingHub)
        {
            var detachResult = await _roomRepository.DetachFromHubAsync(room, existingHub, messageContext.FromMember);
            if (!detachResult.IsSuccess)
            {
                await messageContext.SendActivityAsync(
                    $"Failed to detach room {roomArg} from Hub {existingHub.Room.ToMention()}: {detachResult.ErrorMessage}.");

                return;
            }

            await messageContext.SendActivityAsync(
                $"Detached room {roomArg} from Hub {existingHub.Room.ToMention()}.");
        }

        var result = await _roomRepository.AttachToHubAsync(room, hub, messageContext.FromMember);
        if (result.IsSuccess)
        {
            await messageContext.SendActivityAsync(
                $"Attached {roomArg} to this Hub!");
        }
        else if (result.Type == EntityResultType.Conflict)
        {
            await messageContext.SendActivityAsync(
                $"Room {roomArg} is already attached to a Hub. Use `--force` to override this and replace the existing Hub attachment.");
        }
        else
        {
            await messageContext.SendActivityAsync(
                $"An unknown error occurred: {result.ErrorMessage}");
        }
    }

    async Task DetachRoomAsync(MessageContext messageContext, IArguments rest)
    {
        // Make sure we're in a Hub
        var hub = await _hubRepository.GetHubAsync(messageContext.Room);
        if (hub is null)
        {
            await messageContext.SendActivityAsync("This command must be run from within a Hub room.");
            return;
        }

        // Get the room to attach
#pragma warning disable CA1826
        if (rest.FirstOrDefault() is not IRoomArgument roomArg)
#pragma warning restore CA1826
        {
            await messageContext.SendActivityAsync("You must specify a room to attach to this Hub.");
            return;
        }

        var room = await _slackResolver.ResolveRoomAsync(roomArg.Room.Id, messageContext.Organization, false);
        if (room is null)
        {
            await messageContext.SendActivityAsync(
                $"Could not identify room `{roomArg.Value}`. If it's a private room, make sure I am a member and try again.");
            return;
        }

        var result = await _roomRepository.DetachFromHubAsync(room, hub, messageContext.FromMember);
        if (result.IsSuccess)
        {
            await messageContext.SendActivityAsync(
                $"Detached {roomArg} from this Hub!");
        }
        else if (result.Type == EntityResultType.Conflict)
        {
            var message = room.Hub is { Room: var existingHubRoom }
                ? $"Room {roomArg} is not attached to this Hub. It is attached to {existingHubRoom.ToMention()}."
                : $"Room {roomArg} is not attached to any Hub.";
            await messageContext.SendActivityAsync(message);
        }
        else
        {
            await messageContext.SendActivityAsync(
                $"An unknown error occurred: {result.ErrorMessage}");
        }
    }

    async Task CreateHubAsync(MessageContext messageContext)
    {
        // Ensure there isn't already a Hub in this room.
        var existingHub = await _hubRepository.GetHubAsync(messageContext.Room);
        if (existingHub is not null)
        {
            await messageContext.SendActivityAsync(
                $"This room is already associated with the Hub {existingHub.Room.ToMention()}.");

            return;
        }

        // Create the Hub
        await _hubRepository.CreateHubAsync(
            messageContext.Room.Name.Require(),
            messageContext.Room,
            messageContext.FromMember);

        await messageContext.SendActivityAsync("Hub created!");
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        foreach (var (args, description) in GetUsages())
        {
            usage.Add(args, description);
        }
    }

    static IEnumerable<(string, string)> GetUsages()
    {
        yield return ("create", "Create a new Hub in this room");
        yield return ("rooms", "Lists the rooms attached to this Hub");
        yield return ("attach #room [--force]", "Attaches #room to this Hub. If `--force` is specified, the room will be detached from its existing Hub, if any");
        yield return ("detach #room", "Detaches #room from this Hub, if it'is attached");
        yield return ("default", "Shows Organization default Hub");
        yield return ("default set [#room]", "Sets Organization default Hub to current room, or to #room if specified");
        yield return ("default unset", "Unsets Organization default Hub");
    }

    async Task ReplyWithUsageAsync(MessageContext messageContext, string? additionalMessage = null)
    {
        if (additionalMessage is { Length: > 0 })
        {
            await messageContext.SendActivityAsync(additionalMessage);
        }

        await messageContext.SendHelpTextAsync(this);
    }
}
