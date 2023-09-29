using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

public class SignalsValidationControllerTests : ControllerTestBase<SignalValidationController>
{
    protected override string? ExpectedArea => InternalApiControllerBase.Area;

    public class TheValidateNameAsyncMethod : SignalsValidationControllerTests
    {
        [Fact]
        public async Task ReturnsJsonTrueWhenNameUnique()
        {
            Builder.Substitute<ISignalRepository>(out var repository);
            repository.GetAsync("test", "some-skill", Args.Organization)
                .Returns(Task.FromResult((SignalSubscription?)null));

            var (_, result) = await InvokeControllerAsync(c => c.ValidateNameAsync("test", "some-skill"));

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(true, jsonResult.Value);
        }

        [Fact]
        public async Task ReturnsJsonMessageWhenNameConflict()
        {
            Builder.Substitute<ISignalRepository>(out var repository);
            repository.GetAsync("test", "some-skill", Args.Organization)!
                .Returns(Task.FromResult(new SignalSubscription { Id = 42 }));

            var (_, result) = await InvokeControllerAsync(c => c.ValidateNameAsync("test", "some-skill"));

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("This skill is already subscribed to the signal \"test\".", jsonResult.Value);
        }
    }
}
