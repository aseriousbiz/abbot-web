using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers.PublicApi;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Security;
using Serious.TestHelpers;

public class CliControllerTests
{
    public abstract class CliControllerTestBase : ControllerTestBase<CliController>, IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            HttpContext.Request.Headers["X-Client-Version"] = CliController.MinimumClientVersion.ToString();
            Env.TestData.Organization.ApiEnabled = true;
            await Env.Db.SaveChangesAsync();
        }

        public Task DisposeAsync() =>
            Task.CompletedTask;
    }

    public class TheOnActionExecutingFilter : ControllerTestBase<CliController>
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("poop")]
        [InlineData("0.0.0")]
        public async Task ReturnsProblemIfClientVersionMissingOrInappropriate(string? clientVersion)
        {
            if (clientVersion is not null)
            {
                HttpContext.Request.Headers["X-Client-Version"] = clientVersion;
            }

            var (_, result) = await InvokeControllerAsync(c => c.GetSkillAsync("some-skill")).ConfigureAwait(false);

            var objectResult = Assert.IsType<ObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(AbbotSchema.GetProblemUri("outdated_client"), problem.Type);
            Assert.Equal(AbbotSchema.GetProblemUri("outdated_client"), problem.Instance);
            Assert.Equal(
                $"The Abbot CLI client ({clientVersion ?? "0.0.0"}) you are using is outdated. Please visit https://github.com/aseriousbiz/abbot-cli/releases and use the latest release",
                problem.Detail);
            Assert.Equal("Client version outdated", problem.Title);
            Assert.Equal(StatusCodes.Status403Forbidden, problem.Status);
        }

        [Fact]
        public async Task ReturnsProblemIfApiDisabledForOrg()
        {
            HttpContext.Request.Headers["X-Client-Version"] = CliController.MinimumClientVersion.ToString();
            var (_, result) = await InvokeControllerAsync(c => c.GetSkillAsync("some-skill")).ConfigureAwait(false);

            var objectResult = Assert.IsType<ObjectResult>(result);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(AbbotSchema.GetProblemUri("api_disabled"), problem.Type);
            Assert.Equal(AbbotSchema.GetProblemUri("api_disabled"), problem.Instance);
            Assert.Equal(
                $"The organization {Env.TestData.Organization.Name} ({Env.TestData.Organization.PlatformId}) has the API disabled. This setting can be changed by an Administrator at https://app.ab.bot/settings/organization",
                problem.Detail);
            Assert.Equal("API Disabled", problem.Title);
            Assert.Equal(StatusCodes.Status403Forbidden, problem.Status);
        }
    }

    public class TheGetSkillAsyncMethod : CliControllerTestBase
    {
        [Fact]
        public async Task ReturnsNotFoundIfSkillIsNotFound()
        {
            var (_, result) = await InvokeControllerAsync(c => c.GetSkillAsync("some-skill"));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsNotFoundIfSkillIsDeleted()
        {
            var skill = await Env.CreateSkillAsync("some-skill");
            skill.IsDeleted = true;
            await Env.Db.SaveChangesAsync();

            var (_, result) = await InvokeControllerAsync(c => c.GetSkillAsync("some-skill"));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsSkillInformation()
        {
            await Env.CreateSkillAsync("some-skill",
                codeText: "// The code",
                cacheKey: "blargle",
                language: CodeLanguage.JavaScript);

            var (_, result) = await InvokeControllerAsync(c => c.GetSkillAsync("some-skill"));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillGetResponse>(objectResult.Value);
            Assert.Equal("// The code", response.Code);
            Assert.Equal("13luWJLyPV4aBkIdnhwXfLtzAS4=", response.CodeHash);
            Assert.Equal(CodeLanguage.JavaScript, response.Language);
            Assert.True(response.Enabled);
        }
    }

    public class ThePublishSkillAsyncMethod : CliControllerTestBase
    {
        [Fact]
        public async Task WithCodeChangesButNoPermissionToEditItReturnsForbiddenResult()
        {
            await Env.CreateSkillAsync("some-skill",
                codeText: "// The code",
                cacheKey: "13luWJLyPV4aBkIdnhwXfLtzAS4=",
                language: CodeLanguage.JavaScript,
                restricted: true);

            var updateRequest = new SkillUpdateRequest
            {
                PreviousCodeHash = "13luWJLyPV4aBkIdnhwXfLtzAS4=",
                Code = "// The new code"
            };

            var (_, result) = await InvokeControllerAsync(c => c.UpdateSkillAsync("some-skill", updateRequest));

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task WithNoCodeChangesAndPermissionReturnsSkillNotUpdated()
        {
            var admin = await Env.CreateAdminMemberAsync();
            var skill = await Env.CreateSkillAsync("some-skill",
                codeText: "// The code",
                language: CodeLanguage.JavaScript,
                restricted: true);

            await Env.Permissions.SetPermissionAsync(Env.TestData.Member, skill, Capability.Edit, admin);

            var (_, result) = await InvokeControllerAsync(c => c.UpdateSkillAsync("some-skill",
                new SkillUpdateRequest
                {
                    PreviousCodeHash = "13luWJLyPV4aBkIdnhwXfLtzAS4=",
                    Code = "// The code"
                }));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillUpdateResponse>(objectResult.Value);
            Assert.False(response.Updated);
            Assert.Equal("13luWJLyPV4aBkIdnhwXfLtzAS4=", response.NewCodeHash);
        }

        [Fact]
        public async Task WithCodeChangesAndPermissionButSkillHasChangedReturnsConflict()
        {
            var admin = await Env.CreateAdminMemberAsync();
            var skill = await Env.CreateSkillAsync("some-skill",
                codeText: "// The code",
                language: CodeLanguage.JavaScript,
                restricted: true);

            await Env.Permissions.SetPermissionAsync(Env.TestData.Member, skill, Capability.Edit, admin);

            var (_, result) = await InvokeControllerAsync(c => c.UpdateSkillAsync("some-skill",
                new SkillUpdateRequest
                {
                    PreviousCodeHash = "AnOutdatedCacheKey",
                    Code = "// The new code"
                }));

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            var conflict = Assert.IsType<ConflictInfo>(problemDetails.Extensions["Conflict"]);
            Assert.Equal(Env.TestData.User.PlatformUserId, conflict.ModifiedBy.PlatformUserId);
        }

        [Fact]
        public async Task WithCodeChangesAndPermissionUpdatesSkill()
        {
            var admin = await Env.CreateAdminMemberAsync();
            var skill = await Env.CreateSkillAsync("some-skill",
                codeText: "// The code",
                language: CodeLanguage.JavaScript,
                restricted: true);

            await Env.Permissions.SetPermissionAsync(Env.TestData.Member, skill, Capability.Edit, admin);

            var (_, result) = await InvokeControllerAsync(c => c.UpdateSkillAsync("some-skill",
                new SkillUpdateRequest
                {
                    PreviousCodeHash = "13luWJLyPV4aBkIdnhwXfLtzAS4=",
                    Code = "// The new code"
                }));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillUpdateResponse>(objectResult.Value);
            Assert.True(response.Updated);
            Assert.Equal("H9RVuOdCb3y8jI3GABPYjAWrPqo=", response.NewCodeHash);
        }

        [Fact]
        public async Task WithCodeChangesAndNoPermissionUpdatesUnrestrictedSkill()
        {
            await Env.CreateSkillAsync("some-skill",
                codeText: "// The code",
                language: CodeLanguage.JavaScript,
                restricted: false);

            var (_, result) = await InvokeControllerAsync(c => c.UpdateSkillAsync("some-skill",
                new SkillUpdateRequest
                {
                    PreviousCodeHash = "13luWJLyPV4aBkIdnhwXfLtzAS4=",
                    Code = "// The new code"
                }));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillUpdateResponse>(objectResult.Value);
            Assert.True(response.Updated);
            Assert.Equal("H9RVuOdCb3y8jI3GABPYjAWrPqo=", response.NewCodeHash);
        }
    }

    public class TheRunSkillAsyncMethod : CliControllerTestBase
    {
        [Fact]
        public async Task CompilesCodeIfNotInCache()
        {
            var skill = await Env.CreateSkillAsync("some-skill", codeText: "whatevs", restricted: false);
            Env.SkillRunnerClient.PushResponse(new SkillRunResponse
            {
                Success = true,
                Replies = new List<string>
                {
                    "Got your message loud and clear!"
                },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null,
            });

            Env.CachingCompilerService.AddCompilationResult(
                new FakeOrganizationIdentifier(skill.Organization),
                "// code",
                new FakeSkillCompilationResult("// code"));

            var (_, result) = await InvokeControllerAsync(c => c.RunSkillAsync(skill.Name,
                new SkillRunRequest
                {
                    Name = "some-skill",
                    Arguments = "args",
                    Code = "// code"
                }));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var runResponse = Assert.IsType<SkillRunResponse>(objectResult.Value);
            Assert.NotNull(runResponse.Replies);
            var message = Assert.Single(runResponse.Replies);
            Assert.Equal("Got your message loud and clear!", message);
            Assert.True(Env.CachingCompilerService.CompileAsyncCalled);
        }
    }

    public class TheListSkillsAsyncMethod : CliControllerTestBase
    {
        [Fact]
        public async Task ReturnsSetOfSkillsWithDefaultOrdering()
        {
            await Env.CreateSkillAsync("some-skill", language: CodeLanguage.Python);
            await Env.CreateSkillAsync("another-skill", language: CodeLanguage.JavaScript);
            await Env.CreateSkillAsync("zee-skill", language: CodeLanguage.CSharp);

            var (_, result) = await InvokeControllerAsync(c => c.ListSkillsAsync());

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillListResponse>(objectResult.Value);
            Assert.Equal(OrderDirection.Ascending, response.OrderDirection);
            Assert.Equal(SkillOrderBy.Name, response.OrderBy);
            Assert.Equal(3, response.Results.Count);
            Assert.Equal("another-skill", response.Results[0].Name);
            Assert.Equal("some-skill", response.Results[1].Name);
            Assert.Equal("zee-skill", response.Results[2].Name);
        }

        [Fact]
        public async Task ReturnsSetOfSkillsWithSpecifiedOrdering()
        {
            var oldestSkill = await Env.CreateSkillAsync("another-skill", language: CodeLanguage.JavaScript);
            oldestSkill.Created = DateTime.UtcNow.AddDays(-10);
            var middleSkill = await Env.CreateSkillAsync("zee-skill", language: CodeLanguage.CSharp);
            middleSkill.Created = DateTime.UtcNow.AddDays(-5);
            await Env.CreateSkillAsync("some-skill", language: CodeLanguage.Python);

            var (_, result) = await InvokeControllerAsync(c => c.ListSkillsAsync(SkillOrderBy.Created, OrderDirection.Descending));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillListResponse>(objectResult.Value);
            Assert.Equal(OrderDirection.Descending, response.OrderDirection);
            Assert.Equal(SkillOrderBy.Created, response.OrderBy);
            Assert.Equal(3, response.Results.Count);
            Assert.Equal("some-skill", response.Results[0].Name);
            Assert.Equal("zee-skill", response.Results[1].Name);
            Assert.Equal("another-skill", response.Results[2].Name);
        }
    }
}
