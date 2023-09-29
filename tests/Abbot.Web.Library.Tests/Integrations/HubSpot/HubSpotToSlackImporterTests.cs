using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.Extensions.Options;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Repositories;
using Serious.TestHelpers;
using Xunit;

public class HubSpotToSlackImporterTests
{
    public class TestData : CommonTestData
    {
        public const long ThreadId = 111111222;
        public const string MessageId = "56aa7bfad2ecf37c";
        public const long TicketId = 8675309;
        public const long PortalId = 1234567890;
        public Room Room { get; private set; } = null!;
        public Conversation Conversation { get; private set; } = null!;
        public Integration Integration { get; private set; } = null!;
        public IHubSpotClient HubSpotClient { get; private set; } = null!;
        public int SettingId { get; private set; }

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await base.SeedAsync(env);

            // Message synchronization setting.
            SettingId = (await env.Settings.SetAsync(SettingsScope.HubSpotPortal(PortalId),
                name: $"HubSpotMessageImport:{ThreadId}:{MessageId}",
                value: "Anything",
                env.TestData.Abbot.User)).Id;
            Room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            Conversation = await env.CreateConversationAsync(Room);
            Integration = await env.Integrations.EnableAsync(Organization, IntegrationType.HubSpot, Abbot);
            Integration.ExternalId = "1234567890";
            await env.Integrations.SaveSettingsAsync(
                Integration,
                new HubSpotSettings
                {
                    AccessToken = env.Secret("access_token"),
                    RefreshToken = env.Secret("refresh_token"),
                    RedirectUri = "https://example.com",
                    HubDomain = "hub_domain",
                });
            HubSpotClient = env.HubSpotClientFactory.ClientFor("access_token");
            await env.HubSpotLinker.CreateConversationTicketLinkAsync(
                TicketId,
                PortalId,
                Conversation,
                HubSpotClient,
                Abbot);
        }

        public HubSpotMessage CreateHubSpotMessage(
            string text,
            string? richText = null,
            string direction = "OUTGOING",
            string client = "HUBSPOT",
            string? clientIntegrationAppId = null)
        {
            return new HubSpotMessage(
                Id: MessageId,
                CreatedAt: "2022-12-07T00:32:21.721Z",
                UpdatedAt: "2022-12-07T00:32:21.852Z",
                Archived: false,
                CreatedBy: "A-46685293",
                Client: new HubSpotClient(client, clientIntegrationAppId),
                Senders: new[]
                {
                    new HubSpotSender(
                        ActorId: "A00001",
                        Name: "Michelle",
                        SenderField: "FROM",
                        DeliveryIdentifier: new DeliveryIdentifier(
                            Type: "HS_EMAIL_ADDRESS",
                            Value: "support@22761544.hs-inbox.com"))
                },
                Recipients: new[]
                {
                    new HubSpotRecipient(
                        ActorId: "E-haacked@example.com",
                        RecipientField: "TO",
                        DeliveryIdentifiers: new[]
                        {
                            new DeliveryIdentifier(
                                Type: "HS_EMAIL_ADDRESS",
                                Value: "haacked@example.com")
                        })
                },
                Text: text,
                RichText: richText ?? $"<div>{text}</div>",
                Subject: "Yo",
                TruncationStatus: "TRUNCATED_TO_MOST_RECENT_REPLY",
                InReplyToId: "2cd4672286504f869af05f178ac505d1",
                Status: new HubSpotMessageStatus("StatusType"),
                Direction: direction,
                ChannelId: "1002",
                ChannelAccountId: "89315249",
                Type: "MESSAGE");
        }
    }

    public class TheImportMessageAsyncMethod
    {
        [Theory]
        [InlineData("HUBSPOT", null)]
        [InlineData("INTEGRATION", "0000223344")]
        public async Task ImportsOutgoingMessagesFromHubSpotOrAnotherIntegration(string client, string? integrationAppId)
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .Configure<HubSpotOptions>(o => o.AppId = "9911223344")
                .Build();
            var hubSpotClient = env.TestData.HubSpotClient;
            var hubSpotMessage = env.TestData.CreateHubSpotMessage(
                "Hey there!",
                direction: "OUTGOING",
                client: client);
            hubSpotClient.GetAssociationsAsync(
                    fromObjectType: HubSpotObjectType.Conversation,
                    TestData.ThreadId,
                    toObjectType: HubSpotObjectType.Ticket)
                .Returns(new HubSpotApiResults<HubSpotAssociation>(new[]
                {
                    new HubSpotAssociation(TestData.TicketId, new[]
                    {
                        new HubSpotAssociationType("HUBSPOT_DEFINED", 31, null)
                    })
                }));
            hubSpotClient.GetMessageAsync(TestData.ThreadId, TestData.MessageId).Returns(hubSpotMessage);
            var importer = env.Activate<HubSpotToSlackImporter>();

            var message = await importer.ImportMessageAsync(env.TestData.SettingId, TestData.MessageId, TestData.ThreadId, TestData.PortalId);

            Assert.NotNull(message);
            Assert.Equal("Hey there!", message.Text);

            // Someday: test better; maybe Verify?
            var published = Assert.Single(env.ConversationPublisher.PublishedMessages);
            Assert.Equal(typeof(NewMessageInConversation), published.MessageType);
        }

        [Theory]
        [InlineData("OUTGOING", "INTEGRATION", "9911223344")]
        [InlineData("INCOMING", "HUBSPOT", null)]
        public async Task IgnoresIncomingMessagesOrMessagesFromAbbotMessages(
            string direction,
            string client,
            string? clientIntegrationAppId)
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .Configure<HubSpotOptions>(o => o.AppId = "9911223344")
                .Build();
            var hubSpotClient = env.TestData.HubSpotClient;
            var hubSpotMessage = env.TestData.CreateHubSpotMessage(
                "Hey there!",
                direction: direction,
                client: client,
                clientIntegrationAppId: clientIntegrationAppId);
            hubSpotClient.GetAssociationsAsync(
                    fromObjectType: HubSpotObjectType.Conversation,
                    TestData.ThreadId,
                    toObjectType: HubSpotObjectType.Ticket)
                .Returns(new HubSpotApiResults<HubSpotAssociation>(new[]
                {
                    new HubSpotAssociation(TestData.TicketId, new[]
                    {
                        new HubSpotAssociationType("HUBSPOT_DEFINED", 31, null)
                    })
                }));
            hubSpotClient.GetMessageAsync(TestData.ThreadId, TestData.MessageId).Returns(hubSpotMessage);
            var importer = env.Activate<HubSpotToSlackImporter>();

            var message = await importer.ImportMessageAsync(env.TestData.SettingId, TestData.MessageId, TestData.ThreadId, TestData.PortalId);

            Assert.Null(message);

            Assert.Empty(env.ConversationPublisher.PublishedMessages);
        }
    }
}
