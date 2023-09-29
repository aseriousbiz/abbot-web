using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Serious.Abbot.Controllers;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Extensions;
using Serious.Abbot.Scripting;
using Serious.Abbot.Validation;
using Serious.TestHelpers;
using Xunit;

public class PatternValidationControllerTests
{
    public class TheValidateNameAsyncMethod
    {
        [Fact]
        public async Task ReturnsJsonTrueWhenNameUnique()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IPatternValidator>(out var validator)
                .Build();
            validator.IsUniqueNameAsync("test", 123, "some-skill", env.TestData.Organization)
                .Returns(true);

            var controller = env.Activate<PatternsController>();
            var httpContext = new FakeHttpContext();
            httpContext.SetCurrentMember(env.TestData.Member);
            controller.ControllerContext = new FakeControllerContext(httpContext);

            var result = await controller.ValidateNameAsync("test", 123, "some-skill") as JsonResult;

            Assert.NotNull(result?.Value);
            Assert.True((bool)result.Value);
        }

        [Fact]
        public async Task ReturnsJsonMessageWhenNameConflict()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IPatternValidator>(out var validator)
                .Build();
            validator.IsUniqueNameAsync("test", 123, "the-skill", env.TestData.Organization)
                .Returns(false);
            var controller = env.Activate<PatternsController>();
            var httpContext = new FakeHttpContext();
            httpContext.SetCurrentMember(env.TestData.Member);
            controller.ControllerContext = new FakeControllerContext(httpContext);

            var result = await controller.ValidateNameAsync("test", 123, "the-skill") as JsonResult;

            Assert.NotNull(result?.Value);
            Assert.Equal("The name \"test\" conflicts with another pattern for this skill with the same name.", (string)result.Value);
        }
    }

    public class TheValidatePatternMethod
    {
        [Fact]
        public void ReturnsErrorMessageForInvalidPattern()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IPatternValidator>(out var validator)
                .Build();
            validator.IsValidPattern("test", PatternType.RegularExpression)
                .Returns(false);
            var controller = env.Activate<PatternsController>();
            var httpContext = new FakeHttpContext();
            httpContext.SetCurrentMember(env.TestData.Member);
            controller.ControllerContext = new FakeControllerContext(httpContext);

            var result = controller.ValidatePattern("test", PatternType.RegularExpression) as JsonResult;

            Assert.NotNull(result?.Value);
            Assert.Equal("The pattern <code>test</code> is not a valid regular expression.", (string)result.Value);
        }

        [Fact]
        public void ReturnsTrueForValidPattern()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IPatternValidator>(out var validator)
                .Build();
            validator.IsValidPattern("test", PatternType.RegularExpression)
                .Returns(true);
            var controller = env.Activate<PatternsController>();
            var httpContext = new FakeHttpContext();
            httpContext.SetCurrentMember(env.TestData.Member);
            controller.ControllerContext = new FakeControllerContext(httpContext);

            var result = controller.ValidatePattern("test", PatternType.RegularExpression) as JsonResult;

            Assert.NotNull(result?.Value);
            Assert.True((bool)result.Value);
        }
    }
}
