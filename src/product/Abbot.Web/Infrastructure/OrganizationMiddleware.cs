using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Security;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure;

/// <summary>
/// Asp.NET Middleware to set the current organization for the current <see cref="HttpContext"/> so we don't
/// have to query this multiple times a request.
/// </summary>
public class OrganizationMiddleware
{
    static readonly ILogger<OrganizationMiddleware> Log =
        ApplicationLoggerFactory.CreateLogger<OrganizationMiddleware>();

    readonly RequestDelegate _next;

    public OrganizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext httpContext, IBackgroundSlackClient backgroundSlackClient)
    {
        IDisposable? scope = null;
        var principal = httpContext.User;
        if (principal.IsAuthenticated())
        {
            var member = httpContext.GetCurrentMember();
            if (member is { Organization: { } organization })
            {
                if (organization.Scopes is null)
                {
                    backgroundSlackClient.EnqueueUpdateOrganizationScopes(organization);
                }

                httpContext.SetFeatureActor(new WebFeatureActor(organization, principal.GetPlatformUserId()));

                // OrgId is all we really need, but when reading logs, it's nice to have the organization name etc.
                scope = Disposable.Combine(
                    Log.BeginOrganizationScope(organization),
                    Log.BeginMemberScope(member));
            }
        }

        using (scope)
        {
            await _next(httpContext);
        }
    }
}

/// <summary>
/// A <see cref="IFeatureActor"/> representing the current web request.
/// </summary>
public class WebFeatureActor : IFeatureActor
{
    readonly TargetingContext _context;

    public WebFeatureActor(Organization organization, string? userId)
    {
        // If we want, we can include request-specific targeting context here (IP addresses, etc.).
        _context = FeatureHelper.CreateTargetingContext(organization, userId);
    }

    public TargetingContext GetTargetingContext() => _context;
}
