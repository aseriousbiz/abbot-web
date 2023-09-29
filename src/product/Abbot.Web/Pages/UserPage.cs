using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;

namespace Serious.Abbot.Pages;

/// <summary>
/// Base class for a page that is intended to be viewed by the active user.
/// </summary>
public abstract class UserPage : AuthenticatedPage
{
    public virtual Organization Organization => Viewer.Organization;

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        await base.OnPageHandlerExecutionAsync(context,
            async () => {
                ViewData.SetOrganization(Organization);
                return await next();
            });
    }
}

/// <summary>
/// Base class for a page that can be accessed by a staff member OR an active user.
/// </summary>
public class StaffViewablePage : UserPage
{
    Organization? _overrideOrganization;

    public override Organization Organization => _overrideOrganization ?? base.Organization;

    public override string? StaffPageUrl() => InStaffTools
        ? null
        : Url.Page(null, new { staffOrganizationId = Organization.PlatformId });

    public bool InStaffTools => HttpContext.InStaffTools();

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        await base.OnPageHandlerExecutionAsync(context,
            async () => {
                if (context.RouteData.Values.TryGetValue("staffOrganizationId", out var v)
                    && v is string organizationId)
                {
                    // This route value should only be set when accessed by a staff member!
                    if (!Viewer.IsStaff())
                    {
                        context.Result = NotFound();
                        return null!;
                    }

                    // Does the handler allow staff?
                    // The rules for this are:
                    // * Any method is allowed if AllowStaffAttribute is present
                    // * A GET is allowed unless ForbidStaffAttribute is present
                    // * Otherwise, it is not allowed
                    // Specifically, this means non-GETs require AllowStaffAttribute to be present.
                    var handlerMethod = context.HandlerMethod.Require();
                    var allowed =
                        handlerMethod.MethodInfo.GetCustomAttribute<AllowStaffAttribute>() is not null ||
                        (
                            IsGenerallyAcceptedAsSafe(context.HttpContext.Request.Method) &&
                            handlerMethod.MethodInfo.GetCustomAttribute<ForbidStaffAttribute>() is null
                        );
                    if (!allowed)
                    {
                        // It does not. Staff should know better, or rather the app should be preventing this :).
                        throw new UnreachableException("You can't perform this action as staff");
                    }

                    var organizationRepository =
                        context.HttpContext.RequestServices.GetRequiredService<IOrganizationRepository>();

                    var org = await organizationRepository.GetAsync(organizationId);
                    if (org is null)
                    {
                        context.Result = NotFound();
                        return null!;
                    }

                    _overrideOrganization = org;
                    ViewData.SetOrganization(org);
                    context.HttpContext.SetInStaffTools(true);
                }

                return await next();
            });
    }

    // I'll be darned if I ever write a method that declares something "Safe" without some kind of condition :P -Ashley
    static bool IsGenerallyAcceptedAsSafe(string httpMethod)
    {
        // https://developer.mozilla.org/en-US/docs/Glossary/Safe/HTTP
        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Methods/TRACE
        return HttpMethods.IsGet(httpMethod) ||
               HttpMethods.IsHead(httpMethod) ||
               HttpMethods.IsTrace(httpMethod) ||
               HttpMethods.IsOptions(httpMethod);
    }
}

/// <summary>
/// Base class for a page that is intended to only be viewed by a staff member.
/// </summary>
public abstract class StaffToolsPage : AuthenticatedPage
{
    public override async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        // Put a marker in the HttpContext so we know we're in the staff tools
        PageContext.HttpContext.SetInStaffTools(true);

        await base.OnPageHandlerExecutionAsync(context,
            async () => {
                // In AuthorizationPolicies.ConfigureRazorPagesAuthorization we apply the RequireStaffRole to the
                // /Staff directory. All staff pages should be in this directory. But JUUUUUUST IN CASE, we have
                // this extra check is here.
                if (!Viewer.IsStaff())
                {
                    context.Result = NotFound();
                    return null!;
                }

                return await next();
            });
    }
}

public abstract class AuthenticatedPage : AbbotPageModelBase
{
    /// <summary>
    /// The currently logged-in <see cref="Viewer"/>
    /// </summary>
    public Member Viewer { get; private set; } = null!;

    public override async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        await base.OnPageHandlerExecutionAsync(context,
            async () => {
                if (!User.IsAuthenticated())
                {
                    context.Result = NotFound();
                    return null!;
                }

                Viewer = context.HttpContext.RequireCurrentMember();
                ViewData.SetViewer(Viewer);

                return await next();
            });
    }
}
