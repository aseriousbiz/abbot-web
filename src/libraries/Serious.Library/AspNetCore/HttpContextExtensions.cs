using System;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Serious.Abbot;

namespace Serious.AspNetCore;

public static class HttpContextExtensions
{
    public static string GetRawUrl(this HttpContext httpContext)
    {
        var httpRequestFeature = httpContext.Features.Get<IHttpRequestFeature>();
        return httpRequestFeature.RawTarget;
    }

    public static string GetPageName(this HttpContext httpContext)
    {
        return httpContext.GetRawUrl()
            .GetPageName();
    }

    public static string GetPageName(this string path)
    {
        var trimmed = path.TrimSuffix("/", StringComparison.Ordinal)
            .TrimSuffix("/Index", StringComparison.OrdinalIgnoreCase);
        return trimmed.RightAfterLast("/", StringComparison.Ordinal);
    }

    public static string GetPath(this HttpContext httpContext)
    {
        return httpContext.GetRawUrl()
            .LeftBefore("?", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAuthenticated(this HttpContext httpContext)
    {
        return httpContext is { User: { Identity: { IsAuthenticated: true } } };
    }

    /// <summary>
    /// Retrieves page info using a convention given the current path.
    /// </summary>
    /// <param name="httpContext">The current <see cref="HttpContext"/>.</param>
    /// <returns></returns>
    public static PageInfo GetPageInfo(this HttpContext httpContext)
    {
        return GetPageInfo(httpContext.Request.Path);
    }

    /// <summary>
    /// Retrieves page info using a convention given the current path.
    /// </summary>
    /// <param name="path">The current request path.</param>
    /// <returns></returns>
    public static PageInfo GetPageInfo(this PathString path)
    {
        var segments = path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);

        string Normalize(string segment)
        {
            return segment.Equals("Index", StringComparison.OrdinalIgnoreCase)
                ? "Home"
                : JavaScriptEncoder.Default.Encode(segment.Titleize());
        }

        return segments switch
        {
            { Length: 0 } => new PageInfo("Home", "Home", "Home"),
            { Length: 1 } => new PageInfo(Normalize(segments[0]), "Home"),
            { Length: >= 2 } => new PageInfo(Normalize(segments[^2]), Normalize(segments[^1])),
            _ => throw new UnreachableException() // Arrays can't have negative lengths so we're good. LOL.
        };
    }

    /// <summary>
    /// Used to store something in HttpContext.Items so that it can be retrieved later in the same request.
    /// </summary>
    /// <param name="httpContext">The current <see cref="HttpContext"/>.</param>
    /// <param name="key">A key to use.</param>
    /// <param name="factory">The factory</param>
    public static async Task<T> GetOrCreateAsync<T>(this HttpContext httpContext, string key, Func<Task<T>> factory)
    {
        if (httpContext.Items.TryGetValue(key, out var value))
        {
            return (T)value;
        }

        var result = await factory();
        httpContext.Items[key] = result;
        return result;
    }
}
