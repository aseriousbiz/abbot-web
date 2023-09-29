using System.Diagnostics;
using Abbot.Common.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Serialization;
using Serious.TestHelpers;

public class SkillRunnerClientTests
{
    public class TheSendAsyncMethod
    {
        [Theory]
        [InlineData(CodeLanguage.CSharp, "// Ignored", "somecachekey")]
        [InlineData(CodeLanguage.Python, "# Ignored", "# Ignored")]
        [InlineData(CodeLanguage.JavaScript, "// Ignored", "// Ignored")]
        public async Task CallsTheAbbotFunctionsEndpointWithCorrectArgumentsForSlack(
            CodeLanguage codeLanguage,
            string code,
            string expectedCode)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var user = member.User;
            var url = codeLanguage switch
            {
                CodeLanguage.Python => env.SkillOptions.PythonEndpoint,
                CodeLanguage.JavaScript => env.SkillOptions.JavaScriptEndpoint,
                _ => env.SkillOptions.DotNetEndpoint
            };
            var requestHandler = env.Http.AddResponse(
                new Uri(url!),
                HttpMethod.Post,
                new SkillRunResponse
                {
                    Success = true,
                    Errors = new List<RuntimeError>(),
                    Content = null,
                    ContentType = null,
                    Replies = new List<string> { "https://taters/" }
                }, "application/vnd.abbot.v1+json");
            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = await env.CreateSkillAsync("pug", codeLanguage, codeText: code);
            skill.CacheKey = "somecachekey";
            var conversation = new Conversation
            {
                Id = 42,
                Title = "Convo Title",
                Room = new Room { PlatformRoomId = "C001", Name = "midgar" },
                StartedBy = member,
                Created = new DateTime(2021, 01, 01, 00, 00, 00),
                LastMessagePostedOn = new DateTime(2021, 01, 01, 00, 00, 00),
                FirstMessageId = "1234567890.1234546",
                ThreadIds = new List<string> { "1234567890.1234546" },
                Members = new List<ConversationMember>
                {
                    new() { Member = member }
                }
            };
            var caller = env.Activate<SkillRunnerClient>();

            var response = await caller.SendAsync(
                skill,
                new Arguments("argue ments"),
                "the command text",
                messageContext.Mentions,
                messageContext.FromMember,
                messageContext.Bot,
                messageContext.Room.ToPlatformRoom(),
                null,
                new Uri("https://app.ab.bot/skills/pug"),
                messageId: "1234567890.1234546",
                messageUrl: new Uri("https://example.com/some/slack/url"),
                triggeringMessageAuthor: messageContext.FromMember,
                conversation: conversation.ToChatConversation(new Uri("https://app.ab.bot/conversations/42")));

            Assert.NotNull(response.Replies);
            Assert.Equal("https://taters/", response.Replies.Single());
            var received = AbbotJsonFormat.Default.Deserialize<SkillMessage>(requestHandler.GetReceivedRequestBody());
            Assert.NotNull(received);
            Assert.Equal(skill.Name, received.SkillInfo.SkillName);
            Assert.Equal("argue ments", received.SkillInfo.Arguments);
            Assert.Equal(new[] { "argue", "ments" }, received.SkillInfo.TokenizedArguments.Select(a => a.Value).ToArray());
            Assert.Equal("the command text", received.SkillInfo.CommandText);
            Assert.Equal(expectedCode, received.RunnerInfo.Code);
            Assert.True(received.SkillInfo.IsChat);
            Assert.False(received.SkillInfo.IsRequest);
            Assert.Equal(organization.PlatformBotUserId, received.SkillInfo.Bot.Id);
            Assert.Equal("test-abbot", received.SkillInfo.Bot.UserName);
            Assert.Equal("test-abbot", received.SkillInfo.Bot.Name);
            Assert.Equal("1234567890.1234546", received.SkillInfo.Message?.MessageId);
            Assert.Equal(new Uri("https://example.com/some/slack/url"), received.SkillInfo.Message?.MessageUrl);
            Assert.NotNull(received.ConversationInfo);
            Assert.Equal("42", received.ConversationInfo.Id);
            Assert.Equal("Convo Title", received.ConversationInfo.Title);
            Assert.Equal(user.PlatformUserId, received.ConversationInfo.StartedBy.Id);
            Assert.Equal(user.DisplayName, received.ConversationInfo.StartedBy.UserName);
            Assert.Equal("C001", received.ConversationInfo.Room.Id);
            Assert.Equal("midgar", received.ConversationInfo.Room.Name);
            Assert.Equal("1234567890.1234546", received.ConversationInfo.FirstMessageId);
            Assert.NotNull(received.ConversationInfo);
            Assert.Collection(received.ConversationInfo.Members,
                m => {
                    Assert.Equal(user.PlatformUserId, m.Id);
                    Assert.Equal(user.DisplayName, m.UserName);
                });
        }

