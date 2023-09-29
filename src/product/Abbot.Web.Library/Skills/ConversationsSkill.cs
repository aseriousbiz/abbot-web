using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Humanizer;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Skills;

[Skill("conversations", Description = "Manage Conversation Tracking", RequirePlanFeature = PlanFeature.ConversationTracking)]
public class ConversationsSkill : ISkill
{
    const string NotInValidRoomErrorMessage = "Sorry, I cannot track conversations here.";
    readonly IRoomRepository _roomRepository;
    readonly ISettingsManager _settingsManager;

    public ConversationsSkill(IRoomRepository roomRepository, ISettingsManager settingsManager)
    {
        _roomRepository = roomRepository;
        _settingsManager = settingsManager;
    }

    public async Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        if (!messageContext.Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            await messageContext.SendActivityAsync(
                $"Sorry, conversation tracking isn't included in your plan. Contact us at `{WebConstants.SupportEmail}` to discuss upgrading!");
            return;
        }

        var (command, rest) = messageContext.Arguments.Pop();

        if (string.IsNullOrWhiteSpace(command))
        {
            await ReplyWithUsageAsync(messageContext, "You didn’t specify a command!");
            return;
        }

        Room room;
        if (command is "for")
        {
            if (rest.Count < 1 || rest[0] is not IRoomArgument roomArg)
            {
                await ReplyWithUsageAsync(messageContext, "You didn’t specify a room name.");
                return;
            }

            var foundRoom =
                await _roomRepository.GetRoomByPlatformRoomIdAsync(roomArg.Room.Id, messageContext.Organization);

            if (foundRoom is null)
            {
                var message =
                    $@"I couldn’t find the room '{roomArg.Room.Name}' in my records. Maybe I’m not a member of the room?";

                await messageContext.SendActivityAsync(message);
                return;
            }

            room = foundRoom;
            rest = rest.Skip(1);
            (command, rest) = rest.Pop();
        }
        else if (messageContext.Room.Persistent)
        {
            room = messageContext.Room;
        }
        else
        {
            await messageContext.SendActivityAsync(NotInValidRoomErrorMessage);
            return;
        }

        await (command switch
        {
            "" => ReplyWithUsageAsync(messageContext, "You didn’t specify a command!"),
            "debug" when messageContext.FromMember.IsStaff() => ManageDebugModeAsync(messageContext, room, rest),
            "info" => GetConversationInfoAsync(messageContext),
            "track" => StartTrackingAsync(room, messageContext),
            "untrack" => StopTrackingAsync(room, messageContext),
            "status" => GetStatusAsync(room, messageContext),
            "roles" => messageContext.SendActivityAsync(@"Available Roles:
• first-responder - Individuals responsible for responding to new conversations.
• escalation-responder - Individuals notified of overdue conversation."),
            "first-responder" or "fr" or "first-responders" or "frs" => ManageRoleAsync(RoomRole.FirstResponder,
                rest,
                room,
                messageContext),
            "escalation-responder" or "er" or "escalation-responders" or "ers" => ManageRoleAsync(RoomRole.EscalationResponder,
                rest,
                room,
                messageContext),
            "help" => ReplyWithUsageAsync(messageContext, null),
            var x => ReplyWithUsageAsync(messageContext, $"Unknown command or role '{x}'"),
        });
    }

    async Task ManageDebugModeAsync(MessageContext messageContext, Room room, IArguments args)
    {
        var scope = SettingsScope.Room(room);
        var (subcommand, _) = args.Pop();

        var currentState = await _settingsManager.GetBooleanValueAsync(scope, ConversationTracker.DebugModeSettingName);
        if (subcommand is "on")
        {
            await _settingsManager.SetBooleanValueAsync(scope,
                ConversationTracker.DebugModeSettingName,
                true,
                messageContext.From);
            currentState = true;
        }
        else if (subcommand is "off")
        {
            await _settingsManager.SetBooleanValueAsync(scope,
                ConversationTracker.DebugModeSettingName,
                false,
                messageContext.From);
            currentState = false;
        }

        await messageContext.SendActivityAsync("Debug mode is " + (currentState ? "enabled" : "disabled"));
    }

    static async Task GetConversationInfoAsync(MessageContext messageContext)
    {
        if (messageContext.Conversation is null)
        {
            await messageContext.SendActivityAsync("This message is not part of a conversation.");
            return;
        }

        var info = @$"Conversation ID: `{messageContext.Conversation.Id}`
Started {messageContext.Conversation.Created.Humanize()} by {GetUserReference(messageContext.Conversation.StartedBy)}
Title: `{messageContext.Conversation.Title}`
Last message was {messageContext.Conversation.LastMessagePostedOn.Humanize()}

Participants:

{messageContext.Conversation.Members.Select(m => GetUserReference(m.Member)).ToMarkdownList()}";

        await messageContext.SendActivityAsync(info, inThread: true);
    }

    async Task ManageRoleAsync(RoomRole role, IArguments args, Room room, MessageContext messageContext)
    {
        var (subcommand, _) = args.Pop();
        await (subcommand switch
        {
            "" or "list" => ListRoleMembersAsync(role, room, messageContext),
            "add" or "assign" => AddRoleMembersAsync(role,
                room,
                messageContext.Mentions,
                messageContext),
            "remove" or "unassign" => RemoveRoleMembersAsync(role,
                room,
                messageContext.Mentions,
                messageContext),
            var x => ReplyWithUsageAsync(messageContext, $"Unknown command '{x}'"),
        });
    }

