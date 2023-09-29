using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Messages;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.TestHelpers;
using Xunit;

public class TicketControllerTests
{
    public class ThePostMethod : ControllerTestBase<TicketController>
    {
        [Fact]
        public async Task ReturnsNotFoundForWrongSkill()
        {
            var skill = await Env.CreateSkillAsync("test");
            var room = await Env.CreateRoomAsync();
            var channel = room.PlatformRoomId;
            AuthenticateAs(Env.TestData.Member, new(404));
            var conversationIdentifier = new ConversationIdentifier(channel, "1234567.8000");
            var request = new TicketPromptRequest(
                User: "U01234",
                MessageId: "1234567.8000",
                conversationIdentifier);

            var (_, result) = await InvokeControllerAsync(c => c.PostAsync(request));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsNotFoundIfSkillOrganizationDoesNotMatchConversationOrganization()
        {
            var skill = await Env.CreateSkillAsync("test");
            var room = await Env.CreateRoomAsync();
            var foreignRoom = await Env.CreateRoomAsync(org: Env.TestData.ForeignOrganization);
            var conversation = await Env.CreateConversationAsync(foreignRoom, "Test");
            var channel = room.PlatformRoomId;
            AuthenticateAs(Env.TestData.Member, skill);
            var conversationIdentifier = new ConversationIdentifier(channel, "1234567.8000", conversation.Id);
            var request = new TicketPromptRequest(
                User: "U01234",
                MessageId: "1234567.8000",
                conversationIdentifier);

            var (_, result) = await InvokeControllerAsync(c => c.PostAsync(request));

            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData(true, "{CONVOID}")]
        [InlineData(false, "{CHANNEL}:{MESSAGEID}")]
        public async Task SendsTicketPromptForExistingConversation(bool conversationExists, string expectedButtonValue)
        {
            var skill = await Env.CreateSkillAsync("test");
            var room = await Env.CreateRoomAsync();
            var conversation = conversationExists ? await Env.CreateConversationAsync(room, "Test") : null;
            var channel = room.PlatformRoomId;
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            AuthenticateAs(Env.TestData.Member, skill);
            var conversationIdentifier = new ConversationIdentifier(channel, "1234567.8000", conversation?.Id);
            var request = new TicketPromptRequest(
                User: "U01234",
                MessageId: "1234567.8000",
                conversationIdentifier);

            var (_, result) = await InvokeControllerAsync(c => c.PostAsync(request));

            var apiResult = Assert.IsType<ApiResult>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.True(apiResult.Ok);
            var posted = Assert.Single(Env.SlackApi.PostedMessages);
            var ephemeralMessage = Assert.IsType<EphemeralMessageRequest>(posted);
            Assert.Equal("U01234", ephemeralMessage.User);
            Assert.Equal(2, ephemeralMessage.Blocks?.Count);
            var actions = Assert.IsType<Actions>(ephemeralMessage.Blocks?.Last());
            var buttons = actions.Elements.Cast<ButtonElement>().ToList();
            Assert.Equal(2, buttons.Count);
            var ticketButton = buttons[0];
            var dismissButton = buttons[1];
            expectedButtonValue = expectedButtonValue
                .Replace("{CONVOID}", conversation?.Id.ToString())
                .Replace("{CHANNEL}", channel)
                .Replace("{MESSAGEID}", "1234567.8000");

            Assert.Equal("Create Zendesk Ticket", ticketButton.Text.Text);
            Assert.Equal(expectedButtonValue, ticketButton.Value);
            Assert.Equal($"i:{nameof(CreateZendeskTicketFormModal)}:{integration.Id}", ticketButton.ActionId);
            Assert.Equal("Dismiss", dismissButton.Text.Text);
            Assert.Equal($"i:{nameof(DismissHandler)}", dismissButton.ActionId);
        }
    }
}
