using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class SignalerTests
{
    public class TheSignalAsyncMethod
    {
        [Fact]
        public async Task SendsSignalRequest()
        {
            var url = new Uri("https://ab.bot/api/skills/42/signal");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(url, HttpMethod.Post, new ApiResult());
            var message = new SkillMessage
            {
                SkillInfo = new SkillInfo
                {
                    SkillName = "source-skill",
                    Arguments = "the original skill args",
                    Room = new PlatformRoom("C01234", "the-clean-room"),
                    Mentions = new List<PlatformUser> { new("U1234", "username", "name") },
                    SkillUrl = new Uri("https://app.ab.bot/skills/source-skill")
                },
                RunnerInfo = new SkillRunnerInfo
                {
                    SkillId = 42,
                    MemberId = 23,
                }
            };
            var contextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(message, "FakeApiKey")
            };
            var signaler = new Signaler(apiClient, contextAccessor);

            var result = await signaler.SignalAsync("some-signal", "the signal args");

            Assert.True(result.Ok);
            var signalRequest = Assert.IsType<SignalRequest>(apiClient.SentJson[(url, HttpMethod.Post)][0]);
            Assert.Equal("some-signal", signalRequest.Name);
            Assert.Equal("the signal args", signalRequest.Arguments);
            Assert.Equal(23, signalRequest.SenderId);
            Assert.Equal("the-clean-room", signalRequest.Room.Name);
            Assert.Equal("C01234", signalRequest.Room.Id);
            var source = signalRequest.Source;
            Assert.Equal("source-skill", source.SkillName);
            Assert.Equal("the original skill args", source.Arguments);
            var mention = Assert.Single(source.Mentions);
            Assert.Equal("U1234", mention.Id);
            Assert.Equal(new Uri("https://app.ab.bot/skills/source-skill"), source.SkillUrl);
        }

        [Fact]
        public async Task SendsSignalRequestForSkillCalledByHttpTrigger()
        {
            var url = new Uri("https://ab.bot/api/skills/42/signal");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(url, HttpMethod.Post, new ApiResult());
            var message = new SkillMessage
            {
                SkillInfo = new SkillInfo
                {
                    SkillName = "source-skill",
                    Arguments = "the original skill args",
                    Room = new PlatformRoom("C01234", "the-clean-room"),
                    Mentions = new List<PlatformUser> { new("U1234", "username", "name") },
                    SkillUrl = new Uri("https://app.ab.bot/skills/source-skill"),
                    IsRequest = true,
                    IsChat = false,
                    IsInteraction = false,
                    IsSignal = false,
                    Request = new HttpTriggerRequest
                    {
                        RawBody = "Http body",
                        HttpMethod = "PUT"
                    }
                },
                RunnerInfo = new SkillRunnerInfo
                {
                    SkillId = 42,
                    MemberId = 23,
                }
            };
            var contextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(message, "FakeApiKey")
            };
            var signaler = new Signaler(apiClient, contextAccessor);

            var result = await signaler.SignalAsync("some-signal", "the signal args");

            Assert.True(result.Ok);
            var signalRequest = Assert.IsType<SignalRequest>(apiClient.SentJson[(url, HttpMethod.Post)][0]);
            Assert.Equal("some-signal", signalRequest.Name);
            Assert.Equal("the signal args", signalRequest.Arguments);
            Assert.Equal(23, signalRequest.SenderId);
            Assert.Equal("the-clean-room", signalRequest.Room.Name);
            Assert.Equal("C01234", signalRequest.Room.Id);
            var source = signalRequest.Source;
            Assert.Equal("source-skill", source.SkillName);
            Assert.Equal("the original skill args", source.Arguments);
            var mention = Assert.Single(source.Mentions);
            Assert.Equal("U1234", mention.Id);
            Assert.Equal(new Uri("https://app.ab.bot/skills/source-skill"), source.SkillUrl);
            Assert.True(source.IsRequest);
            Assert.NotNull(source.Request);
            Assert.Equal("Http body", source.Request.RawBody);
        }

        [Fact]
        public async Task SendsSignalRequestForSkillCalledByPattern()
        {
            var url = new Uri("https://ab.bot/api/skills/42/signal");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(url, HttpMethod.Post, new ApiResult());
            var message = new SkillMessage
            {
                SkillInfo = new SkillInfo
                {
                    SkillName = "source-skill",
                    Arguments = "the original skill args",
                    Room = new PlatformRoom("C01234", "the-clean-room"),
                    Mentions = new List<PlatformUser> { new("U1234", "username", "name") },
                    SkillUrl = new Uri("https://app.ab.bot/skills/source-skill"),
                    IsRequest = false,
                    IsChat = true,
                    IsInteraction = false,
                    IsSignal = false,
                    Request = null,
                    Pattern = new PatternMessage
                    {
                        PatternType = PatternType.ExactMatch,
                        Pattern = "MATCH THIS"
                    }
                },
                RunnerInfo = new SkillRunnerInfo
                {
                    SkillId = 42,
                    MemberId = 23,
                }
            };
            var contextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(message, "FakeApiKey")
            };
            var signaler = new Signaler(apiClient, contextAccessor);

            var result = await signaler.SignalAsync("some-signal", "the signal args");

            Assert.True(result.Ok);
            var signalRequest = Assert.IsType<SignalRequest>(apiClient.SentJson[(url, HttpMethod.Post)][0]);
            Assert.Equal("some-signal", signalRequest.Name);
            Assert.Equal("the signal args", signalRequest.Arguments);
            Assert.Equal(23, signalRequest.SenderId);
            Assert.Equal("the-clean-room", signalRequest.Room.Name);
            Assert.Equal("C01234", signalRequest.Room.Id);
            var source = signalRequest.Source;
            Assert.Equal("source-skill", source.SkillName);
            Assert.Equal("the original skill args", source.Arguments);
            var mention = Assert.Single(source.Mentions);
            Assert.Equal("U1234", mention.Id);
            Assert.Equal(new Uri("https://app.ab.bot/skills/source-skill"), source.SkillUrl);
            Assert.False(source.IsRequest);
            Assert.True(source.IsChat);
            Assert.NotNull(source.Pattern);
            Assert.Equal("MATCH THIS", source.Pattern.Pattern);
            Assert.Equal(PatternType.ExactMatch, source.Pattern.PatternType);
        }

        [Fact]
        public async Task SendsSignalRequestWithSource()
        {
            var url = new Uri("https://ab.bot/api/skills/42/signal");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(url, HttpMethod.Post, new ApiResult());
            var message = new SkillMessage
            {
                SkillInfo = new SkillInfo
                {
                    SkillName = "source-skill",
                    Arguments = "the original skill args",
                    Room = new PlatformRoom("C01234", "the-clean-room"),
                    Mentions = new List<PlatformUser> { new("U1234", "username", "name") },
                    SkillUrl = new Uri("https://app.ab.bot/skills/source-skill")
                },
                SignalInfo = new SignalMessage
                {
                    Name = "root-signal",
                    Arguments = "root signal arguments",
                    Source = new SignalSourceMessage
                    {
                        SkillName = "root-skill",
                        Arguments = "root arguments",
                        SkillUrl = new Uri("https://app.ab.bot/skills/root-skill"),
                        IsChat = false,
                        IsRequest = true,
                        Request = new HttpTriggerRequest { HttpMethod = "PUT", RawBody = "Http body" }
                    }
                },
                RunnerInfo = new SkillRunnerInfo
                {
                    SkillId = 42,
                    MemberId = 23,
                }
            };
            var contextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(message, "FakeApiKey")
            };
            var signaler = new Signaler(apiClient, contextAccessor);

            var result = await signaler.SignalAsync("some-signal", "the signal args");

            Assert.True(result.Ok);
            var signalRequest = Assert.IsType<SignalRequest>(apiClient.SentJson[(url, HttpMethod.Post)][0]);
            var source = signalRequest.Source;
            var rootSignal = source.SignalEvent;
            Assert.NotNull(rootSignal);
            Assert.Equal("root-signal", rootSignal.Name);
            var rootSkill = rootSignal.Source;
            Assert.Equal("root-skill", rootSkill.SkillName);
            Assert.Equal(new Uri("https://app.ab.bot/skills/root-skill"), rootSkill.SkillUrl);
            Assert.True(rootSkill.IsRequest);
            Assert.NotNull(rootSkill.Request);
            Assert.Equal("Http body", rootSkill.Request.RawBody);
            Assert.Null(rootSkill.SignalEvent);
        }
    }
}
