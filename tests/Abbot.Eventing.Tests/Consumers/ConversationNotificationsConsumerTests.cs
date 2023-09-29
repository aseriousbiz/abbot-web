using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot.Eventing.Consumers;

[UsesVerify]
public class ConversationNotificationsConsumerTests
{
    TestEnvironmentWithData CreateTestEnvironment()
    {
        return TestEnvironmentBuilder.Create()
            .AddBusConsumer<ConversationNotificationsConsumer>()
            .Build(snapshot: true);
    }

    static SettingsTask Verify(TestEnvironment env)
    {
        return Verifier.Verify(new {
            // Preserve the old snapshot format for simplicity.
            target = new {
                env.SlackApi.PostedMessages
            },
            logs = env.GetAllLogs(),
        });
    }

    [Theory]
    // Using a string instead of a series of bools makes the Verify snapshot name clearer.
    [InlineData("InvalidOrganization")]
    [InlineData("InvalidConversation")]
    [InlineData("InvalidHub")]
    [InlineData("MissingApiToken")]
    public async Task FailsIfPreconditionsMissing(string precondition)
    {
        var env = CreateTestEnvironment();
        var room = await env.CreateRoomAsync("Croom");
        var convo = await env.CreateConversationAsync(room);

        if (precondition is "MissingApiToken")
        {
            env.TestData.Organization.ApiToken = null;
            await env.Db.SaveChangesAsync();
        }

        if (precondition is "InvalidHub")
        {
            convo.HubId = 99;
            convo.HubThreadId = "123";
            await env.Db.SaveChangesAsync();
        }

        await env.PublishAndWaitForConsumptionAsync(new PublishConversationNotification
        {
            OrganizationId = precondition is "InvalidOrganization"
                ? new Id<Organization>(99)
                : env.TestData.Organization,
            ConversationId = precondition is "InvalidConversation"
                ? new Id<Conversation>(99)
                : convo,
            Broadcast = false,
            Notification = new()
            {
                Icon = "I",
                Headline = "H",
                Message = "M",
                Type = NotificationType.TicketCreated,
            },
        });

        await Verify(env).UseParameters(precondition);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendsNotificationToHubThreadIfConversationAttachedToHub(bool broadcast)
    {
        var env = CreateTestEnvironment();
        var room = await env.CreateRoomAsync("Croom");
        var hub = await env.CreateHubAsync("hub");
        var convo = await env.CreateConversationAsync(room);
        await env.Conversations.AttachConversationToHubAsync(convo,
            hub,
            "hub.thread",
            new Uri("https://example.com/hub-thread"),
            env.TestData.Member,
            env.Clock.UtcNow);

        await env.PublishAndWaitForConsumptionAsync(new PublishConversationNotification()
        {
            OrganizationId = env.TestData.Organization,
            ConversationId = convo,
            Broadcast = broadcast,
            Notification = new()
            {
                Icon = "I",
                Headline = "H",
                Message = "M",
                MentionGroups =
                {
                    new(NotificationRecipientType.Assignee, new[] { "Umention1", "Umention2" }),
                    new(NotificationRecipientType.FirstResponder, new[] { "Umention1", "Umention3" }),
                },
                Type = NotificationType.TicketCreated,
            },
        });

        await Verify(env).UseParameters(broadcast);
    }

    [Fact]
    public async Task SendsNotificationAsSeparateDMsToEachGroupIfConversationNotAttachedToHub()
    {
        var env = CreateTestEnvironment();
        var room = await env.CreateRoomAsync("Croom");
        var convo = await env.CreateConversationAsync(room);

        await env.PublishAndWaitForConsumptionAsync(new PublishConversationNotification()
        {
            OrganizationId = env.TestData.Organization,
            ConversationId = convo,
            Broadcast = false,
            Notification = new()
            {
                Icon = "I",
                Headline = "H",
                Message = "M",
                MentionGroups =
                {
                    new(NotificationRecipientType.Assignee, new[] { "Umention1", "Umention2" }),
                    new(NotificationRecipientType.FirstResponder, new[] { "Umention1", "Umention3" }),
                },
                Type = NotificationType.TicketCreated,
            },
        });

        await Verify(env);
    }

    [Fact]
    public async Task ReportsErrorIfUnableToResolveGroupDm()
    {
        var env = CreateTestEnvironment();
        var room = await env.CreateRoomAsync("Croom");
        var convo = await env.CreateConversationAsync(room);

        env.SlackApi.Conversations.AddConversationInfoResponse(
            env.TestData.Organization.RequireAndRevealApiToken(),
            "Umention1-Umention2",
            "jeepers_creepers");

        await env.PublishAndWaitForConsumptionAsync(new PublishConversationNotification()
        {
            OrganizationId = env.TestData.Organization,
            ConversationId = convo,
            Broadcast = false,
            Notification = new()
            {
                Icon = "I",
                Headline = "H",
                Message = "M",
                MentionGroups =
                {
                    new(NotificationRecipientType.Assignee, new[] { "Umention1", "Umention2" }),
                    new(NotificationRecipientType.FirstResponder, new[] { "Umention1", "Umention3" }),
                },
                Type = NotificationType.TicketCreated,
            },
        });

        await Verify(env);
    }
}
