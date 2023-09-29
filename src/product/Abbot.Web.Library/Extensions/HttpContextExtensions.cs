using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Extensions;

/// <summary>
/// Extension methods to <see cref="HttpContext"/>.
/// </summary>
public static class HttpContextExtensions
{
    const string CurrentMember = nameof(CurrentMember);

    /// <summary>
    /// Sets the current member in the <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/>.</param>
    /// <param name="member">The <see cref="Member"/> to set.</param>
    public static void SetCurrentMember(
        this HttpContext context,
        Member member)
    {
        context.Items[CurrentMember] = member;
    }

    /// <summary>
    /// Retrieves the current <see cref="Member"/> model stored for this request.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>The <see cref="Member"/>.</returns>
    public static Member? GetCurrentMember(this HttpContext context)
    {
        var model = context.Items[CurrentMember] as Member;
        return model;
    }

    /// <summary>
    /// Retrieves the current <see cref="Member"/> model stored for this request.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>The <see cref="Member"/>.</returns>
    /// <exception cref="UnreachableException">If <see cref="Organization"/> is not found.</exception>
    public static Member RequireCurrentMember(this HttpContext context) =>
        context.GetCurrentMember().Require();

    /// <summary>
    /// Retrieves the current organization model stored for this request.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns></returns>
    public static Organization? GetCurrentOrganization(this HttpContext context) => context.GetCurrentMember()?.Organization;

    /// <summary>
    /// Retrieves the current organization model stored for this request.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <exception cref="UnreachableException">If <see cref="Organization"/> is not found.</exception>
    public static Organization RequireCurrentOrganization(this HttpContext context) =>
        context.GetCurrentOrganization().Require();

    public static bool IsSwapPingPath(this HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;

#if DEBUG
        if (Environment.GetEnvironmentVariable("DOCKER_FRIENDLY") == "true" &&
            httpContext.Request.Host.Host == "host.docker.internal")
            return true;
#endif

        return path.Equals("/warmup", StringComparison.OrdinalIgnoreCase);
    }
}