        [Fact]
        public async Task SetsChatToFalseWhenSendingSignal()
        {
            var env = TestEnvironment.Create();
            var requestHandler = env.Http.AddResponse(
                new Uri(env.SkillOptions.PythonEndpoint!),
                HttpMethod.Post,
                new SkillRunResponse
                {
                    Success = true,
                    Errors = new List<RuntimeError>(),
                    Content = null,
                    ContentType = null,
                    Replies = new List<string> { "https://taters/" }
                }, "application/vnd.abbot.v1+json");

            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = new Skill
            {
                Name = "pug",
                Code = "// Code",
                Language = CodeLanguage.Python,
                Organization = env.TestData.Organization,
                CacheKey = "somecachekey"
            };
            var caller = env.Activate<SkillRunnerClient>();

            var response = await caller.SendAsync(
                skill,
                new Arguments(string.Empty),
                string.Empty,
                messageContext.Mentions,
                messageContext.FromMember,
                messageContext.Bot,
                messageContext.Room.ToPlatformRoom(),
                null,
                new Uri("https://app.ab.bot/skills/pug"),
                signal: new SignalMessage { Source = new SignalSourceMessage() });

            Assert.NotNull(response.Replies);
            Assert.Equal("https://taters/", response.Replies.Single());
            var received = AbbotJsonFormat.Default.Deserialize<SkillMessage>(requestHandler.GetReceivedRequestBody());
            Assert.NotNull(received);
            Assert.Equal("// Code", received.RunnerInfo.Code);
            Assert.False(received.SkillInfo.IsChat);
            Assert.True(received.SkillInfo.IsSignal);
            Assert.False(received.SkillInfo.IsRequest);
        }

        [Theory]
        [InlineData(CodeLanguage.CSharp, "https://config.example|abcd", null, null, "https://config.example", "abcd")]
        [InlineData(CodeLanguage.CSharp, "https://config.example|abcd", "https://global.example|efgh", null, "https://global.example", "efgh")]
        [InlineData(CodeLanguage.CSharp, "https://config.example|abcd", "https://global.example|efgh", "https://org.example|ijkl", "https://org.example", "ijkl")]
        [InlineData(CodeLanguage.Python, "https://config.example|abcd", null, null, "https://config.example", "abcd")]
        [InlineData(CodeLanguage.Python, "https://config.example|abcd", "https://global.example|efgh", null, "https://global.example", "efgh")]
        [InlineData(CodeLanguage.Python, "https://config.example|abcd", "https://global.example|efgh", "https://org.example|ijkl", "https://org.example", "ijkl")]
        [InlineData(CodeLanguage.JavaScript, "https://config.example|abcd", null, null, "https://config.example", "abcd")]
        [InlineData(CodeLanguage.JavaScript, "https://config.example|abcd", "https://global.example|efgh", null, "https://global.example", "efgh")]
        [InlineData(CodeLanguage.JavaScript, "https://config.example|abcd", "https://global.example|efgh", "https://org.example|ijkl", "https://org.example", "ijkl")]
        [InlineData(CodeLanguage.Ink, "https://config.example|abcd", null, null, "https://config.example", "abcd")]
        [InlineData(CodeLanguage.Ink, "https://config.example|abcd", "https://global.example|efgh", null, "https://global.example", "efgh")]
        [InlineData(CodeLanguage.Ink, "https://config.example|abcd", "https://global.example|efgh", "https://org.example|ijkl", "https://org.example", "ijkl")]
        public async Task CallsTheOverrideEndpointWithCorrectArguments(
            CodeLanguage codeLanguage,
            string configEndpoint,
            string? globalOverrideEndpoint,
            string? orgOverrideEndpoint,
            string expectedEndpoint,
            string? expectedApiToken)
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ConfigureServices(s => {
                    var splat = configEndpoint.Split('|');
                    s.Configure<SkillOptions>(o => {
                        o.DotNetEndpoint = codeLanguage == CodeLanguage.CSharp ? splat[0] : null;
                        o.DotNetEndpointCode = codeLanguage == CodeLanguage.CSharp ? splat[1] : null;
                        o.PythonEndpoint = codeLanguage == CodeLanguage.Python ? splat[0] : null;
                        o.PythonEndpointCode = codeLanguage == CodeLanguage.Python ? splat[1] : null;
                        o.JavaScriptEndpoint = codeLanguage == CodeLanguage.JavaScript ? splat[0] : null;
                        o.JavaScriptEndpointCode = codeLanguage == CodeLanguage.JavaScript ? splat[1] : null;
                        o.InkEndpoint = codeLanguage == CodeLanguage.Ink ? splat[0] : null;
                        o.InkEndpointCode = codeLanguage == CodeLanguage.Ink ? splat[1] : null;
                    });
                })
                .Build();
            var organization = env.TestData.Organization;

