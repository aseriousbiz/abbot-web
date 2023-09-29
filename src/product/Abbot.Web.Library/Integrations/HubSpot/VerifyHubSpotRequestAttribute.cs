using System;
using Microsoft.AspNetCore.Http;

namespace Serious.Abbot.Integrations.HubSpot;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class VerifyHubSpotRequestAttribute : Attribute
{
    public static readonly string RequestBodyKey = "HubSpot:RequestBody";

    /// <summary>
    /// If the HubSpot Webhook Request Body is stored in the <see cref="HttpContext.Items"/>, this will return the
    /// body.
    /// </summary>
    /// <param name="httpContext">The current <see cref="HttpContext"/>.</param>
    public static string? GetHubSpotWebhookRequestBody(HttpContext httpContext)
    {
        return httpContext.Items[RequestBodyKey] as string;
    }
}
