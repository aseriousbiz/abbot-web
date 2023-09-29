using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.TestHelpers;

public class TriggerControllerTests
{
    public class TheOnRequestAsyncMethod
    {
        [Fact]
        public async Task WithNonExistentTriggerReturnsNotFound()
        {
            var env = TestEnvironment.Create();
            var controller = env.Activate<TriggerController>();

            var result = await controller.OnRequestAsync("skillname", "apitoken");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task WithInvalidTokenReturnsNotFound()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("pug");
            skill.Triggers.Add(new SkillHttpTrigger
            {
                Name = "the-room",
                ApiToken = "SECRET",
                Creator = skill.Creator,
                RoomId = "C001",
            });
            await env.Db.SaveChangesAsync();
            var controller = env.Activate<TriggerController>();

            var result = await controller.OnRequestAsync("pug", "WRONGTOKEN");

            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData("application/json")]
        [InlineData("application/xml")]
        public async Task WithValidTokenCallsSkillAndReturnsContentTypeSpecifiedBySkill(string contentType)
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("pug");
            var trigger = new SkillHttpTrigger
            {
                Name = "the-room",
                ApiToken = "SECRET",
                Creator = skill.Creator,
                RoomId = "C001",
            };
            skill.Triggers.Add(trigger);
            await env.Db.SaveChangesAsync();
            env.SkillRunnerClient.PushResponse
            (
                new()
                {
                    Success = true,
                    Replies = new List<string>(),
                    Errors = new List<RuntimeError>(),
                    ContentType = contentType,
                    Content = "{}"
                }
            );
            var httpContext = new FakeHttpContext();
            var bodyStream = new MemoryStream();
            await using var writer = new StreamWriter(bodyStream);
            await writer.WriteAsync("Body Rocking");
            await writer.FlushAsync();
            bodyStream.Position = 0;
            httpContext.Request.Body = bodyStream;
            var controller = env.Activate<TriggerController>();
            controller.ControllerContext = new FakeControllerContext(httpContext);

            var result = await controller.OnRequestAsync("pug", "SECRET");

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal(200, contentResult.StatusCode);
            Assert.Equal(contentType, contentResult.ContentType);
            var invocation = env.SkillRunnerClient.Invocations.Single();
            Assert.Same(trigger, invocation.SkillTrigger);
            Assert.NotNull(invocation.HttpTriggerEvent);
            Assert.Equal("Body Rocking", invocation.HttpTriggerEvent.RawBody);
        }

        [Theory]
        [InlineData("application/json", "*/*", "application/json")]
        [InlineData("application/json;charset=utf8", "text/json,application/xml,application/json", "application/json")]
        [InlineData("application/xml", "*/*", "application/xml")]
        [InlineData("text/json", "*/*", "text/json")]
        [InlineData("text/xml", "*/*", "text/xml")]
        [InlineData("text/xml", "application/xml", "application/xml")]
        [InlineData("text/xml", "application/*", "application/xml")]
        [InlineData("text/json", "application/*", "application/json")]
        [InlineData("application/vnd.github+json", "text/json", "application/json")]
        public async Task WithValidTokenCallsSkillAndReturnsNegotiatedContentType(
            string contentType,
            string accepts,
            string expectedContentType)
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("pug");
            var trigger = new SkillHttpTrigger
            {
                Name = "the-room",
                ApiToken = "SECRET",
                Creator = skill.Creator,
                RoomId = "C001",
            };
            skill.Triggers.Add(trigger);
            await env.Db.SaveChangesAsync();
            env.SkillRunnerClient.PushResponse
            (
                new()
                {
                    Success = true,
                    Replies = new List<string>(),
                    Errors = new List<RuntimeError>(),
                    ContentType = null,
                    Content = "{}"
                }
            );
            var httpContext = new FakeHttpContext();
            httpContext.Request.Headers["Content-Type"] = contentType;
            httpContext.Request.Headers["Accept"] = accepts;
            var bodyStream = new MemoryStream();
            await using var writer = new StreamWriter(bodyStream);
            await writer.WriteAsync("Body Rocking");
            await writer.FlushAsync();
            bodyStream.Position = 0;
            httpContext.Request.Body = bodyStream;
            var controller = env.Activate<TriggerController>();
            controller.ControllerContext = new FakeControllerContext(httpContext);

            var result = await controller.OnRequestAsync("pug", "SECRET");

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal(200, contentResult.StatusCode);
            Assert.Equal(expectedContentType, contentResult.ContentType);
            var invocation = env.SkillRunnerClient.Invocations.Single();
            Assert.Same(trigger, invocation.SkillTrigger);
            Assert.NotNull(invocation.HttpTriggerEvent);
            Assert.Equal("Body Rocking", invocation.HttpTriggerEvent.RawBody);
        }

        [Theory]
        [InlineData(true, null, 204)]
        [InlineData(true, "", 204)]
        [InlineData(true, "{}", 200)]
        [InlineData(true, "<xml><stuff /></xml>", 200)]
        public async Task ReturnsContentSpecifiedBySkillAndAutomaticStatusCode(
            bool success,
            string content,
            int expectedStatusCode)
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("pug");
            var trigger = new SkillHttpTrigger
            {
                Name = "the-room",
                ApiToken = "SECRET",
                Creator = skill.Creator,
                RoomId = "C001",
            };
            skill.Triggers.Add(trigger);
            skill.Enabled = true;
            await env.Db.SaveChangesAsync();
            env.SkillRunnerClient.PushResponse
            (
                new()
                {
                    Success = success,
                    Replies = new List<string>(),
                    Errors = new List<RuntimeError>(),
                    ContentType = null,
                    Content = content
                }
            );
            var httpContext = new FakeHttpContext();
            var bodyStream = new MemoryStream();
            using var writer = new StreamWriter(bodyStream);
            await writer.WriteAsync("Body Rocking");
            await writer.FlushAsync();
            bodyStream.Position = 0;
            httpContext.Request.Body = bodyStream;
            var controller = env.Activate<TriggerController>();
            controller.ControllerContext = new FakeControllerContext(httpContext);


            var result = await controller.OnRequestAsync("pug", "SECRET");

            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal(expectedStatusCode, contentResult.StatusCode);
            Assert.Equal(content, contentResult.Content);
            var invocation = env.SkillRunnerClient.Invocations.Single();
            Assert.Same(trigger, invocation.SkillTrigger);
            Assert.NotNull(invocation.HttpTriggerEvent);
            Assert.Equal("Body Rocking", invocation.HttpTriggerEvent.RawBody);
        }

        [Fact]
        public async Task ReturnsProperStatusCodeForDeleteSkills()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("pug", enabled: true);
            var trigger = new SkillHttpTrigger
            {
                Name = "the-room",
                ApiToken = "SECRET",
                Creator = skill.Creator,
                RoomId = "C001",
            };
            skill.Triggers.Add(trigger);
            skill.IsDeleted = true;
            await env.Db.SaveChangesAsync();
            var httpContext = new FakeHttpContext();
            var controller = env.Activate<TriggerController>();
            controller.ControllerContext = new FakeControllerContext(httpContext);

            var result = await controller.OnRequestAsync("pug", "SECRET");

            var statusResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(410, statusResult.StatusCode);
        }
    }
}
