using System;
using Microsoft.AspNetCore.Http;

namespace Serious.Slack.AspNetCore;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class VerifySlackRequestAttribute : Attribute
{
    public static readonly string RequestBodyKey = "Slack:RequestBody";

    /// <summary>
    /// If the Slack Request Body is stored in the <see cref="HttpContext.Items"/>, this will return the
    /// body.
    /// </summary>
    /// <param name="httpContext">The current <see cref="HttpContext"/>.</param>
    public static string? GetSlackRequestBody(HttpContext httpContext)
    {
        return httpContext.Items[RequestBodyKey] as string;
    }
}
