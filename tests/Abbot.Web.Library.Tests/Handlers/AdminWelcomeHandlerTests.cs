using System;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Hangfire.States;
using Serious.Abbot.Events;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Security;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Xunit;

public class AdminWelcomeHandlerTests
{
    public class TheOnMessageInteractionAsyncMethod
    {
        [Fact]
        public async Task EnqueuesReminderInADayAndResponds()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await env.Roles.AddUserToRoleAsync(
                env.TestData.Member,
                Roles.Administrator,
                env.TestData.Abbot);
            var interactionInfo = new MessageInteractionInfo(
                new MessageBlockActionsPayload
                {
                    Message = new SlackMessage
                    {
                        Text = "The original message",
                        Blocks = new ILayoutBlock[]
                        {
                            new Section("Some Blocks")
                        }
                    },
                    Container = new MessageContainer("message-timestamp", false, "C01234"),
                    ResponseUrl = new Uri("https://example.com/respond")
                },
                "remind",
                new InteractionCallbackInfo(nameof(AdminWelcomeHandler)));
            var message = env.CreatePlatformMessage(
                room,
                interactionInfo: interactionInfo);
            var handler = env.Activate<AdminWelcomeHandler>();

            await handler.OnMessageInteractionAsync(message);

            var jobs = env.BackgroundJobClient.EnqueuedJobs;
            var (job, state) = Assert.Single(jobs);
            Assert.Equal(nameof(AdminWelcomeHandler.SendAdminReminderMessageAsync), job.Method.Name);
            var scheduledState = Assert.IsType<ScheduledState>(state);
            var scheduledDelay = scheduledState.EnqueueAt - scheduledState.ScheduledAt;
            Assert.True(scheduledDelay > TimeSpan.FromDays(1).Add(TimeSpan.FromSeconds(-1)));
            Assert.True(scheduledDelay < TimeSpan.FromDays(1).Add(TimeSpan.FromSeconds(1)));
            var updatedReply = Assert.IsType<RichActivity>(env.Responder.FirstActivityReply());
            // When clicking "Remind me later", we update the existing message and remove the buttons.
            Assert.Equal("The original message", updatedReply.Text);
            Assert.Empty(updatedReply.Blocks);
            Assert.Equal(new Uri("https://example.com/respond"), updatedReply.ResponseUrl);
            var newReply = env.Responder.LastActivityReply();
            Assert.Equal("Ok! I’ll remind you tomorrow about customizing me.", newReply.Text);
        }
    }

    public class TheSendAdminReminderMessageAsyncMethod
    {
        [Fact]
        public async Task SendsReminderMessage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var admin = await env.CreateAdminMemberAsync();
            var handler = env.Activate<AdminWelcomeHandler>();

            await handler.SendAdminReminderMessageAsync(
                organization.Id,
                admin.User.PlatformUserId);

            var posted = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal("You asked me to remind you to add additional Administrators and to customize me to fit in with your organization (such as configuring auto-replies).", posted.Text);
            Assert.NotNull(posted.Blocks);
            var section = Assert.IsType<Section>(posted.Blocks.First());
            Assert.NotNull(section.Text);
            Assert.Equal(posted.Text, section.Text.Text);
            var actions = Assert.IsType<Actions>(posted.Blocks.FindBlockById("i:AdminWelcomeHandler"));
            Assert.Collection(actions.Elements,
                b => AssertButton(b, ":key: Manage Administrators", "admins"),
                b => AssertButton(b, ":envelope: Invite Users", "invite", new Uri("https://app.ab.bot/settings/organization/users/invite")),
                b => AssertButton(b,
                    ":gear: Customize Abbot",
                    "customize",
                    new Uri("https://app.ab.bot/settings/organization")));
        }
    }

    static void AssertButton(IActionElement element, string text, string value, Uri? url = null)
    {
        var button = Assert.IsType<ButtonElement>(element);
        Assert.Equal(text, button.Text.Text);
        Assert.Equal(value, button.Value);
        Assert.Equal(url, button.Url);
    }
}
