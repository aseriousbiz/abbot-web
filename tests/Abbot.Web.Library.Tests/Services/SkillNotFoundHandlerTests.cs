using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;
using Serious.TestHelpers;
using Xunit;

public class SkillNotFoundHandlerTests
{
    public class TheHandleSkillNotFoundAsyncMethod
    {
        [Fact]
        public async Task RepliesWithDefaultResponderAndLogsIt()
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext("skil", commandText: "skil");
            messageContext.Organization.FallbackResponderEnabled = true;
            var handler = env.Activate<SkillNotFoundHandler>();

            await handler.HandleSkillNotFoundAsync(messageContext);

            var reply = messageContext.SentMessages.First();
            Assert.Equal(@"You want the answer to: skil", reply);
            var auditEvent = await env.Db.AuditEvents.OfType<SkillNotFoundEvent>().LastAsync();
            Assert.Equal("Told Abbot `skil` which did not match a skill. Abbot replied with a default response.", auditEvent.Description);
            Assert.Equal(reply, auditEvent.Response);
            Assert.Equal(ResponseSource.AutoResponder, auditEvent.ResponseSource);
            Assert.Equal("skil", auditEvent.Command);
        }

        [Fact]
        public async Task DoesNotUseDefaultResponderWhenNotEnabled()
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext("skil", commandText: "skil");
            messageContext.Organization.FallbackResponderEnabled = false;
            var handler = env.Activate<SkillNotFoundHandler>();

            await handler.HandleSkillNotFoundAsync(messageContext);

            var reply = messageContext.SentMessages.First();
            Assert.Equal($@"Sorry, I did not understand that. `{messageContext.Bot} help` to learn what I can do.", reply);
        }

        [Fact]
        public async Task RepliesToEmptyCommand()
        {
            var env = TestEnvironment.Create();
            var messageContext = env.CreateFakeMessageContext(string.Empty);
            var handler = env.Activate<SkillNotFoundHandler>();

            await handler.HandleSkillNotFoundAsync(messageContext);

            var reply = messageContext.SentMessages.First();
            Assert.Equal($@"Sorry, I did not understand that. `{messageContext.Bot} help` to learn what I can do.", reply);
        }
    }
}
