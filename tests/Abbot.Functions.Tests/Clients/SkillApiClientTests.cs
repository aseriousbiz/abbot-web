using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Abbot.Serialization;
using Serious.TestHelpers;
using Xunit;

public class SkillApiClientTests
{
    public class TheSendJsonAsyncMethod
    {
        [Fact]
        public async Task SerializesConversationReference()
        {
            var expectedUri = new Uri("https://localhost/some/api");
            var environment = new FakeEnvironment();
            var httpMessageHandler = new FakeHttpMessageHandler();
            var expectedResponseBody = new ProactiveBotMessageResponse(true);
            httpMessageHandler.AddResponse(expectedUri, HttpMethod.Post, expectedResponseBody);
            var httpClient = new HttpClient(httpMessageHandler);
            var reference = new ConversationReference
            {
                User = new ChannelAccount("U111") { Properties = new JObject() }
            };
            var contextAccessor = new FakeSkillContextAccessor();

            var apiClient = new SkillApiClient(httpClient, environment, contextAccessor);

            var responseBody = await apiClient.SendJsonAsync<ConversationReference, ProactiveBotMessageResponse>(expectedUri, HttpMethod.Post, reference);
            Assert.NotNull(responseBody);
            Assert.True(responseBody.Success);
        }

        [Fact]
        public async Task DeserializesSomeInterfaces()
        {
            var expectedUri = new Uri("https://localhost/some/api");
            var environment = new FakeEnvironment();
            var httpMessageHandler = new FakeHttpMessageHandler();
            var workingHours = new WorkingHours(new TimeOnly(9, 0), new(17, 0));
            var expectedResponseBody = new[]
            {
                new PlatformUser("id1", "user1", "user1")
                {
                    Location = new Location(null, "Some address", "America/New_York")
                },
                new PlatformUser("id2", "user2", "user2")
                {
                    WorkingHours = workingHours
                },
            };
            var expectedResponseBodyJson = AbbotJsonFormat.Default.Serialize(expectedResponseBody);
            httpMessageHandler.AddResponse(expectedUri, HttpMethod.Get, expectedResponseBodyJson, "application/json");
            var httpClient = new HttpClient(httpMessageHandler);
            var contextAccessor = new FakeSkillContextAccessor();

            var apiClient = new SkillApiClient(httpClient, environment, contextAccessor);

            var responseBody = await apiClient.GetAsync<IReadOnlyList<IChatUser>>(expectedUri);
            Assert.NotNull(responseBody);
            Assert.Collection(responseBody,
                u => {
                    Assert.Equal("id1", u.Id);
                    Assert.Equal("America/New_York", u.Location?.TimeZone?.Id);
                },
                u => {
                    Assert.Equal("id2", u.Id);
                    Assert.Equal("9:00am-5:00pm", u.WorkingHours?.Humanize());
                });
        }

        [Fact]
        public async Task DeserializesSomeConcreteTypes()
        {
            var expectedUri = new Uri("https://localhost/some/api");
            var environment = new FakeEnvironment();
            var httpMessageHandler = new FakeHttpMessageHandler();
            var expectedResponseBody = new[]
            {
                new PlatformUser("id1", "user1", "user1"),
                new PlatformUser("id2", "user2", "user2"),
            };
            httpMessageHandler.AddResponse(expectedUri, HttpMethod.Get, expectedResponseBody);
            var httpClient = new HttpClient(httpMessageHandler);
            var contextAccessor = new FakeSkillContextAccessor();

            var apiClient = new SkillApiClient(httpClient, environment, contextAccessor);

            var responseBody = await apiClient.GetAsync<IReadOnlyList<PlatformUser>>(expectedUri);
            Assert.NotNull(responseBody);
            Assert.Collection(responseBody,
                u => Assert.Equal(u.Id, "id1"),
                u => Assert.Equal(u.Id, "id2"));
        }

        [Fact]
        public async Task HandlesAppShutdown()
        {
            var expectedUri = new Uri("https://localhost/some/api");
            var environment = new FakeEnvironment();
            var httpMessageHandler = new FakeHttpMessageHandler();
            var expectedResponseBody = new ProactiveBotMessageResponse(true);
            var response = FakeHttpMessageHandler.CreateResponse(expectedResponseBody);

            httpMessageHandler.AddResponse(expectedUri, HttpMethod.Post,
                async () => {
                    await Task.Delay(1).ConfigureAwait(false);
                    environment.CancellationTokenSource.Cancel();
                    return response;

                });
            var httpClient = new HttpClient(httpMessageHandler);
            var reference = new ConversationReference
            {
                User = new ChannelAccount("U111") { Properties = new JObject() }
            };
            var contextAccessor = new FakeSkillContextAccessor();
            var apiClient = new SkillApiClient(httpClient, environment, contextAccessor);

            await Assert.ThrowsAsync<TaskCanceledException>(() => apiClient.SendJsonAsync<ConversationReference, ProactiveBotMessageResponse>(expectedUri, HttpMethod.Post, reference));
        }
    }

