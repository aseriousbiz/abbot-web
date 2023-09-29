using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Serious.Abbot.Pages;
using Serious.Abbot.Repositories;
using Xunit;

public class UserPageTests : PageTestBase<UserPageTests.TestPage>
{
    protected override void ApplyDefaultAuthentication()
    {
        // Disable default auth so we can test it
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_ProducesNotFoundResultIfUserNotAuthenticated()
    {
        var (page, result) = await InvokePageAsync(p => p.OnGetAsync());

        Assert.True(page.FilterInvoked);
        Assert.False(page.ActionInvoked);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPageHandlerExecutionAsync_SetsUpCurrentUserAndDerivedPropertiesIfAuthenticated()
    {
        AuthenticateAs(TestMember);
        var (page, result) = await InvokePageAsync(p => p.OnGetAsync());

        Assert.True(page.FilterInvoked);
        Assert.True(page.ActionInvoked);
        Assert.Same(TestPage.ExpectedResult, result);
        Assert.Same(page.Viewer, TestMember);
        Assert.Same(page.Organization, TestOrganization);
    }

    public class TestPage : UserPage
    {
        public static readonly IActionResult ExpectedResult = new ObjectResult(new object());

        public bool ActionInvoked { get; private set; }
        public bool FilterInvoked { get; private set; }

        public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            FilterInvoked = true;
            await base.OnPageHandlerExecutionAsync(context, next);
        }

        public Task<IActionResult> OnGetAsync()
        {
            ActionInvoked = true;
            return Task.FromResult(ExpectedResult);
        }
    }
}
