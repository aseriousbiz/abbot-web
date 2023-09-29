using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Xunit;

// ReSharper disable StaticMemberInGenericType

public class ConversationPageTestBase<TPage> : PageTestBase<TPage>, IAsyncLifetime where TPage : PageModel
{
    static readonly DateTime Jan012020 = new(2020, 01, 01);
    static readonly DateTime Jan012021 = new(2021, 01, 01);
    static readonly DateTime Jan012022 = new(2022, 01, 01);

    public Organization TestOrganization2 => Env.TestData.ForeignOrganization;

    public Room TestRoom2 { get; private set; } = null!;

    public Member TestMember2 => Env.TestData.ForeignMember;

    public async Task InitializeAsync()
    {
        TestRoom2 = await Rooms.CreateAsync(new Room()
        {
            Name = "test-room",
            OrganizationId = TestOrganization2.Id,
            PlatformRoomId = "C0002",
        });

        TestRoom.TimeToRespond = new Threshold<TimeSpan>(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2));
        await Db.SaveChangesAsync();

        // Seed the conversations themselves.
        var overdue = await Conversations.CreateAsync(
            TestRoom,
            new MessagePostedEvent
            {
                MessageId = "0000",
                MessageUrl = new Uri("https://example.com/messages/0")
            },
            "Convo0",
            TestMember,
            Jan012020,
            null);
        overdue.State = ConversationState.Overdue;
        var convo = await Conversations.CreateAsync(
            TestRoom2,
            new MessagePostedEvent
            {
                MessageId = "0001",
                MessageUrl = new Uri("https://example.com/messages/1")
            },
            "Convo1",
            TestMember2,
            Jan012021,
            null);
        await Conversations.ArchiveAsync(convo, TestMember2, Jan012021);

        var overdue2 = await Conversations.CreateAsync(
            TestRoom,
            new MessagePostedEvent
            {
                MessageId = "0002",
                MessageUrl = new Uri("https://example.com/messages/2")
            },
            "Convo2",
            TestMember,
            Jan012022,
            null);

        overdue2.State = ConversationState.Overdue;
        await Db.SaveChangesAsync();
        convo = await Conversations.CreateAsync(
            TestRoom,
            new MessagePostedEvent
            {
                MessageId = "0003",
                MessageUrl = new Uri("https://example.com/messages/3")
            },
            "Convo3",
            TestMember,
            Jan012021,
            null);
        await Conversations.ArchiveAsync(convo, TestMember2, Jan012021);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
