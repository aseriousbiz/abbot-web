using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Pages;

namespace Abbot.Web.Tests.Pages;

public class StaffViewablePageTests : PageTestBase<StaffViewablePageTests.TestPage>
{
    [Fact]
    public async Task OrganizationIsViewersOrgIfStaffOverrideRouteValueNotSet()
    {
        var staffMember = await Env.CreateMemberAsync();
        await Env.MakeMemberStaffAsync(staffMember);
        AuthenticateAs(staffMember);

        var (page, result) = await InvokePageAsync(p => p.Unmarked());

        Assert.Equal(Env.TestData.Organization, page.Organization);
        Assert.Equal(Env.TestData.Organization, page.Viewer.Organization);
        Assert.Equal(staffMember, page.Viewer);
        Assert.Same(result, TestPage.ExpectedResult);
        Assert.False(HttpContext.InStaffTools());
        Assert.Equal(Env.TestData.Organization, PageContext.ViewData.GetOrganization());
    }

    [Fact]
    public async Task ReturnsNotFoundIfStaffOverrideSetButViewerIsNotStaff()
    {
        // Default auth is not staff
        var subjectOrg = Env.CreateOrganizationAsync();

        RouteData.Values.Add("staffOrganizationId", subjectOrg.Id.ToString());

        var (page, result) = await InvokePageAsync(p => p.Unmarked());

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(page.ActionCalled);
    }

    [Theory]
    [InlineData("DELETE", false)]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("OPTIONS", true)]
    [InlineData("PATCH", false)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("TRACE", true)]
    public async Task HandlesUnmarkedHandlerMethodCorrectly(string method, bool allowed)
    {
        SetHandlerMethod(p => p.Unmarked());
        await RunHandlerAllowedTestAsync(method, allowed);
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("PATCH")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("TRACE")]
    public async Task HandlesForbiddenHandlerMethodCorrectly(string method)
    {
        SetHandlerMethod(p => p.Forbidden());
        await RunHandlerAllowedTestAsync(method, false);
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("PATCH")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("TRACE")]
    public async Task HandlesAllowedHandlerMethodCorrectly(string method)
    {
        SetHandlerMethod(p => p.Allowed());
        await RunHandlerAllowedTestAsync(method, true);
    }

    [Fact]
    public async Task ReturnsNotFoundIfOverrideOrganizationIdNotFound()
    {
        // Default auth is not staff
        var staffMember = await Env.CreateMemberAsync();
        await Env.MakeMemberStaffAsync(staffMember);
        AuthenticateAs(staffMember);
        await Env.CreateOrganizationAsync();

        RouteData.Values.Add("staffOrganizationId", "TNOTREAL");
        HttpContext.Request.Method = HttpMethods.Get;
        SetHandlerMethod(p => p.Unmarked());

        var (page, result) = await InvokePageAsync(p => p.Unmarked());

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(page.ActionCalled);
    }

    async Task RunHandlerAllowedTestAsync(string method, bool allowed)
    {
        // Default auth is not staff
        var staffMember = await Env.CreateMemberAsync();
        await Env.MakeMemberStaffAsync(staffMember);
        AuthenticateAs(staffMember);
        var subjectOrg = await Env.CreateOrganizationAsync();

        RouteData.Values.Add("staffOrganizationId", subjectOrg.PlatformId);
        HttpContext.Request.Method = method;

        if (allowed)
        {
            var (page, result) = await InvokePageAsync(p => p.Unmarked());
            Assert.Same(TestPage.ExpectedResult, result);
            Assert.Equal("Unmarked", page.ActionCalled);
            Assert.True(HttpContext.InStaffTools());
            Assert.Equal(staffMember, page.Viewer);
            Assert.Equal(subjectOrg, page.Organization);
            Assert.Equal(subjectOrg, PageContext.ViewData.GetOrganization());
        }
        else
        {
            var ex = await Assert.ThrowsAsync<UnreachableException>(() => InvokePageAsync(p => p.Unmarked()));
            Assert.Equal(ex.Message, "You can't perform this action as staff");
        }
    }

    public class TestPage : StaffViewablePage
    {
        public static readonly IActionResult ExpectedResult = new ObjectResult(new object());

        public string? ActionCalled { get; set; }

        [AllowStaff]
        public Task<IActionResult> Allowed()
        {
            ActionCalled = nameof(Allowed);
            return Task.FromResult(ExpectedResult);
        }

        [ForbidStaff]
        public Task<IActionResult> Forbidden()
        {
            ActionCalled = nameof(Forbidden);
            return Task.FromResult(ExpectedResult);
        }

        public Task<IActionResult> Unmarked()
        {
            ActionCalled = nameof(Unmarked);
            return Task.FromResult(ExpectedResult);
        }
    }
}
