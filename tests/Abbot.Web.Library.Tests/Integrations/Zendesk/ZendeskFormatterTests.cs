using System;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Messaging;
using Serious.Abbot.Scripting;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.Payloads;
using Serious.TestHelpers;
using Xunit;

public class ZendeskFormatterTests
{
    public class TheCreateCommentInstanceAsyncMethod
    {
        [Fact]
        public async Task FormatsBlocksForRegularMessage()
        {
            var env = TestEnvironment.Create();
            env.TestData.User.PlatformUserId = "U011223344";
            env.TestData.User.DisplayName = "Mr. T";
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync(platformRoomId: "C08675309");
            var conversation = await env.CreateConversationAsync(room, firstMessageId: "1483051909.018632");
            var blocks = new ILayoutBlock[]
            {
                new RichTextBlock
                {
                    Elements = new[]
                    {
                        new RichTextPreformatted
                        {
                            Elements = new[]
                            {
                                new TextElement { Text = "Hello, world." }
                            }
                        }
                    }
                }
            };
            var message = new ConversationMessage(
                Text: "Hello, world",
                env.TestData.Organization,
                env.TestData.Member,
                room,
                env.Clock.UtcNow,
                MessageId: "1483125339.020269",
                ThreadId: conversation.FirstMessageId,
                blocks,
                Files: Array.Empty<FileUpload>(),
                MessageContext: null);
            var formatter = env.Activate<ZendeskFormatter>();

            var instance = await formatter.CreateCommentAsync(conversation, message, 123456789);

            Assert.Equal($"""

<strong><a href="https://testorg.example.com/archives/C08675309/p1483125339020269?thread_ts=1483051909.018632">New reply</a> from <a href="https://testorg.example.com/team/U011223344">Mr. T</a> in Slack:</strong><br />
<pre>Hello, world.</pre>
""", instance.HtmlBody);
        }

        [Fact]
        public async Task FormatsFileAttachmentsAsLinks()
        {
            var env = TestEnvironment.Create();
            var filesClient = env.Get<IFilesApiClient>();
            env.TestData.User.PlatformUserId = "U011223344";
            env.TestData.User.DisplayName = "Mr. T";
            await env.Db.SaveChangesAsync();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken.Require().Reveal();
            var room = await env.CreateRoomAsync(platformRoomId: "C08675309");
            filesClient.GetFileInfoAsync(apiToken, "F04DCMS81FB")
                .Returns(new FileResponse
                {
                    Ok = true,
                    Body = new UploadedFile
                    {
                        Id = "F04DCMS81FB",
                        Created = 1669759514,
                        Name = "abbot-logo.png",
                        Title = "abbot-logo.png",
                        MimeType = "image/png",
                        FileType = "png",
                        PrettyType = "PNG",
                        User = "U011223344",
                        UserTeam = organization.PlatformId,
                        Size = 1234,
                        Permalink = "https://aseriousbiz.slack.com/files/U012LKJFG0P/F04DCMS81FB/abbot-logo.png",
                        PermalinkPublic = "https://slack-files.com/T013108BYLS-F04DCMS81FB-728077b36d",
                        Thumb64 = "https://files.slack.com/files-tmb/T013108BYLS-F04DCMS81FB-ea5603b571/abbot-logo_64.png",
                    }
                });
            filesClient.GetFileInfoAsync(apiToken, "F04CTEUMVFY")
                .Returns(new FileResponse
                {
                    Ok = true,
                    Body = new UploadedFile
                    {
                        Id = "F04CTEUMVFY",
                        Created = 1669759514,
                        Name = "important-info.csv",
                        Title = "important-info.csv",
                        MimeType = "text/csv",
                        FileType = "csv",
                        PrettyType = "CSV",
                        User = "U011223344",
                        UserTeam = organization.PlatformId,
                        Size = 3134,
                        Permalink = "https://aseriousbiz.slack.com/files/U012LKJFG0P/F04CTEUMVFY/important-info.csv",
                        PermalinkPublic = "https://slack-files.com/T013108BYLS-F04CTEUMVFY-8e805a0710",
                    }
                });
            filesClient.GetFileInfoAsync(apiToken, "F00NOTFOUND")
                .Returns(new FileResponse
                {
                    Ok = false,
                    Error = "file_not_found"
                });
            var conversation = await env.CreateConversationAsync(room, firstMessageId: "1483051909.018632");

            var message = new ConversationMessage(
                Text: "Hello, world.",
                env.TestData.Organization,
                env.TestData.Member,
                room,
                env.Clock.UtcNow,
                MessageId: "1483125339.020269",
                ThreadId: conversation.FirstMessageId,
                Blocks: Array.Empty<ILayoutBlock>(),
                Files: new[]
                {
                    new FileUpload("F04DCMS81FB", "check_file_info", 0, 0, ""),
                    new FileUpload("F04CTEUMVFY", "check_file_info", 0, 0, ""),
                    new FileUpload("F00NOTFOUND", "check_file_info", 0, 0, ""),
                },
                MessageContext: null);
            var formatter = env.Activate<ZendeskFormatter>();

            var instance = await formatter.CreateCommentAsync(conversation, message, 123456789);

            Assert.Equal($"""

<strong><a href="https://testorg.example.com/archives/C08675309/p1483125339020269?thread_ts=1483051909.018632">New reply</a> from <a href="https://testorg.example.com/team/U011223344">Mr. T</a> in Slack:</strong><br />
Hello, world.
<hr />
<strong>Message Attachments:</strong>
<ol>
    <li><a href="https://aseriousbiz.slack.com/files/U012LKJFG0P/F04DCMS81FB/abbot-logo.png">abbot-logo.png</a></li>
    <li><a href="https://aseriousbiz.slack.com/files/U012LKJFG0P/F04CTEUMVFY/important-info.csv">important-info.csv</a></li>
</ol>

""", instance.HtmlBody);
        }
    }

    public class TheCreateTicketMethod
    {
        [Fact]
        public async Task BindsTagsField()
        {
            var ticket = await RunTicketFieldBindingTestAsync(
                ("tags", "tag1, tag2"));
            Assert.Equal(new[] { "tag1", "tag2" }, ticket.Tags);
        }

        [Fact]
        public async Task BindsTypeField()
        {
            var ticket = await RunTicketFieldBindingTestAsync(
                ("type", "plorp"));

            Assert.Equal("plorp", ticket.Type);
        }

        [Fact]
        public async Task BindsPriorityField()
        {
            var ticket = await RunTicketFieldBindingTestAsync(
                ("priority", "urgent"));

            Assert.Equal("urgent", ticket.Priority);
        }

        [Fact]
        public async Task BindsCustomFields()
        {
            var ticket = await RunTicketFieldBindingTestAsync(
                ("custom_field:123", "custom_value_1"),
                ("custom_field:456", "custom_value_2"));

            Assert.Equal(new CustomFieldValue[]
            {
                new(123, "custom_value_1"),
                new(456, "custom_value_2")
            }, ticket.CustomFields.ToArray());
        }

        static async Task<ZendeskTicket> RunTicketFieldBindingTestAsync(params (string, object?)[] fields)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var formatter = env.Activate<ZendeskFormatter>();

            var fieldsDict = fields.ToDictionary(x => x.Item1, x => x.Item2);
            var ticket = formatter.CreateTicket(
                convo,
                1234,
                "Subject",
                fieldsDict,
                env.TestData.Member,
                5678);

            return ticket;
        }
    }
}