    static async Task ListRoleMembersAsync(RoomRole role, Room room, MessageContext messageContext)
    {
        var assignmentList = room.Assignments
            .Where(a => a.Role == role)
            .OrderBy(a => a.Member.User.DisplayName)
            .Select(a => GetUserReference(a.Member))
            .ToMarkdownList();

        if (assignmentList.Length > 0)
        {
            await messageContext.SendActivityAsync(
                $"The following users are assigned to the '{role.GetEnumMemberValueName()}' role:\n{assignmentList}");
        }
        else
        {
            await messageContext.SendActivityAsync($"No users are assigned to the '{role.GetEnumMemberValueName()}' role.");
        }
    }

    async Task AddRoleMembersAsync(
        RoomRole role,
        Room room,
        IReadOnlyList<Member> members,
        MessageContext messageContext)
    {
        var builder = new StringBuilder();
        foreach (var mention in members)
        {
            if (await _roomRepository.AssignMemberAsync(room, mention, role, messageContext.FromMember))
            {
                builder.AppendLine(CultureInfo.InvariantCulture,
                    $"{GetUserReference(mention)} has been assigned the '{role.GetEnumMemberValueName()}' role.");
            }
            else
            {
                builder.AppendLine(CultureInfo.InvariantCulture,
                    $"{GetUserReference(mention)} already has the '{role.GetEnumMemberValueName()}' role.");
            }
        }

        if (builder.Length > 0)
        {
            builder.Length -= Environment.NewLine.Length;
            await messageContext.SendActivityAsync(builder.ToString());
        }
    }

    async Task RemoveRoleMembersAsync(
        RoomRole role,
        Room room,
        IReadOnlyList<Member> members,
        MessageContext messageContext)
    {
        var builder = new StringBuilder();
        foreach (var mention in members)
        {
            if (await _roomRepository.UnassignMemberAsync(room, mention, role, messageContext.FromMember))
            {
                builder.AppendLine(CultureInfo.InvariantCulture,
                    $"{GetUserReference(mention)} has been removed from the '{role.GetEnumMemberValueName()}' role.");
            }
            else
            {
                builder.AppendLine(CultureInfo.InvariantCulture,
                    $"{GetUserReference(mention)} is not assigned the '{role.GetEnumMemberValueName()}' role.");
            }
        }

        if (builder.Length > 0)
        {
            builder.Length -= Environment.NewLine.Length;
            await messageContext.SendActivityAsync(builder.ToString());
        }
    }

    async Task ReplyWithUsageAsync(MessageContext messageContext, string? additionalMessage)
    {
        if (additionalMessage is { Length: > 0 })
        {
            await messageContext.SendActivityAsync(additionalMessage);
        }

        await messageContext.SendHelpTextAsync(this);
    }

    static async Task GetStatusAsync(Room room, MessageContext messageContext)
    {
        var enabledMessage = room.ManagedConversationsEnabled
            ? "enabled"
            : "disabled";

        await messageContext.SendActivityAsync($"Conversation tracking is {enabledMessage} in {room.ToMention()}.");
    }

    async Task StopTrackingAsync(Room room, MessageContext messageContext)
    {
        if (room.ManagedConversationsEnabled)
        {
            await _roomRepository.SetConversationManagementEnabledAsync(room, enabled: false, messageContext.FromMember);
            await messageContext.SendActivityAsync(
                $"Conversation tracking has been disabled in {room.ToMention()}. Existing conversation data is preserved.");
        }
        else
        {
            await messageContext.SendActivityAsync(
                $"Conversation tracking is already disabled in {room.ToMention()}.");
        }
    }

    async Task StartTrackingAsync(Room room, MessageContext messageContext)
    {
        if (!room.ManagedConversationsEnabled)
        {
            await _roomRepository.SetConversationManagementEnabledAsync(room, enabled: true, messageContext.FromMember);
            await messageContext.SendActivityAsync(
                $"Conversation tracking has been enabled in {room.ToMention()}.");
        }
        else
        {
            await messageContext.SendActivityAsync(
                $"Conversation tracking is already enabled in {room.ToMention()}.");
        }
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        foreach (var (args, description) in GetUsages())
        {
            usage.Add(args, description);
        }
    }

    /// <summary>
    /// Gets a string to "reference" a user without mentioning them.
    /// We do this by generating a link to their profile instead of an `@mention`
    /// </summary>
    /// <param name="member">The member to reference.</param>
    static string GetUserReference(Member member)
    {
        var url = SlackFormatter.UserUrl(member.Organization.Domain, member.User.PlatformUserId);

        return $"[{member.DisplayName}]({url})";
    }

    static IEnumerable<(string, string)> GetUsages()
    {
        yield return ("for {room} {command}", "Run the provided command in the context of the specified room");
        yield return ("track", "Start tracking conversations in this room");
        yield return ("untrack", "Stop tracking conversations in this room");
        yield return ("status", "Get conversation tracking status for this room");
        yield return ("roles", "List available roles for this room");
        yield return ("{role} list", "List the assigned users for the specified role");
        yield return ("{role} assign {mentions...}", "Assign the mentioned users to the specified role");
        yield return ("{role} unassign {mentions...}", "Unassign the mentioned users from the specified role");
        yield return ("info", "Get metadata about the conversation in which this skill was invoked, if any");
    }
}