    public class TheDownloadAssemblyAsyncMethod
    {
        static Func<HttpRequestMessage, Task<bool>> CreateExpectedCompilationRequest(
            Uri expectedUri,
            CompilationRequestType expectedType)
        {
            return async req => {
                var stream = new MemoryStream();
                Assert.NotNull(req.Content);
                await req.Content.CopyToAsync(stream);
                var body = JsonConvert.DeserializeObject<CompilationRequest>(await stream.ReadAsStringAsync());
                Assert.NotNull(body);
                return body.Type == expectedType
                       && req.RequestUri == expectedUri
                       && req.Method == HttpMethod.Post;
            };
        }

        [Theory]
        [InlineData(false, CompilationRequestType.Cached)]
        [InlineData(true, CompilationRequestType.Recompile)]
        public async Task DownloadsAssemblyAndSymbolsStreamAndReturnsSkillAssembly(
            bool recompile,
            CompilationRequestType expectedCompilationRequestType)
        {
            var (assemblyStream, symbolsStream) = await TestSkillCompiler.CompileCodeToStreamsAsync<IScriptGlobals>(
                "await Bot.ReplyAsync(\"Winner! Winner!\");");
            var environment = new FakeEnvironment
            {
                ["SkillApiBaseUriFormatString"] = "https://localhost/api/skills/{0}"
            };
            var expectedAssemblyUri = new Uri("https://localhost/api/skills/42/compilation");
            var httpMessageHandler = new FakeHttpMessageHandler();

            var expectedAssemblyRequest =
                CreateExpectedCompilationRequest(expectedAssemblyUri, expectedCompilationRequestType);
            var expectedSymbolsRequest =
                CreateExpectedCompilationRequest(expectedAssemblyUri, CompilationRequestType.Symbols);

            httpMessageHandler.AddStreamResponse(expectedAssemblyRequest, assemblyStream);
            httpMessageHandler.AddStreamResponse(expectedSymbolsRequest, symbolsStream);
            var httpClient = new HttpClient(httpMessageHandler);
            var contextAccessor = new FakeSkillContextAccessor(42);
            var apiClient = new SkillApiClient(httpClient, environment, contextAccessor);
            var skillIdentifier = new FakeSkillAssemblyIdentifier
            {
                PlatformId = "T001",
                PlatformType = PlatformType.Slack,
                SkillId = 42
            };

            var download = await apiClient.DownloadCompiledSkillAsync(skillIdentifier, recompile);

            var botContext = new FakeBot();
            await download.RunAsync(botContext);
            var reply = Assert.Single(botContext.Replies);
            Assert.Equal("Winner! Winner!", reply);
        }

        [Fact]
        public async Task DownloadsAssemblyWithoutSymbolsStreamAndReturnsSkillAssembly()
        {
            var (assemblyStream, _) = await TestSkillCompiler.CompileCodeToStreamsAsync<IScriptGlobals>(
                "await Bot.ReplyAsync(\"Chicken Dinner!\");");
            var environment = new FakeEnvironment
            {
                ["SkillApiBaseUriFormatString"] = "https://localhost/api/skills/{0}"
            };
            var expectedAssemblyUri = new Uri("https://localhost/api/skills/456/compilation");
            var httpMessageHandler = new FakeHttpMessageHandler();
            var expectedAssemblyRequest =
                CreateExpectedCompilationRequest(expectedAssemblyUri, CompilationRequestType.Cached);

            httpMessageHandler.AddStreamResponse(expectedAssemblyRequest, assemblyStream);
            var httpClient = new HttpClient(httpMessageHandler);
            var contextAccessor = new FakeSkillContextAccessor(456);
            var apiClient = new SkillApiClient(httpClient, environment, contextAccessor);
            var skillIdentifier = new FakeSkillAssemblyIdentifier
            {
                PlatformId = "T001",
                PlatformType = PlatformType.Slack,
                SkillId = 456
            };

            var download = await apiClient.DownloadCompiledSkillAsync(skillIdentifier, recompile: false);

            var botContext = new FakeBot();
            await download.RunAsync(botContext);
            var reply = Assert.Single(botContext.Replies);
            Assert.Equal("Chicken Dinner!", reply);
        }
    }
}
