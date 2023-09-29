using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages;
using Serious.TestHelpers;
using Xunit;

public abstract class PageTestBase<TPage> : PageTestBase<TPage, PageTestBase<TPage>.TestData>
    where TPage : PageModel
{
    public new class TestData : PageTestBase<TPage, TestData>.TestData { }
}

public abstract class PageTestBase<TPage, TTestData> : ActionTestBase<TTestData>
    where TPage : PageModel
    where TTestData : PageTestBase<TPage, TTestData>.TestData
{

    public HandlerMethodDescriptor HandlerMethodDescriptor { get; set; } = new();

    public class TestData : CommonTestData
    {
        public Room TestRoom { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            TestRoom = await env.Rooms.CreateAsync(new Room
            {
                Name = "test-room",
                OrganizationId = Organization.Id,
                PlatformRoomId = "C0001",
            });
        }
    }

    PageContext? _pageContext;

    public PageContext PageContext => _pageContext ??= new FakePageContext(ActionContext)
    {
        ViewData = new ViewDataDictionary(
            ActionContext.HttpContext.RequestServices.GetRequiredService<IModelMetadataProvider>(),
            new ModelStateDictionary()),
    };

    protected PageTestBase()
    {
    }

    public Room TestRoom => Env.TestData.TestRoom;

    /// <summary>
    /// Authenticates as <see cref="TestMember"/> if <typeparamref name="TPage"/> inherits from <see cref="UserPage"/>.
    /// </summary>
    protected override void ApplyDefaultAuthentication()
    {
        if (!HasAuthenticatedManually && typeof(UserPage).IsAssignableFrom(typeof(TPage)))
        {
            AuthenticateAs(TestMember);
        }
    }

    public void SetHandlerMethod(Expression<Action<TPage>> expression)
    {
        var lambda = expression.Require<LambdaExpression>();
        var call = lambda.Body.Require<MethodCallExpression>();
        var method = call.Method;
        HandlerMethodDescriptor.MethodInfo = method;
    }

    protected async Task<(TPage, IActionResult?)> InvokePageAsync(Action<TPage> pageAction) =>
        await InvokePageAsync(p => {
            pageAction(p);
            return Task.FromResult<IActionResult>(null!);
        });

    protected async Task<(TPage, IActionResult?)> InvokePageAsync(Func<TPage, Task> pageAction) =>
        await InvokePageAsync(async p => { await pageAction(p); return null!; });

    protected async Task<(TPage, T)> InvokePageAsync<T>(Func<TPage, IActionResult> pageAction) where T : IActionResult =>
        ValidateResult<T>(await InvokePageAsync(pageAction));

    protected async Task<(TPage, IActionResult?)> InvokePageAsync(Func<TPage, IActionResult> pageAction) =>
        await InvokePageAsync(p => Task.FromResult(pageAction(p)));

    protected async Task<(TPage, T)> InvokePageAsync<T>(Func<TPage, Task<IActionResult>> pageAction, bool acceptsTurbo = false) where T : IActionResult =>
        ValidateResult<T>(await InvokePageAsync(pageAction, acceptsTurbo));

    protected async Task<(TPage, IActionResult?)> InvokePageAsync(Func<TPage, Task<IActionResult>> pageAction, bool acceptsTurbo = false)
    {
        // Create the page
        var page = Env.Activate<TPage>(pageContext: PageContext);

        ApplyDefaultAuthentication();

        // Run filters
        var context = new PageHandlerExecutingContext(
            PageContext,
            new List<IFilterMetadata>(),
            HandlerMethodDescriptor,
            new Dictionary<string, object?>(),
            page);

        var executedContext = new PageHandlerExecutedContext(
            PageContext,
            new List<IFilterMetadata>(),
            HandlerMethodDescriptor,
            page);

        await page.OnPageHandlerExecutionAsync(
            context,
            async () => {
                if (acceptsTurbo)
                {
                    context.HttpContext.Request.Headers.Accept = "text/vnd.turbo-stream.html";
                }
                context.Result ??= await pageAction(page);

                return executedContext;
            });

        return (page, context.Result ?? new PageResult());
    }

    (TPage, T) ValidateResult<T>((TPage, IActionResult?) original) where T : IActionResult
    {
        var (page, result) = original;
        Assert.NotNull(result);
        var tResult = Assert.IsType<T>(result);
        return (page, tResult);
    }
}
