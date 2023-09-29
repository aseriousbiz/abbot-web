using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Pages.Account.Register;
using Serious.Abbot.Security;
using Xunit;

public class RegisterPageTests
{
    public class TheOnGetAsyncMethod
    {
        [Theory]
        [InlineData(RegistrationStatus.ApprovalRequired, true)]
        [InlineData(RegistrationStatus.Ok, false)]
        public async Task DoesNotRequireRegistration(RegistrationStatus registrationStatus, bool expected)
        {
            var env = TestEnvironment.Create();
            var page = env.Activate<IndexPage>(registrationStatus);

            await page.OnGetAsync();

            Assert.Equal(expected, page.RequiresUserRegistration);
        }
    }

    public class TheOnPostAsyncMethod
    {
        [Fact]
        public async Task RequestsAccessForUserAndSendsEmailToAdminsAndRedirects()
        {
            var env = TestEnvironment.Create();
            env.TestData.User.NameIdentifier = $"oauth2|slack|{env.TestData.User.PlatformUserId}";
            Assert.NotNull(env.TestData.User.NameIdentifier);
            var page = env.Activate<IndexPage>();

            var result = await page.OnPostAsync();

            var redirectToPageResult = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("/Status/AccessDenied", redirectToPageResult.PageName);
            var user = await env.Users.GetCurrentMemberAsync(page.User);
            Assert.NotNull(user);
            Assert.False(page.User.GetRegistrationStatusClaim() is RegistrationStatus.ApprovalRequired);
            Assert.NotNull(user.AccessRequestDate);
            Assert.DoesNotContain(user.MemberRoles, ur => ur.Role.Name == Roles.Administrator);
            Assert.False(page.User.IsInRole(Roles.Administrator));
            Assert.True(env.Authentication.SignInAsyncCalled);
            Assert.True(env.BackgroundSlackClient.EnqueueDirectMessagesCalled);
        }
    }
}
