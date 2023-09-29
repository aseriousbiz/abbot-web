using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serious.Abbot.Security;

namespace Microsoft.AspNetCore.Http;

public class StaffModeRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// A list of paths that allow users who ARE staff, but have staff mode disabled.
    /// </summary>
    public IList<string> AllowedPathsWhenDisabled { get; }

    public bool AllowedInDevelopmentEnvironment { get; init; }

    /// <summary>
    /// Constructs the <see cref="StaffModeRequirement"/> with the specified list of paths to allow even when staff mode is disabled for a session.
    /// </summary>
    /// <param name="allowedPathsWhenDisabled">Paths to allow when staff mode is disabled for a session (the user MUST still be in the staff role!)</param>
    public StaffModeRequirement(params string[] allowedPathsWhenDisabled)
    {
        AllowedPathsWhenDisabled = allowedPathsWhenDisabled.ToList();
    }
}

public class StaffModeAuthorizationHandler : AuthorizationHandler<StaffModeRequirement>
{
    readonly IWebHostEnvironment _environment;
    readonly IHttpContextAccessor _httpContextAccessor;

    public StaffModeAuthorizationHandler(IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor)
    {
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        StaffModeRequirement requirement)
    {
        var httpContext = context.Resource as HttpContext ?? _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            await HandleRequirementAsync(context, requirement, httpContext);
            return;
        }

        // No HTTP context available, so we can't check staff mode information!
        // So instead, just check if the user is in the staff role.
        if (context.User.IsInRole(Roles.Staff))
        {
            context.Succeed(requirement);
        }

        // NOT calling context.Succeed causes authorization to fail.
    }

    async Task HandleRequirementAsync(AuthorizationHandlerContext context, StaffModeRequirement requirement,
        HttpContext httpContext)
    {
        if (requirement.AllowedInDevelopmentEnvironment && _environment.IsDevelopment())
        {
            context.Succeed(requirement);
        }
        else if (httpContext.IsStaffMode())
        {
            context.Succeed(requirement);
        }
        else if (httpContext.User.IsInRole(Roles.Staff) && requirement.AllowedPathsWhenDisabled.Any(p =>
                     p.Equals(httpContext.Request.Path, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        // NOT calling context.Succeed causes authorization to fail.
    }
}
