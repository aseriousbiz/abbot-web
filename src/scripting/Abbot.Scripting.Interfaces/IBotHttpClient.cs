using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Scripting;

/// <summary>
/// A simple HTTP client to make requests.
/// </summary>
public interface IBotHttpClient
{
    /// <summary>
    /// Makes an HTTP request sending the optional content and returns JSON as a dynamic object.
    /// </summary>
    /// <param name="url">The url to request.</param>
    /// <param name="httpMethod">The HTTP method of the request</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    Task<dynamic?> SendJsonAsync(Uri url, HttpMethod httpMethod, object? content, Headers headers);

    /// <summary>
    /// Makes an HTTP request sending the optional content and returns JSON as a dynamic object.
    /// </summary>
    /// <param name="url">The url to request.</param>
    /// <param name="httpMethod">The HTTP method of the request</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    Task<AbbotResponse<TResponse>> SendJsonAsAsync<TResponse>(Uri url, HttpMethod httpMethod, object? content, Headers headers);

    /// <summary>
    /// Requests the specified url and returns an <see cref="HtmlNode"/> with
    /// the first section of the HTML that matches the selector. This uses the
    /// HtmlAgilityPack under the hood.
    /// </summary>
    /// <returns>An <see cref="HtmlNode"/> with the section of the web page.</returns>
    Task<HtmlNode?> ScrapeAsync(Uri url, string selector);

    /// <summary>
    /// Requests the specified url and returns an <see cref="HtmlNode"/> with
    /// all sections of the HTML that match the selector. This uses the
    /// HtmlAgilityPack under the hood.
    /// </summary>
    /// <returns>An <see cref="HtmlNode"/> with the section of the web page.</returns>
    Task<IReadOnlyList<HtmlNode>> ScrapeAllAsync(Uri url, string selector);
}
