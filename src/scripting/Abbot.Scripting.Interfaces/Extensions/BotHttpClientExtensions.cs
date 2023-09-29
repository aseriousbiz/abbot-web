using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Extension methods on <see cref="IBotHttpClient" /> to make it easy to request JSON.
/// </summary>
public static class BotHttpClientExtensions
{
    /// <summary>
    /// Makes an HTTP request sending the optional content and returns JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="httpMethod">The HTTP method of the request</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    static Task<dynamic?> SendJsonAsync(this IBotHttpClient httpClient, string url, HttpMethod httpMethod, object? content, Headers headers)
    {
        return httpClient.SendJsonAsync(new Uri(url), httpMethod, content, headers);
    }

    /// <summary>
    /// Makes an HTTP GET request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> GetJsonAsync(this IBotHttpClient httpClient, string url)
    {
        return httpClient.GetJsonAsync(url, new Headers());
    }

    /// <summary>
    /// Makes an HTTP GET request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> GetJsonAsync(this IBotHttpClient httpClient, Uri url)
    {
        return httpClient.GetJsonAsync(url, new Headers());
    }

    /// <summary>
    /// Makes an HTTP GET request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static async Task<dynamic?> GetJsonAsync(this IBotHttpClient httpClient, string url, Headers headers)
        => await httpClient.GetJsonAsync(new Uri(url), headers);

    /// <summary>
    /// Makes an HTTP GET request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> GetJsonAsync(this IBotHttpClient httpClient, Uri url, Headers headers)
    {
        return httpClient.SendJsonAsync(url, HttpMethod.Get, null, headers);
    }

    /// <summary>
    /// Makes an HTTP POST request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> PostJsonAsync(this IBotHttpClient httpClient, string url, object content)
    {
        return httpClient.PostJsonAsync(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP POST request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> PostJsonAsync(this IBotHttpClient httpClient, Uri url, object content)
    {
        return httpClient.PostJsonAsync(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP POST request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static async Task<dynamic?> PostJsonAsync(this IBotHttpClient httpClient, string url, object content, Headers headers)
        => await httpClient.PostJsonAsync(new Uri(url), content, headers);

    /// <summary>
    /// Makes an HTTP POST request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> PostJsonAsync(this IBotHttpClient httpClient, Uri url, object content, Headers headers)
    {
        return httpClient.SendJsonAsync(url, HttpMethod.Post, content, headers);
    }

    /// <summary>
    /// Makes an HTTP PUT request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> PutJsonAsync(this IBotHttpClient httpClient, string url, object content)
    {
        return httpClient.PutJsonAsync(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP PUT request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> PutJsonAsync(this IBotHttpClient httpClient, Uri url, object content)
    {
        return httpClient.PutJsonAsync(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP PUT request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static async Task<dynamic?> PutJsonAsync(this IBotHttpClient httpClient, string url, object content, Headers headers)
        => await httpClient.PutJsonAsync(new Uri(url), content, headers);

    /// <summary>
    /// Makes an HTTP PUT request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> PutJsonAsync(this IBotHttpClient httpClient, Uri url, object content, Headers headers)
    {
        return httpClient.SendJsonAsync(url, HttpMethod.Put, content, headers);
    }

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> DeleteJsonAsync(this IBotHttpClient httpClient, string url)
    {
        return httpClient.DeleteJsonAsync(url, new Headers());
    }

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> DeleteJsonAsync(this IBotHttpClient httpClient, Uri url)
    {
        return httpClient.DeleteJsonAsync(url, new Headers());
    }

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static async Task<dynamic?> DeleteJsonAsync(this IBotHttpClient httpClient, string url, Headers headers)
        => await httpClient.DeleteJsonAsync(new Uri(url), headers);

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A dynamic object with the structure of the returned JSON.</returns>
    public static Task<dynamic?> DeleteJsonAsync(this IBotHttpClient httpClient, Uri url, Headers headers)
    {
        return httpClient.SendJsonAsync(url, HttpMethod.Delete, null, headers);
    }
}
