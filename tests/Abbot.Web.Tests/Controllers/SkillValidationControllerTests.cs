using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Entities;
using Serious.Abbot.Validation;
using Xunit;

public class SkillValidationControllerTests : ControllerTestBase<SkillValidationController>
{
    protected override string? ExpectedArea => InternalApiControllerBase.Area;

    public class TheValidateAsyncMethod : SkillValidationControllerTests
    {
        [Fact]
        public async Task ReturnsJsonTrueWhenNameUnique()
        {
            Builder.Substitute<ISkillNameValidator>(out var validator);
            validator.IsUniqueNameAsync("test", 123, nameof(Skill), Env.TestData.Organization)
                .Returns(UniqueNameResult.Unique);

            var (_, result) = await InvokeControllerAsync(c => c.ValidateAsync("test", 123, nameof(Skill)));

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(true, jsonResult.Value);
        }

        [Fact]
        public async Task ReturnsJsonMessageWhenNameConflict()
        {
            Builder.Substitute<ISkillNameValidator>(out var validator);
            validator.IsUniqueNameAsync("test", 123, nameof(Skill), Env.TestData.Organization)
                .Returns(UniqueNameResult.Conflict(nameof(UserList)));

            var (_, result) = await InvokeControllerAsync(c => c.ValidateAsync("test", 123, nameof(Skill)));

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("The name \"test\" conflicts with a list with the same name.", jsonResult.Value);
        }

        [Fact]
        public async Task ReturnsJsonMessageWhenNameConflictWithReservedKeyword()
        {
            Builder.Substitute<ISkillNameValidator>(out var validator);
            validator.IsUniqueNameAsync("install", 123, nameof(Skill), Env.TestData.Organization)
                .Returns(UniqueNameResult.Conflict(UniqueNameResult.ReservedKeywordConflict));

            var (_, result) = await InvokeControllerAsync(c => c.ValidateAsync("install", 123, nameof(Skill)));

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("The name \"install\" is reserved.", jsonResult.Value);
        }
    }
}
