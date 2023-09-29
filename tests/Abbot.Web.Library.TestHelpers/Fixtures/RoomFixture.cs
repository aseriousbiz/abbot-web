using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;

namespace Abbot.Common.TestHelpers;

public record RoomFixture(
    TestEnvironmentWithData Environment,
    Room Room,
    Member Member,
    DateTime OverdueDate,
    DateTime WarningDate,
    DateTime OkDate)
{
    public static async Task<RoomFixture> CreateAsync(
        TestEnvironmentWithData env,
        DateTime nowUtc,
        Member firstResponder,
        bool hasSlo,
        Organization? organization = null,
        RoomRole? roomRole = null)
    {
        organization ??= env.TestData.Organization;
        var room = await env.CreateRoomAsync(managedConversationsEnabled: true, org: organization);
        await env.Rooms.AssignMemberAsync(room, firstResponder, roomRole ?? RoomRole.FirstResponder, env.TestData.Member);

        if (hasSlo)
        {
            room.TimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(3));
        }

        return new RoomFixture(
            env,
            room,
            firstResponder,
            nowUtc.AddDays(-4),
            nowUtc.AddDays(-2),
            nowUtc.AddHours(-4));
    }

    public async Task<IReadOnlyList<Conversation>> SetupConversationsAsync(params ConversationFixture[] fixtures)
    {
        var conversations = new List<Conversation>();
        foreach (var fixture in fixtures)
        {
            var conversation = await Environment.CreateConversationAsync(
                Room,
                fixture.Title,
                fixture.LastStateChangedDate);

            conversation.LastStateChangeOn = fixture.LastStateChangedDate;
            conversation.State = fixture.State;
            conversations.Add(conversation);
        }
        await Environment.Db.SaveChangesAsync();
        return conversations;
    }
}
