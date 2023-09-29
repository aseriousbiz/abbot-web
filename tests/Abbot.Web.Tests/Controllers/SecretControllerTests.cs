using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers;
using Serious.Abbot.Messages;
using Serious.TestHelpers;
using Xunit;

public class SecretControllerTests
{
    public class TheGetAsyncMethod : ControllerTestBase<SecretController>
    {
        [Fact]
        public async Task ReturnsNotFoundIfDataNotFound()
        {
            AuthenticateAs(Env.TestData.Member, new(404));
            var (_, result) = await InvokeControllerAsync(c => c.GetAsync("key"));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsSecret()
        {
            var user = Env.TestData.User;
            var skill = await Env.CreateSkillAsync("test", codeText: "await Bot.ReplyAsync(\"\");");
            await Env.SkillSecrets.CreateAsync(
                "secret-key",
                "secret-value",
                "description",
                skill,
                user);

            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAsync("secret-key"));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillSecretResponse>(objectResult.Value);
            Assert.NotNull(response);
            Assert.Equal("secret-value", response.Secret);
        }
    }
}
