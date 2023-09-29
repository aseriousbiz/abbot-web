using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Clients;
using Xunit;

public class InvitationSenderTests
{
    public class TheSendInvitationAsyncMethod
    {
        [Fact]
        public async Task SendsInvitationToRecipientsAndUnarchivesUser()
        {
            var env = TestEnvironment.Create();
            await env.CreateMemberAsync(platformUserId: "U0000001");
            var archived = await env.CreateMemberAsync(platformUserId: "U0000002");
            archived.User.NameIdentifier = "slack|oauth|U0000002";
            archived.Active = false;
            await env.Db.SaveChangesAsync();
            await env.CreateMemberAsync("U0000003");
            var now = env.Clock.Freeze();
            var organization = env.TestData.Organization;
            var sender = env.Activate<InvitationSender>();

            await sender.SendInvitationAsync(
                recipientUserIds: new[] { "U0000001", "U0000002" },
                fromUserId: "U0000003",
                organization.Id);

            var firstRecipient = await env.Users.GetUserByPlatformUserId("U0000001");
            Assert.NotNull(firstRecipient);
            var firstMember = Assert.Single(firstRecipient.Members);
            Assert.Equal(now, firstMember.InvitationDate);
            var secondRecipient = await env.Users.GetUserByPlatformUserId("U0000002");
            Assert.NotNull(secondRecipient);
            var secondMember = Assert.Single(secondRecipient.Members);
            Assert.Equal(now, secondMember.InvitationDate);
            var postedMessages = env.SlackApi.PostedMessages;
            var expectedMessage =
                "<@U0000003> invites you to manage conversations with Abbot. Visit https://ab.bot/ to log in and accept.";
            Assert.Collection(postedMessages,
                msg => Assert.Equal(("U0000001", expectedMessage), (msg.Channel, msg.Text)),
                msg => Assert.Equal(("U0000002", expectedMessage), (msg.Channel, msg.Text)));
            Assert.True(archived.Active);
        }
    }
}
