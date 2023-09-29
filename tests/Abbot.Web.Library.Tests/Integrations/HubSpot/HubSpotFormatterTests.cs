using System.Threading.Tasks;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Messaging;
using Serious.Abbot.Scripting;
using Xunit;

namespace Abbot.Web.Library.Tests.Integrations.HubSpot;

public class HubSpotFormatterTests : FormatterTestBase
{
    [Fact]
    public async Task CorrectlyFormatsSlackBlocks()
    {
        var slackResolver = Substitute.For<ISlackResolver>();

        var formatter = new HubSpotFormatter(slackResolver);

        var result = await formatter.FormatBlocksAsync(
            new Organization(),
            TestSlackBlocks());

        Assert.Equal(
            "\n\n \n\n" +
            "Something unstyled with <html>.\n" +
            "**Something bold.**\n" +
            "***Something bold and italic.***\n" +
            "*~~Something italic and strike.~~*\n" +
            "***~~`Something bold, italic, code, and strike with a <div>.`~~***\n" +
            "[https://example.com](https://example.com)\n" +
            "[***https://example.com***](https://example.com)\n" +
            "[An unstyled link](https://example.com)\n" +
            "[**A bold link**](https://example.com)",
            result);
    }

    [Fact]
    public async Task CorrectlyFormatsSlackMentions()
    {
        var org = new Organization
        {
            PlatformType = PlatformType.Slack,
            Domain = "org.slack.com",
        };

        var slackResolver = Substitute.For<ISlackResolver>();

        var formatter = new HubSpotFormatter(slackResolver);

        var result = await formatter.FormatBlocksAsync(
            org,
            TestSlackMentions(org, slackResolver));

        Assert.Equal(
            @"[(unknown user)](https://org.slack.com/team/UB40)" +
            @"[**(unknown user)**](https://org.slack.com/team/UB40)" +
            @"[@Submarine](https://org.slack.com/team/U03DYLAKR6U)" +
            @"[(unknown channel)](https://org.slack.com/archives/R5D4)" +
            @"[***(unknown channel)***](https://org.slack.com/archives/R5D4)" +
            @"[#Cantina](https://org.slack.com/archives/C3PO)" +
            @"[(unknown group)](https://org.slack.com/threads/user_groups/S3R10U5)" +
            @"[*(unknown group)*](https://org.slack.com/threads/user_groups/S3R10U5)" +
            // Resolve names again resolver
            @"[@Submarine](https://org.slack.com/team/U03DYLAKR6U)" +
            @"[#Cantina](https://org.slack.com/archives/C3PO)" +
            "", // Intentional; less churn adding new test cases
            result);
    }
}
