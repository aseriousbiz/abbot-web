using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Extensions;
using Serious.Abbot.Onboarding;
using Serious.Abbot.Security;
using Serious.Logging;

namespace Serious.Abbot.Filters;

public class OrganizationStateFilter : IAsyncPageFilter
{
    readonly OnboardingService _onboardingService;
    static readonly ILogger<OrganizationStateFilter> Log =
        ApplicationLoggerFactory.CreateLogger<OrganizationStateFilter>();

    public const string RegistrationPage = "/Account/Register/Index";
    public const string OverviewPage = "/Index";
    const string OrganizationDisabledPage = "/Status/Disabled";

    public OrganizationStateFilter(OnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) =>
        Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (!context.RouteData.Values.TryGetValue("page", out var pagePath)
            || pagePath is not string page)
        {
            await next();
            return;
        }

        RedirectToPageResult? RequirePage(string targetPage)
        {
            return !string.Equals(targetPage, page, StringComparison.OrdinalIgnoreCase)
                ? new RedirectToPageResult(targetPage)
                : null;
        }

        var httpContext = context.HttpContext;

        bool allowsAnonymous = context.ActionDescriptor.EndpointMetadata
            .Any(em => em is AllowAnonymousAttribute);
        if (allowsAnonymous && httpContext.GetCurrentMember() is null)
        {
            // Allow anonymous pages to proceed without filtering them on organization state.
            await next();
            return;
        }

        // Handle the organization disabled case.
        var member = httpContext.GetCurrentMember();
        if (member is null)
        {
            Log.AnonymousUserToNonAnonymousPage(page);
            context.Result = new NotFoundResult();
            return;
        }
        var organization = member.Organization;
        if (organization is { Enabled: false } && RequirePage(OrganizationDisabledPage) is { } r1)
        {
            Log.Redirecting(OrganizationDisabledPage, "OrganizationDisabled");
            context.Result = r1;
            return;
        }

        // If the user has to be approved, they can only go to the registration page.
        var registrationStatus = httpContext.User.GetRegistrationStatusClaim();
        if (registrationStatus is RegistrationStatus.ApprovalRequired && RequirePage(RegistrationPage) is { } r2)
        {
            Log.Redirecting(RegistrationPage, "UserRequiresApproval");
            context.Result = r2;
            return;
        }

        // If the organization is any of the below, allow them to proceed:
        // * Fully onboarded.
        // * A legacy organization activated prior to the onboarding process being introduced.
        // * Requesting an "onboarding" page.
        if (!organization.IsOnboarding() ||
            page.StartsWith("/Onboarding/", StringComparison.Ordinal))
        {
            // They may proceed.
            await next();
            return;
        }

        // Send the user to onboarding.
        if (await _onboardingService.UpdateOnboardingStateAsync(organization, member) is { } result)
        {
            Log.Redirecting(result is RedirectToPageResult rpResult ? rpResult.PageName : null, "OnboardingRequired");
            context.Result = result;
            return;
        }

        // Nothing to do for onboarding, on they go.
        await next();
    }
}

public static partial class OrganizationStateFilterLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Redirecting to {Page} because {Reason}.")]
    public static partial void Redirecting(this ILogger<OrganizationStateFilter> logger, string? page, string reason);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Anonymous user attempting to access non-anonymous page {Page}.")]
    public static partial void AnonymousUserToNonAnonymousPage(this ILogger<OrganizationStateFilter> logger, string page);
}
