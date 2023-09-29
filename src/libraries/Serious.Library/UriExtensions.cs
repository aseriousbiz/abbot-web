using System;
using Microsoft.AspNetCore.WebUtilities;

namespace Serious;

public static class UriExtensions
{
    /// <summary>
    /// Appends the appendage to the end of the URL.
    /// </summary>
    /// <param name="url">The <see cref="Uri"/> to append to.</param>
    /// <param name="appendage">The thing to append.</param>
    /// <returns>A new <see cref="Uri"/>.</returns>
    public static Uri Append(this Uri url, string appendage)
    {
        appendage = appendage.StartsWith('/') ? appendage.Substring(1) : appendage;

        var urlText = url.ToString();
        var baseUri = urlText.EndsWith('/') ? url : new Uri(urlText + '/');

        return new Uri(baseUri, appendage);
    }

    public static Uri AppendQueryString(this Uri url, string key, string? value)
    {
        return new Uri(QueryHelpers.AddQueryString(url.ToString(), key, value ?? ""));
    }

    /// <summary>
    /// Appends the appendage to the end of the URL, making sure to escape it first.
    /// </summary>
    /// <param name="url">The <see cref="Uri"/> to append to.</param>
    /// <param name="appendage">The thing to append.</param>
    /// <returns>A new <see cref="Uri"/>.</returns>
    public static Uri AppendEscaped(this Uri url, string appendage)
        => url.Append(Uri.EscapeDataString(appendage));
}
