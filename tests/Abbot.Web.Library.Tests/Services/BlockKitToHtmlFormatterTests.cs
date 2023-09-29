using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services;

public class BlockKitToHtmlFormatterTests
{
    public class TheFormatBlocksAsyncMethod : FormatterTestBase
    {
        [Fact]
        public async Task CorrectlyFormatsSlackBlocks()
        {
            var slackResolver = Substitute.For<ISlackResolver>();
            var formatter = new BlockKitToHtmlFormatter(slackResolver);

            var result = await formatter.FormatBlocksAsHtmlAsync(TestSlackBlocks(), new Organization());

            Assert.Equal(
                @"<br/><br/> <br/><br/>" +
                @"Something unstyled with &lt;html&gt;." +
                @"<span style=""font-weight: bold""><br/>Something bold.</span>" +
                @"<span style=""font-weight: bold; font-style: italic""><br/>Something bold and italic.</span>" +
                @"<span style=""font-style: italic; text-decoration: line-through""><br/>Something italic and strike.</span>"
                +
                @"<code style=""font-weight: bold; font-style: italic; text-decoration: line-through""><br/>Something bold, italic, code, and strike with a &lt;div&gt;.</code>"
                +
                "<br/>" +
                @"<a href=""https://example.com"">https://example.com</a>" +
                "<br/>" +
                @"<a href=""https://example.com"" style=""font-weight: bold; font-style: italic"">https://example.com</a>"
                +
                "<br/>" +
                @"<a href=""https://example.com"">An unstyled link</a>" +
                "<br/>" +
                @"<a href=""https://example.com"" style=""font-weight: bold"">A bold link</a>",
                result);
        }

        [Fact]
        public async Task CorrectlyFormatsCodeAndBlockQuotes()
        {
            var slackResolver = Substitute.For<ISlackResolver>();
            var formatter = new BlockKitToHtmlFormatter(slackResolver);

            var result = await formatter.FormatBlocksAsHtmlAsync(TestSlackCodeAndBlockQuotes(), new Organization());

            Assert.Equal(
                @"<span style=""font-weight: bold"">Something bold.<br/><br/></span>" +
                @"<pre>Style is ignored.</pre>" +
                @"<blockquote><span style=""font-weight: bold"">Style is allowed.</span></blockquote>",
                result);
        }

        [Fact]
        public async Task CorrectlyFormatsListItems()
        {
            var organization = new Organization
            {
                PlatformType = PlatformType.Slack,
                Domain = "org.slack.com",
            };
            var slackResolver = Substitute.For<ISlackResolver>();
            var formatter = new BlockKitToHtmlFormatter(slackResolver);

            var result = await formatter.FormatBlocksAsHtmlAsync(
                TestSlackListItemBlocks(organization, slackResolver),
                organization,
                HtmlFormatting.Indented);

            Assert.Equal("""
Here&#39;s some bullet points<br/><br/>
<div style="padding-left: 0px;">
    <ul style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">First item</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Mention <a href="https://org.slack.com/team/U03DYLAKR6U">@Submarine</a> and other things</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Third item</li>
    </ul>
</div>
<br/>And an ordered list<br/><br/>
<div style="padding-left: 0px;">
    <ol style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Item 1</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Item 2</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Item 3</li>
    </ol>
</div>

""",
                result);
        }

        [Fact]
        public async Task CorrectlyFormatsIndentedListItems()
        {
            var organization = new Organization
            {
                PlatformType = PlatformType.Slack,
                Domain = "org.slack.com",
            };
            var slackResolver = Substitute.For<ISlackResolver>();
            var formatter = new BlockKitToHtmlFormatter(slackResolver);

            var result = await formatter.FormatBlocksAsHtmlAsync(
                TestSlackListItemWithIndentationBlocks(),
                organization,
                HtmlFormatting.Indented);

            Assert.Equal("""
<div style="padding-left: 0px;">
    <ul style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">One</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Two</li>
    </ul>
</div>
<div style="padding-left: 32px;">
    <ol style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: square;">A</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: square;">B</li>
    </ol>
</div>
<div style="padding-left: 16px;">
    <ul style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: circle;">1</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: circle;">2</li>
    </ul>
</div>
<div style="padding-left: 48px;">
    <ul style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Uno</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Dos</li>
    </ul>
</div>
<div style="padding-left: 64px;">
    <ul style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: circle;">Item One</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: circle;">Item Two</li>
    </ul>
</div>

""",
                result);
        }

        [Fact]
        public async Task CorrectlyFormatsListItemsWithBorder()
        {
            var organization = new Organization
            {
                PlatformType = PlatformType.Slack,
                Domain = "org.slack.com",
            };
            var slackResolver = Substitute.For<ISlackResolver>();
            var formatter = new BlockKitToHtmlFormatter(slackResolver);

            var result = await formatter.FormatBlocksAsHtmlAsync(
                TestSlackListItemWithBorder(),
                organization,
                HtmlFormatting.Indented);

            Assert.Equal("""
<div style="padding-left: 0px; border-left: solid 4px #999;">
    <ul style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">One</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: disc;">Two</li>
    </ul>
</div>
<div style="padding-left: 32px; border-left: solid 4px #999;">
    <ol style="margin: 0 0 0 24px;">
        <li style="margin: 0; padding: 4px 0; list-style-type: square;">A</li>
        <li style="margin: 0; padding: 4px 0; list-style-type: square;">B</li>
    </ol>
</div>

""",
                result);
        }

        [Fact]
        public async Task CorrectlyFormatsSlackMentions()
        {
            var organization = new Organization
            {
                PlatformType = PlatformType.Slack,
                Domain = "org.slack.com",
            };
            var slackResolver = Substitute.For<ISlackResolver>();
            var formatter = new BlockKitToHtmlFormatter(slackResolver);

            var result = await formatter.FormatBlocksAsHtmlAsync(
                TestSlackMentions(organization, slackResolver),
                organization);

            Assert.Equal(
                @"<a href=""https://org.slack.com/team/UB40"">(unknown user)</a>" +
                @"<a href=""https://org.slack.com/team/UB40"" style=""font-weight: bold"">(unknown user)</a>" +
                @"<a href=""https://org.slack.com/team/U03DYLAKR6U"">@Submarine</a>" +
                @"<a href=""https://org.slack.com/archives/R5D4"">(unknown channel)</a>" +
                @"<a href=""https://org.slack.com/archives/R5D4"" style=""font-weight: bold; font-style: italic"">(unknown channel)</a>"
                +
                @"<a href=""https://org.slack.com/archives/C3PO"">#Cantina</a>" +
                @"<a href=""https://org.slack.com/threads/user_groups/S3R10U5"">(unknown group)</a>" +
                @"<a href=""https://org.slack.com/threads/user_groups/S3R10U5"" style=""font-style: italic"">(unknown group)</a>"
                +
                // Resolve names again resolver
                @"<a href=""https://org.slack.com/team/U03DYLAKR6U"">@Submarine</a>" +
                @"<a href=""https://org.slack.com/archives/C3PO"">#Cantina</a>" +
                "", // Intentional; less churn adding new test cases
                result);
        }
    }

}