            if (orgOverrideEndpoint?.Split("|") is [{ } orgUrl, var orgToken])
            {
                organization.Settings.SkillEndpoints[codeLanguage] = new(new(orgUrl), orgToken);
            }

            await env.Db.SaveChangesAsync();

            if (globalOverrideEndpoint?.Split("|") is [{ } globalUrl, var globalToken])
            {
                await env.Get<IRunnerEndpointManager>()
                    .SetGlobalOverrideAsync(codeLanguage, new(new(globalUrl), globalToken), env.TestData.Member);
            }

            var url = new Uri(expectedEndpoint);
            var requestHandler = env.Http.AddResponse(
                url,
                HttpMethod.Post,
                new SkillRunResponse
                {
                    Success = true,
                    Errors = new List<RuntimeError>(),
                    Content = null,
                    ContentType = null,
                    Replies = new List<string> { "https://taters/" }
                }, "application/vnd.abbot.v1+json");
            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = new Skill
            {
                Name = "pug",
                Code = "// Code",
                Language = codeLanguage,
                Organization = organization,
                CacheKey = "somecachekey"
            };
            var caller = env.Activate<SkillRunnerClient>();

            await caller.SendAsync(
                skill,
                new Arguments(string.Empty),
                string.Empty,
                messageContext.Mentions,
                messageContext.FromMember,
                messageContext.Bot,
                messageContext.Room.ToPlatformRoom(),
                null,
                new Uri("https://app.ab.bot/skills/pug"));

