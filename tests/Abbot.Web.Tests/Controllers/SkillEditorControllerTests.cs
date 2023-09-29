using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot;
using Serious.Abbot.Controllers;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.TestHelpers;
using Xunit;

public class SkillEditorControllerTests : ControllerTestBase<SkillEditorController>
{
    protected override string? ExpectedArea => InternalApiControllerBase.Area;

    public class TheInvokeAsyncMethod : SkillEditorControllerTests
    {
        [Fact]
        public async Task ReturnsNotFoundResultIfSkillNotFound()
        {
            var env = Env;
            var (_, user, member) = env.TestData;
            user.NameIdentifier = "test";
            await env.Db.SaveChangesAsync();
            var response = new SkillRunResponse
            {
                Success = true,
                Replies = new List<string> { "Got your message loud and clear!" },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            };
            env.SkillRunnerClient.PushResponse(response);
            var invokeSkillRequest = new SkillRunRequest
            {
                Name = "test-skill",
                Arguments = "args",
                Code = "// code"
            };

            var (_, result) = await InvokeControllerAsync(async controller => {
                controller.Request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, "secret-token");
                controller.Request.Headers.Add(CommonConstants.UserIdHeaderName, member.Id.ToString(CultureInfo.InvariantCulture));
                controller.Request.Headers.Add(CommonConstants.SkillApiTimestampHeaderName, DateTimeOffset.UtcNow.ToString("o"));

                return await controller.InvokeAsync(new(0), invokeSkillRequest);
            });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsErrorResultIfSkillRunnerThrowsException()
        {
            var env = Env;
            var (_, user, member) = env.TestData;
            user.NameIdentifier = "really fake";
            var skill = await env.CreateSkillAsync("test-skill", CodeLanguage.Python);
            env.SkillRunnerClient.PushException(new InvalidOperationException());
            env.CachingCompilerService.OverrideExistAsyncToReturnTrue = true;

            var invokeSkillRequest = new SkillRunRequest
            {
                Name = "test-skill",
                Arguments = "args",
                Code = "// code"
            };

            var (_, result) = await InvokeControllerAsync(async controller => {
                controller.Request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, "secret-token");
                controller.Request.Headers.Add(CommonConstants.UserIdHeaderName, member.Id.ToString(CultureInfo.InvariantCulture));
                controller.Request.Headers.Add(CommonConstants.SkillApiTimestampHeaderName, DateTimeOffset.UtcNow.ToString("o"));

                return await controller.InvokeAsync(skill, invokeSkillRequest);
            });

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var errors = objectResult.Value as List<RuntimeError>;
            Assert.NotNull(errors);
            Assert.Single(errors);
            Assert.Equal("I have encountered an unexpected error and notified my creators. They are very sorry for the inconvenience.", errors[0].Description);
            Assert.Equal("Exception", errors[0].ErrorId);
        }

        [Fact]
        public async Task CompilesCodeIfNotInCache()
        {
            var env = Env;
            var (organization, user, member) = env.TestData;
            user.NameIdentifier = "test";
            var skill = await env.CreateSkillAsync("whatevs");
            var response = new SkillRunResponse
            {
                Success = true,
                Replies = new List<string> { "Got your message loud and clear!" },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            };
            env.SkillRunnerClient.PushResponse(response);
            var organizationIdentifier = new FakeOrganizationIdentifier(organization);
            var compilationResult = new FakeSkillCompilationResult("// code");
            env.CachingCompilerService.AddCompilationResult(
                organizationIdentifier,
                "// code",
                compilationResult);

            var invokeSkillRequest = new SkillRunRequest
            {
                Name = "test-skill",
                Arguments = "args",
                Code = "// code"
            };

            var (_, result) = await InvokeControllerAsync(async controller => {
                controller.Request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, "secret-token");
                controller.Request.Headers.Add(CommonConstants.UserIdHeaderName, member.Id.ToString(CultureInfo.InvariantCulture));
                controller.Request.Headers.Add(CommonConstants.SkillApiTimestampHeaderName, DateTimeOffset.UtcNow.ToString("o"));

                return await controller.InvokeAsync(skill, invokeSkillRequest);
            });

