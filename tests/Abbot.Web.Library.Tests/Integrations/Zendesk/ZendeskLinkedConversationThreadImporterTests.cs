using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Serious.Slack.InteractiveMessages;
using Xunit;

public class ZendeskLinkedConversationThreadImporterTests
{
    public class TheImportThreadAsyncMethod
    {
        [Fact]
        public async Task ImportsRepliesToLinkedMessage()
        {
            var env = TestEnvironment.Create();
            var customer = env.TestData.ForeignUser;
            var agent = env.TestData.User;
            var organization = env.TestData.Organization;
            var foreignOrganization = env.TestData.ForeignOrganization;
            await env.Integrations.EnableAsync(organization, IntegrationType.Zendesk, env.TestData.Member);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room, firstMessageId: "1657653155.174779");
            var messages = new SlackMessage[] {
                new() { Timestamp = "1657653155.174779", SourceTeam = foreignOrganization.PlatformId, User = customer.PlatformUserId },
                new() { Timestamp = "1657653155.174780", User = agent.PlatformUserId },
                new() { Timestamp = "1657653155.174781", SourceTeam = foreignOrganization.PlatformId, User = customer.PlatformUserId  },
                new() { Timestamp = "1657653155.174782", User = agent.PlatformUserId  },
            };
            await env.Settings.SetAsync(
                SettingsScope.Organization(organization),
                "setting-name",
                JsonConvert.SerializeObject(messages),
                agent);

            var link = await env.CreateConversationLinkAsync(
                conversation,
                ConversationLinkType.ZendeskTicket,
                "1234");
            var importer = env.Activate<ZendeskLinkedConversationThreadImporter>();

            await importer.ImportThreadAsync(link, "setting-name");

            var importedMessages = env.SlackToZendeskCommentImporter.ImportedMessages;
            Assert.Collection(importedMessages,
                m => {
                    Assert.Equal("1657653155.174780", m.MessageId);
                    Assert.Equal(agent.Id, m.From.UserId);
                    Assert.Equal(organization.Id, m.Organization.Id);
                },
                m => {
                    Assert.Equal("1657653155.174781", m.MessageId);
                    Assert.Equal(customer.Id, m.From.UserId);
                    Assert.Equal(foreignOrganization.Id, m.Organization.Id);
                },
                m => {
                    Assert.Equal("1657653155.174782", m.MessageId);
                    Assert.Equal(agent.Id, m.From.UserId);
                    Assert.Equal(organization.Id, m.Organization.Id);
                });
        }
    }
}
