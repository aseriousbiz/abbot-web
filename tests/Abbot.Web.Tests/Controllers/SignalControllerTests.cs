using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serious.Abbot.Controllers;
using Serious.Abbot.Messages;

public class SignalControllerTests
{
    public class ThePostMethod : ControllerTestBase<SignalController>
    {
        [Fact]
        public async Task ReturnsOkWhenSignalHandlingEnqueuedCorrectly()
        {
            Env.SignalHandler.CycleDetected = false;
            var skill = await Env.CreateSkillAsync("test");
            AuthenticateAs(Env.TestData.Member, skill);

            await InvokeControllerAsync<OkObjectResult>(c => c.PostAsync(new SignalRequest
            {
                Name = "some-signal",
                Room = new PlatformRoom("C123", "")
            }));
        }

        [Fact]
        public async Task ReturnsFailedResultWhenSignalResultsInCycle()
        {
            Env.SignalHandler.CycleDetected = true;
            var skill = await Env.CreateSkillAsync("test");
            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync<BadRequestObjectResult>(c => c.PostAsync(new SignalRequest
            {
                Name = "high-five",
                Room = new PlatformRoom("C123", "")
            }));

            var json = JsonConvert.SerializeObject(result.Value);
            Assert.Equal("{\"Ok\":false,\"Error\":\"Signal `high-five` would result in a signal cycle.\"}", json);
        }

        [Fact]
        public async Task ReturnsFailedResultWhenSignalNameIsEmpty()
        {
            Env.SignalHandler.CycleDetected = false;
            var skill = await Env.CreateSkillAsync("test");
            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync<BadRequestObjectResult>(c => c.PostAsync(new SignalRequest
            {
                Name = "",
                Room = new PlatformRoom("C123", "")
            }));

            var json = JsonConvert.SerializeObject(result.Value);
            Assert.Equal($"{{\"Ok\":false,\"Error\":\"Empty signal name is not allowed.\"}}", json);
        }

        [Fact]
        public async Task ReturnsFailedResultWhenSignalNameIsReserved()
        {
            Env.SignalHandler.CycleDetected = false;
            var skill = await Env.CreateSkillAsync("test");
            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync<BadRequestObjectResult>(c => c.PostAsync(new SignalRequest
            {
                Name = "system:foo",
                Room = new PlatformRoom("C123", "")
            }));

            var json = JsonConvert.SerializeObject(result.Value);
            Assert.Equal($"{{\"Ok\":false,\"Error\":\"Signal name `system:foo` is reserved and cannot be raised by a user-defined skill.\"}}", json);
        }
    }
}