            var objectResult = Assert.IsType<ObjectResult>(result);
            var message = objectResult.Value as string;
            Assert.Equal("Got your message loud and clear!", message);
            Assert.True(env.CachingCompilerService.CompileAsyncCalled);
        }

        [Fact]
        public async Task DoesNotCompileIfAlreadyInCache()
        {
            var env = Env;
            var (_, user, member) = env.TestData;
            user.NameIdentifier = "really fake";
            var skill = await env.CreateSkillAsync("test-skill");
            var response = new SkillRunResponse
            {
                Success = true,
                Replies = new List<string> { "Got your message loud and clear!" },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            };
            env.SkillRunnerClient.PushResponse(response);
            env.CachingCompilerService.OverrideExistAsyncToReturnTrue = true;

            var invokeSkillRequest = new SkillRunRequest
            {
                Name = "test-skill",
                Arguments = "args",
                Code = "// code"
            };

            var (_, result) = await InvokeControllerAsync(async controller => {
                controller.Request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, "secret-token");
                controller.Request.Headers.Add(CommonConstants.UserIdHeaderName, member.Id.ToString(CultureInfo.InvariantCulture));
                controller.Request.Headers.Add(CommonConstants.SkillApiTimestampHeaderName, DateTimeOffset.UtcNow.ToString("o"));

                return await controller.InvokeAsync(skill, invokeSkillRequest);
            });

            var objectResult = Assert.IsType<ObjectResult>(result);
            var message = objectResult.Value as string;
            Assert.Equal("Got your message loud and clear!", message);
            Assert.False(env.CachingCompilerService.CompileAsyncCalled);
            var runnerInvocation = env.SkillRunnerClient.Invocations.Single();
            Assert.NotNull(runnerInvocation.Skill);
            Assert.Equal(skill.Id, runnerInvocation.Skill.Id);
        }

        [Theory]
        [InlineData(CodeLanguage.JavaScript)]
        [InlineData(CodeLanguage.Python)]
        public async Task DoesNotCompileCodeForNonCSharpAndPassesSkillId(CodeLanguage language)
        {
            var env = Env;
            var (_, user, member) = env.TestData;
            user.NameIdentifier = "fake";
            var skill = await env.CreateSkillAsync("test-skill", language);
            var response = new SkillRunResponse
            {
                Success = true,
                Replies = new List<string> { "Got your message loud and clear!" },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            };
            env.SkillRunnerClient.PushResponse(response);

            var invokeSkillRequest = new SkillRunRequest
            {
                Name = "test-skill",
                Arguments = "args",
                Code = "// code"
            };

            var (_, result) = await InvokeControllerAsync(async controller => {
                controller.Request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, "secret-token");
                controller.Request.Headers.Add(CommonConstants.UserIdHeaderName, member.Id.ToString(CultureInfo.InvariantCulture));
                controller.Request.Headers.Add(CommonConstants.SkillApiTimestampHeaderName, DateTimeOffset.UtcNow.ToString("o"));

                return await controller.InvokeAsync(skill, invokeSkillRequest);
            });

            var objectResult = Assert.IsType<ObjectResult>(result);
            var message = objectResult.Value as string;
            Assert.Equal("Got your message loud and clear!", message);
            Assert.False(env.CachingCompilerService.CompileAsyncCalled);
            var runnerInvocation = env.SkillRunnerClient.Invocations.Single();
            Assert.NotNull(runnerInvocation.Skill);
            Assert.Equal(skill.Id, runnerInvocation.Skill.Id);
        }

        [Fact]
        public async Task ReturnsImmediatelyIfSkillRestrictedAndUserDoesNotHavePermission()
        {
            var env = Env;
            var (_, user, member) = env.TestData;
            user.NameIdentifier = "fake";
            var skill = await env.CreateSkillAsync("whatevs", restricted: true);
            var response = new SkillRunResponse
            {
                Success = true,
                Replies = new List<string> { "Got your message loud and clear!" },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            };
            env.SkillRunnerClient.PushResponse(response);

            var invokeSkillRequest = new SkillRunRequest
            {
                Name = "test-skill",
                Arguments = "args",
                Code = "// code"
            };

            var (_, result) = await InvokeControllerAsync(async controller => {
                controller.Request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, "secret-token");
                controller.Request.Headers.Add(CommonConstants.UserIdHeaderName, member.Id.ToString(CultureInfo.InvariantCulture));
                controller.Request.Headers.Add(CommonConstants.SkillApiTimestampHeaderName, DateTimeOffset.UtcNow.ToString("o"));

                return await controller.InvokeAsync(skill, invokeSkillRequest);
            });

            var objectResult = Assert.IsType<ObjectResult>(result);
            var message = objectResult.Value as string;
            Assert.Equal(
                $"I'm afraid I can't do that, <@{user.PlatformUserId}>. `@abbot who can whatevs` to find out who can change permissions for this skill.",
                message);
            Assert.False(env.CachingCompilerService.CompileAsyncCalled);
        }

        [Fact]
        public async Task ReturnsImmediatelyIfUserCanNotEditRestrictedSkill()
        {
            var env = Env;
            var member = await env.CreateMemberAsync();
            member.User.NameIdentifier = "fake";
            var skill = await env.CreateSkillAsync("whatevs", restricted: true);
            var response = new SkillRunResponse
            {
                Success = true,
                Replies = new List<string> { "Got your message loud and clear!" },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            };
            env.SkillRunnerClient.PushResponse(response);
            var invokeSkillRequest = new SkillRunRequest
            {
                Name = "test-skill",
                Arguments = "args",
                Code = "// code"
            };
            AuthenticateAs(member);

            var (_, result) = await InvokeControllerAsync(async controller => {
                controller.Request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, "secret-token");
                controller.Request.Headers.Add(CommonConstants.UserIdHeaderName, member.Id.ToString(CultureInfo.InvariantCulture));
                controller.Request.Headers.Add(CommonConstants.SkillApiTimestampHeaderName, DateTimeOffset.UtcNow.ToString("o"));

                return await controller.InvokeAsync(skill, invokeSkillRequest);
            });

            var objectResult = Assert.IsType<ObjectResult>(result);
            var message = objectResult.Value as string;
            Assert.Equal(
                $"I'm afraid I can't do that, <@{member.User.PlatformUserId}>. `@abbot who can whatevs` to find out who can change permissions for this skill.",
                message);
            Assert.False(env.CachingCompilerService.CompileAsyncCalled);
        }

        [Fact]
        public async Task ReturnsImmediatelyUserInAnotherOrgEvenIfSkillUnrestricted()
        {
            var env = Env;
            var user = env.TestData.ForeignUser;
            var foreignMember = env.TestData.ForeignMember;
            foreignMember.User.NameIdentifier = "fake";
            var skill = await env.CreateSkillAsync("whatevs");
            var response = new SkillRunResponse
            {
                Success = true,
                Replies = new List<string> { "Got your message loud and clear!" },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            };
            env.SkillRunnerClient.PushResponse(response);

            var invokeSkillRequest = new SkillRunRequest
            {
                Name = "test-skill",
                Arguments = "args",
                Code = "// code"
            };

            AuthenticateAs(foreignMember);

            var (_, result) = await InvokeControllerAsync(async controller => {
                controller.Request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, "secret-token");
                controller.Request.Headers.Add(CommonConstants.UserIdHeaderName, foreignMember.Id.ToString(CultureInfo.InvariantCulture));
                controller.Request.Headers.Add(CommonConstants.SkillApiTimestampHeaderName, DateTimeOffset.UtcNow.ToString("o"));

                return await controller.InvokeAsync(skill, invokeSkillRequest);
            });

            var objectResult = Assert.IsType<ObjectResult>(result);
            var message = objectResult.Value as string;
            Assert.Equal(
                $"I'm afraid I can't do that, <@{user.PlatformUserId}>. `@abbot who can whatevs` to find out who can change permissions for this skill.",
                message);
            Assert.False(env.CachingCompilerService.CompileAsyncCalled);
        }
    }
}
