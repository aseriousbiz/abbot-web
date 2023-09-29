using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Conversations;
using Serious.Slack.InteractiveMessages;
using Xunit;

public class SlackThreadExporterTests
{
    public class TheRetrieveMessagesAsyncMethod
    {
        [Fact]
        public async Task ShouldRetrieveExportedMessages()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.SlackApi.Conversations.AddConversationRepliesResponse(
                organization.ApiToken!.Reveal(),
                "C1234567890",
                "1657653155.174779",
                new SlackMessage[] {
                    new() { Timestamp = "1657653155.174779", SourceTeam = "T00000001", User = "U000001" },
                    new() { Timestamp = "1657653155.174780", User = "U000002" },
                    new() { Timestamp = "1657653155.174781", SourceTeam = "T00000001", User = "U000001" },
                    new() { Timestamp = "1657653155.174782", User = "U000002" },
                }
            );
            var exporter = env.Activate<SlackThreadExporter>();
            var setting = await exporter.ExportThreadAsync(
                "1657653155.174779",
                "C1234567890",
                organization,
                env.TestData.Member);

            Assert.NotNull(setting);
            var retrieved = await exporter.RetrieveMessagesAsync(setting.Name, organization);
            Assert.Collection(retrieved,
                m => Assert.Equal("1657653155.174779", m.Timestamp),
                m => Assert.Equal("1657653155.174780", m.Timestamp),
                m => Assert.Equal("1657653155.174781", m.Timestamp),
                m => Assert.Equal("1657653155.174782", m.Timestamp));
        }
    }
}