            var received = Assert.Single(requestHandler.ReceivedRequests);
            if (expectedApiToken is null)
            {
                Assert.Null(received.Headers.Authorization);
                Assert.False(received.Headers.Contains("X-Functions-Key"));
            }
            else
            {
                Assert.NotNull(received.Headers.Authorization);
                Assert.Equal(expectedApiToken, received.Headers.Authorization.Parameter);
                Assert.Equal("Bearer", received.Headers.Authorization.Scheme);
                Assert.Equal(new[] { expectedApiToken }, received.Headers.GetValues("X-Functions-Key").ToArray());
            }
        }

        [Fact]
        public async Task ReturnsAccessDeniedIfCallerDoesNotHavePermissionToRunSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = await env.CreateSkillAsync("pug", restricted: true);
            var caller = env.Activate<SkillRunnerClient>();

            var response = await caller.SendAsync(
                skill,
                new Arguments(string.Empty),
                string.Empty,
                messageContext.Mentions,
                messageContext.FromMember,
                messageContext.Bot,
                messageContext.Room.ToPlatformRoom(),
                null,
                new Uri("https://app.ab.bot/skills/pug"));

            Assert.NotNull(response.Replies);
            var reply = Assert.Single(response.Replies);
            Assert.Equal(
                $"I’m afraid I can’t do that, <@{user.PlatformUserId}>. `<@{organization.PlatformBotUserId}> who can pug` to find out who can change permissions for this skill.",
                reply);
            Assert.False(response.Success);
            Assert.NotNull(response.Errors);
            var error = Assert.Single(response.Errors).Description;
            Assert.Equal("The user does not have permission to run this skill.", error);
            var logEntry = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(logEntry);
            Assert.Equal("The user does not have permission to run this skill.\n\n", logEntry.ErrorMessage);
            await env.AuditLog.AssertMostRecent<SkillRunAuditEvent>(
                description: "Ran command `pug` with no arguments in _a channel with an unknown name_ (`C000000001`).",
                errorMessage: "The user does not have permission to run this skill.\n\n");
        }

        [Fact]
        public async Task ThrowsInvalidOperationExceptionIfContentTypeIsNull()
        {
            var activity = new Activity("test").Start();
            var httpClient = new FakeHttpClient();
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<HttpClient>(httpClient)
                .Build();
            var member = env.TestData.Member;
            var admin = await env.CreateAdminMemberAsync();
            var skill = await env.CreateSkillAsync("pug", restricted: true);
            await env.Permissions.SetPermissionAsync(member, skill, Capability.Use, admin);
            httpClient.AddResponse(new Uri(env.SkillOptions.DotNetEndpoint!), new HttpResponseMessage());
            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var caller = env.Activate<SkillRunnerClient>();

            var exception = await Assert.ThrowsAsync<SkillRunException>(() => caller.SendAsync(
                skill,
                new Arguments(string.Empty),
                string.Empty,
                messageContext.Mentions,
                messageContext.FromMember,
                messageContext.Bot,
                messageContext.Room.ToPlatformRoom(),
                null,
                new Uri("https://app.ab.bot/skills/pug")));

            Assert.Equal(
                $"Internal error calling skill runner endpoint. Contact 'support@ab.bot' for help and give them the request ID: {activity.Id}",
                exception.Message);
        }

        [Fact]
        public async Task WrapsHttpRequestExceptionWithSkillRunException()
        {
            var activity = new Activity("Test").Start();
            var httpClient = new FakeHttpClient();
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<HttpClient>(httpClient)
                .Build();
            var member = env.TestData.Member;
            var admin = await env.CreateAdminMemberAsync();
            var skill = await env.CreateSkillAsync("pug", restricted: true);
            await env.Permissions.SetPermissionAsync(member, skill, Capability.Use, admin);
            httpClient.AddResponseException(new Uri(env.SkillOptions.DotNetEndpoint!), HttpMethod.Post, new HttpRequestException("shit broke"));
            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var caller = env.Activate<SkillRunnerClient>();

            var exception = await Assert.ThrowsAsync<SkillRunException>(() => caller.SendAsync(
                skill,
                new Arguments(string.Empty),
                string.Empty,
                messageContext.Mentions,
                messageContext.FromMember,
                messageContext.Bot,
                messageContext.Room.ToPlatformRoom(),
                null,
                new Uri("https://app.ab.bot/skills/pug")));

            Assert.Equal($"Internal error calling skill runner endpoint. Contact 'support@ab.bot' for help and give them the request ID: {activity.Id}", exception.Message);
            Assert.Equal(skill.Id, exception.Skill.Id);
        }

        [Fact]
        public async Task WhenUserSkillsDisabledForOrganizationReturnFailMessage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.UserSkillsEnabled = false;
            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = await env.CreateSkillAsync("pug");
            skill.CacheKey = "somecachekey";
            var caller = env.Activate<SkillRunnerClient>();

            var response = await caller.SendAsync(
                skill,
                new Arguments(string.Empty),
                string.Empty,
                Enumerable.Empty<Member>(),
                messageContext.FromMember,
                messageContext.Bot,
                messageContext.Room.ToPlatformRoom(),
                null,
                new Uri("https://app.ab.bot/skills/pug"));

            Assert.False(response.Success);
            Assert.NotNull(response.Replies);
            Assert.NotNull(response.Errors);
            var error = Assert.Single(response.Errors).Description;
            Assert.Equal("Running custom skills is disabled for this organization.", error);
            Assert.Equal("Running custom skills is disabled for this organization. An administrator can enable it in the <https://app.ab.bot/settings/organization/advanced|Advanced Settings Page>.", response.Replies.Single());
            var logEntry = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(logEntry);
            Assert.Equal("Running custom skills is disabled for this organization.\n\n", logEntry.ErrorMessage);
        }

        [Fact]
        public async Task WhenSkillDisabledReturnFailMessage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.UserSkillsEnabled = true;
            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = await env.CreateSkillAsync("pug", enabled: false);
            skill.CacheKey = "somecachekey";
            var caller = env.Activate<SkillRunnerClient>();

            var response = await caller.SendAsync(
                skill,
                new Arguments(string.Empty),
                string.Empty,
                Enumerable.Empty<Member>(),
                messageContext.FromMember,
                messageContext.Bot,
                messageContext.Room.ToPlatformRoom(),
                null,
                new Uri("https://app.ab.bot/skills/pug"));

            Assert.False(response.Success);
            Assert.NotNull(response.Errors);
            var error = Assert.Single(response.Errors).Description;
            Assert.Equal("Failed to run the `pug` skill because it is disabled.", error);
            Assert.NotNull(response.Replies);
            Assert.Equal(error, response.Replies.Single());
            var logEntry = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(logEntry);
            Assert.Equal(error + "\n\n", logEntry.ErrorMessage);
        }
    }

    public class TheSendHttpTriggerAsyncMethod
    {
        [Fact]
        public async Task WhenTriggeredCallsTheAbbotFunctionsEndpointWithCorrectArguments()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var user = member.User;
            const string urlText = "http://localhost:7071/api/skillrunner";
            var url = new Uri(urlText);
            var requestHandler = env.Http.AddResponse(
                url,
                HttpMethod.Post,
                new SkillRunResponse
                {
                    Success = true,
                    Replies = new List<string> { "https://taters/" },
                    Errors = new List<RuntimeError>(),
                    Content = null,
                    ContentType = null
                },
                "application/vnd.abbot.v1+json");
            var trigger = new SkillHttpTrigger
            {
                Skill = await env.CreateSkillAsync("pug"),
                Creator = user,
                RoomId = "C001",
            };
            var triggerEvent = new HttpTriggerRequest
            {
                RawBody = "Raw Text"
            };
            var caller = env.Activate<SkillRunnerClient>();

            var response = await caller.SendHttpTriggerAsync(
                trigger,
                triggerEvent,
                new Uri("https://app.ab.bot/skills/pug"),
                Guid.NewGuid());

            Assert.NotNull(response.Replies);
            var reply = Assert.Single(response.Replies);
            Assert.Equal("https://taters/", reply);
            var received = AbbotJsonFormat.Default.Deserialize<SkillMessage>(requestHandler.GetReceivedRequestBody());
            Assert.NotNull(received);
            Assert.True(received.SkillInfo.IsRequest);
            Assert.False(received.SkillInfo.IsChat);
            Assert.NotNull(received.SkillInfo.Request);
            Assert.Equal(organization.PlatformBotUserId, received.SkillInfo.Bot.Id);
            Assert.Equal("test-abbot", received.SkillInfo.Bot.Name);
            Assert.Equal("pug", received.SkillInfo.SkillName);
            Assert.Equal(member.DisplayName, received.SkillInfo.From.Name);
            Assert.Equal("Raw Text", received.SkillInfo.Request.RawBody);
            Assert.Null(received.SkillInfo.Message);
            Assert.Null(received.ConversationInfo);
            Assert.Equal("C001", received.SkillInfo.Room.Id);
        }

        [Fact]
        public async Task ThrowsExceptionWhenServerIsTemporarilyDown()
        {
            var activity = new Activity("Test").Start();
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            const string urlText = "http://localhost:7071/api/skillrunner";
            var url = new Uri(urlText);
            for (int i = 0; i < 4; i++) // Make it return this for every retry.
            {
                env.Http.AddResponse(
                    url,
                    HttpMethod.Post,
                    "The service is unavailable.",
                    "text/html");
            }
            var trigger = new SkillHttpTrigger
            {
                Skill = await env.CreateSkillAsync("pug"),
                Creator = user,
                RoomId = "C001",
            };
            var triggerEvent = new HttpTriggerRequest
            {
                RawBody = "Raw Text"
            };
            var client = env.Activate<SkillRunnerClient>();

            var exception = await Assert.ThrowsAsync<SkillRunException>(() => client.SendHttpTriggerAsync(
                trigger,
                triggerEvent,
                new Uri("https://app.ab.bot/skills/pug"),
                Guid.NewGuid()));

            Assert.Equal(
                $"Internal error calling skill runner endpoint. Contact 'support@ab.bot' for help and give them the request ID: {activity.Id}",
                exception.Message);
        }

        [Fact]
        public async Task RetriesWhenServerUnavailableExceptionThrown()
        {
            new Activity("Test").Start();
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            const string urlText = "http://localhost:7071/api/skillrunner";
            var url = new Uri(urlText);
            env.Http.AddResponse(
                url,
                HttpMethod.Post,
                "The service is unavailable.",
                "text/html");
            var requestHandler = env.Http.AddResponse(
                url,
                HttpMethod.Post,
                new SkillRunResponse
                {
                    Success = true,
                    Replies = new List<string> { "https://taters/" },
                    Errors = new List<RuntimeError>(),
                    Content = null,
                    ContentType = null
                },
                "application/vnd.abbot.v1+json");
            var trigger = new SkillHttpTrigger
            {
                Skill = await env.CreateSkillAsync("pug"),
                Creator = user,
                RoomId = "the-room"
            };
            var triggerEvent = new HttpTriggerRequest
            {
                RawBody = "Raw Text"
            };
            var client = env.Activate<SkillRunnerClient>();

            var response = await client.SendHttpTriggerAsync(trigger, triggerEvent,
                new Uri("https://app.ab.bot/skills/pug"), Guid.NewGuid());

            Assert.NotNull(response.Replies);
            var reply = Assert.Single(response.Replies);
            Assert.Equal("https://taters/", reply);
            var received = AbbotJsonFormat.Default.Deserialize<SkillMessage>(requestHandler.GetReceivedRequestBody());
            Assert.NotNull(received);
            Assert.True(received.SkillInfo.IsRequest);
            Assert.False(received.SkillInfo.IsChat);
            Assert.NotNull(received.SkillInfo.Request);
            Assert.Equal("pug", received.SkillInfo.SkillName);
            Assert.Equal(member.DisplayName, received.SkillInfo.From.Name);
            Assert.Equal("Raw Text", received.SkillInfo.Request.RawBody);
        }

        [Theory]
        [InlineData("application/unexpected")]
        [InlineData("text/html")]
        public async Task HandlesInvalidContentTypeResponse(string contentType)
        {
            var activity = new Activity("Test").Start();
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            const string urlText = "http://localhost:7071/api/skillrunner";
            var url = new Uri(urlText);
            env.Http.AddResponse(
                url,
                HttpMethod.Post,
                "<html></html>",
                contentType);
            var trigger = new SkillHttpTrigger
            {
                Skill = await env.CreateSkillAsync("pug", codeText: "// Ignored"),
                Creator = user,
                RoomId = "C001",
            };
            trigger.Creator.Members = new List<Member>
            {
                new()
                {
                    OrganizationId = organization.Id,
                    UserId = user.Id,
                    User = trigger.Creator
                }
            };
            var triggerEvent = new HttpTriggerRequest
            {
                RawBody = "Raw Text"
            };
            var client = env.Activate<SkillRunnerClient>();

            var exception = await Assert.ThrowsAsync<SkillRunException>(() => client.SendHttpTriggerAsync(
                trigger,
                triggerEvent,
                new Uri("https://app.ab.bot/skills/pug"),
                Guid.NewGuid()));
            Assert.Equal($"Internal error calling skill runner endpoint. Contact 'support@ab.bot' for help and give them the request ID: {activity.Id}", exception.Message);
        }
    }
}
