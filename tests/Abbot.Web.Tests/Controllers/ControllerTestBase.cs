using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serious.Abbot.Controllers;
using Serious.TestHelpers;
using Xunit;

public abstract class ControllerTestBase<TController> : ControllerTestBase<TController, CommonTestData>
    where TController : ControllerBase
{
}

public abstract class ControllerTestBase<TController, TTestData> : ActionTestBase<TTestData>
    where TController : ControllerBase
    where TTestData : CommonTestData
{
    ControllerContext? _controllerContext;

    public ControllerContext ControllerContext => _controllerContext ??= new FakeControllerContext(ActionContext);

    protected ControllerTestBase()
    {
    }

    [Fact]
    public void HasExpectedArea()
    {
        var area = typeof(TController).GetCustomAttributes(true).OfType<AreaAttribute>().FirstOrDefault();
        Assert.Equal(ExpectedArea, area?.RouteValue);
    }

    protected virtual string? ExpectedArea => null;

    /// <summary>
    /// Authenticates as <see cref="TestMember"/> if <typeparamref name="TController"/> inherits from <see cref="InternalApiControllerBase"/>.
    /// </summary>
    protected override void ApplyDefaultAuthentication()
    {
        // Don't overwrite existing auth
        if (ActionContext.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        if (typeof(UserControllerBase).IsAssignableFrom(typeof(TController)))
        {
            AuthenticateAs(TestMember);
        }
    }

    protected async Task<(TController, IActionResult?)> InvokeControllerAsync(Action<TController> controllerAction) =>
        await InvokeControllerAsync(p => {
            controllerAction(p);
            return Task.FromResult<IActionResult>(null!);
        });

    protected async Task<(TController, IActionResult?)> InvokeControllerAsync(Func<TController, Task> controllerAction) =>
        await InvokeControllerAsync(async p => { await controllerAction(p); return null!; });

    protected async Task<(TController, T)> InvokeControllerAsync<T>(Func<TController, IActionResult> controllerAction) where T : IActionResult =>
        ValidateResult<T>(await InvokeControllerAsync(controllerAction));

    protected async Task<(TController, IActionResult?)> InvokeControllerAsync(Func<TController, IActionResult> controllerAction) =>
        await InvokeControllerAsync(p => Task.FromResult(controllerAction(p)));

    protected async Task<(TController, T)> InvokeControllerAsync<T>(Func<TController, Task<IActionResult>> controllerAction) where T : IActionResult =>
        ValidateResult<T>(await InvokeControllerAsync(controllerAction));

    protected async Task<(TController, IActionResult?)> InvokeControllerAsync(Func<TController, Task<IActionResult>> controllerAction)
    {
        // Create the page
        var controller = Env.Activate<TController>(controllerContext: ControllerContext);

        ApplyDefaultAuthentication();

        // Run filters
        var context = new ActionExecutingContext(
            ActionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller);

        var executedContext = new ActionExecutedContext(ActionContext, new List<IFilterMetadata>(), controller);

        if (controller is not Controller mvcController)
        {
            return (controller, await controllerAction(controller));
        }

        // OnActionExecutionAsync will, by default, run the other OnActionNNN overloads.
        await mvcController.OnActionExecutionAsync(context,
            async () => {
                if (context.Result is null)
                {
                    context.Result = await controllerAction(controller);
                }

                return executedContext;
            });

        return (controller, context.Result);
    }

    (TController, T) ValidateResult<T>((TController, IActionResult?) original) where T : IActionResult
    {
        var (page, result) = original;
        Assert.NotNull(result);
        var tResult = Assert.IsType<T>(result);
        return (page, tResult);
    }
}
