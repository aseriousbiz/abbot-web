using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Serious.AspNetCore;

public static class UrlHelperExtensions
{
    public static string? GetReturnUrl(this IUrlHelper url, string parameterName = "returnUrl")
    {
        ArgumentNullException.ThrowIfNull(url);

        var request = url.ActionContext.HttpContext.Request;
        var returnUrl = request.Form[parameterName].LastOrDefault(v => !string.IsNullOrEmpty(v))
            ?? request.Query[parameterName].LastOrDefault(v => !string.IsNullOrEmpty(v));

        return returnUrl is { Length: > 0 } && url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
    }
}
