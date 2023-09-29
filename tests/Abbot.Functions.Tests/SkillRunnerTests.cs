using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Serious.Abbot;
using Serious.Abbot.Functions.DotNet;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.TestHelpers;
using Xunit;

public class SkillRunnerTests
{
    public class TheRunMethod
    {
        [Fact]
        public async Task ReturnsMessageForGetRequest()
        {
            var compilationCache = new FakeCompilationCache();
            var skillRunner = new SkillRunner(
                new FakeCompiledSkillRunner(),
                new FakeSkillContextAccessor(),
                compilationCache,
                NullLoggerFactory.Instance);
            var request = new FakeHttpRequestData(
                HttpMethods.Get,
                new Dictionary<string, string> { { CommonConstants.SkillApiTokenHeaderName, "Some-Token" } });

            var result = await skillRunner.RunAsync(request);

            Assert.NotNull(result);
            Assert.StartsWith(
                "This skill only responds to POST requests.",
                await result.Body.ReadAsStringAsync());
        }

        [Fact]
        public async Task ThrowsExceptionIfTokenHeaderIsMissing()
        {
            var compilationCache = new FakeCompilationCache();
            var skillRunner = new SkillRunner(
                new FakeCompiledSkillRunner(),
                new FakeSkillContextAccessor(),
                compilationCache,
                NullLoggerFactory.Instance);
            var message = new SkillMessage
            {
                SkillInfo = new()
                {
                    SkillName = "some-skill",
                    Arguments = "",
                    Bot = new PlatformUser { Id = "B001", UserName = "abbot" },
                    From = new PlatformUser(),
                    Room = new PlatformRoom("C0001", "test-room"),
                },
                RunnerInfo = new()
                {
                    Scope = SkillDataScope.Organization,
                    SkillId = 42,
                    Code = "ignored"
                }
            };
            var request = await FakeHttpRequestData.CreateAsync(message);

            await Assert.ThrowsAsync<InvalidOperationException>(() => skillRunner.RunAsync(request));
        }

        [Fact]
        public async Task ThrowsExceptionIfTokenHeaderIsEmpty()
        {
            var compilationCache = new FakeCompilationCache();
            var skillRunner = new SkillRunner(
                new FakeCompiledSkillRunner(),
                new FakeSkillContextAccessor(),
                compilationCache,
                NullLoggerFactory.Instance);
            var message = new SkillMessage
            {
                SkillInfo = new()
                {
                    SkillName = "some-skill",
                    Arguments = "",
                    Bot = new PlatformUser { Id = "B001", UserName = "abbot" },
                    From = new PlatformUser(),
                    Room = new PlatformRoom("C0001", "test-room"),
                },
                RunnerInfo = new()
                {
                    Scope = SkillDataScope.Organization,
                    SkillId = 42,
                    Code = "ignored"
                }
            };
            var request = await FakeHttpRequestData.CreateAsync(
                message,
                new Dictionary<string, string> { { CommonConstants.SkillApiTokenHeaderName, string.Empty } });

            await Assert.ThrowsAsync<InvalidOperationException>(() => skillRunner.RunAsync(request));
        }

        [Fact]
        public async Task CompilesSkillAndRunsIt()
        {
            var compilationCache = new FakeCompilationCache();
            var compiledSkill = new FakeSkillAssembly();
            compilationCache.Add("The Cache Key", compiledSkill);
            var compiledSkillRunner = new FakeCompiledSkillRunner();
            var value = new SkillRunResponse
            {
                Success = true,
                Content = "some content",
                Replies = new List<string> { "Hello world!" }
            };
            var objectResult = new ObjectResult(value) { StatusCode = (int)HttpStatusCode.OK };
            compiledSkillRunner.AddObjectResult(compiledSkill, objectResult);
            var skillRunner = new SkillRunner(
                compiledSkillRunner,
                new FakeSkillContextAccessor(),
                compilationCache,
                NullLoggerFactory.Instance);
            var message = new SkillMessage
            {
                SkillInfo = new()
                {
                    SkillName = "some-skill",
                    Arguments = "",
                    Bot = new PlatformUser { Id = "B001", UserName = "abbot" },
                    From = new PlatformUser(),
                    Room = new PlatformRoom("C0001", "test-room"),
                },
                RunnerInfo = new()
                {
                    Scope = SkillDataScope.Organization,
                    SkillId = 42,
                    Code = "The Cache Key"
                }
            };
            var request = await FakeHttpRequestData.CreateAsync(
                message,
                new Dictionary<string, string>
                {
                    { CommonConstants.SkillApiTokenHeaderName, "Token" }
                });

            var result = await skillRunner.RunAsync(request);
            Assert.True(result.Headers.TryGetValues("Content-Type", out var contentTypes));
            var mediaType = Assert.Single(contentTypes);
            Assert.Equal("application/vnd.abbot.v1+json", mediaType);
            var response = await result.Body.ReadAsAsync<SkillRunResponse>();
            Assert.NotNull(response.Replies);
            Assert.Equal("some content", response.Content);
            var reply = Assert.Single(response.Replies);
            Assert.Equal("Hello world!", reply);
        }

        [Fact]
        public async Task WritesCompiledSkillRunnerStatusCodeToResponse()
        {
            var compilationCache = new FakeCompilationCache();
            var compiledSkill = new FakeSkillAssembly();
            compilationCache.Add("The Cache Key", compiledSkill);
            var compiledSkillRunner = new FakeCompiledSkillRunner();
            var value = new SkillRunResponse
            {
                Success = true,
                Content = "some content",
                Replies = new List<string> { "Hello world!" }
            };
            var objectResult = new ObjectResult(value) { StatusCode = 406 };
            compiledSkillRunner.AddObjectResult(compiledSkill, objectResult);
            var skillRunner = new SkillRunner(
                compiledSkillRunner,
                new FakeSkillContextAccessor(),
                compilationCache,
                NullLoggerFactory.Instance);
            var message = new SkillMessage
            {
                SkillInfo = new()
                {
                    SkillName = "some-skill",
                    Arguments = "",
                    Bot = new PlatformUser { Id = "B001", UserName = "abbot" },
                    From = new PlatformUser(),
                    Room = new PlatformRoom("C0001", "test-room"),
                },
                RunnerInfo = new()
                {
                    Scope = SkillDataScope.Organization,
                    SkillId = 42,
                    Code = "The Cache Key"
                }
            };
            var request = await FakeHttpRequestData.CreateAsync(
                message,
                new Dictionary<string, string>
                {
                    { CommonConstants.SkillApiTokenHeaderName, "Token" }
                });

            var result = await skillRunner.RunAsync(request);
            Assert.Equal(406, (int)result.StatusCode);
        }
    }
}
