using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Extension methods on <see cref="IBotHttpClient" /> to make it easy to request JSON and deserialize it to
/// a static type.
/// </summary>
public static class BotHttpClientStaticExtensions
{
    /// <summary>
    /// Makes an HTTP GET request for the url and returns the JSON as the specified type.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> GetJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        string url) => await httpClient.GetJsonAsAsync<TResponse>(new Uri(url));

    /// <summary>
    /// Makes an HTTP GET request for the url and returns the JSON as the specified type.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> GetJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        Uri url) => await httpClient.GetJsonAsAsync<TResponse>(url, new Headers());

    /// <summary>
    /// Makes an HTTP GET request for the url and returns the JSON as the specified type.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> GetJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        string url,
        Headers headers) => await httpClient.GetJsonAsAsync<TResponse>(new Uri(url), headers);

    /// <summary>
    /// Makes an HTTP GET request for the url and returns the JSON as the specified type.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> GetJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        Uri url,
        Headers headers) =>
        await httpClient.SendJsonAsAsync<TResponse>(url, HttpMethod.Get, null, headers).RequireSuccess();

    /// <summary>
    /// Makes an HTTP POST request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PostJsonAsAsync<TResponse>(this IBotHttpClient httpClient, string url, object content)
    {
        return await httpClient.PostJsonAsAsync<TResponse>(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP POST request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PostJsonAsAsync<TResponse>(this IBotHttpClient httpClient, Uri url, object content)
    {
        return await httpClient.PostJsonAsAsync<TResponse>(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP POST request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PostJsonAsAsync<TResponse>(this IBotHttpClient httpClient, string url,
        object content, Headers headers)
        => await httpClient.PostJsonAsAsync<TResponse>(new Uri(url), content, headers);

    /// <summary>
    /// Makes an HTTP POST request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PostJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        Uri url,
        object content,
        Headers headers) =>
        await httpClient.SendJsonAsAsync<TResponse>(url, HttpMethod.Post, content, headers).RequireSuccess();

    /// <summary>
    /// Makes an HTTP PUT request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PutJsonAsAsync<TResponse>(this IBotHttpClient httpClient, string url, object content)
    {
        return await httpClient.PutJsonAsAsync<TResponse>(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP PUT request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PutJsonAsAsync<TResponse>(this IBotHttpClient httpClient, Uri url, object content)
    {
        return await httpClient.PutJsonAsync(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP PUT request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PutJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        string url,
        object content,
        Headers headers)
        => await httpClient.PutJsonAsAsync<TResponse>(new Uri(url), content, headers);

    /// <summary>
    /// Makes an HTTP PUT request for the url, sends the content as JSON, and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to send as JSON.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PutJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        Uri url,
        object content,
        Headers headers) =>
        await httpClient.SendJsonAsAsync<TResponse>(url, HttpMethod.Put, content, headers).RequireSuccess();

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> DeleteJsonAsAsync<TResponse>(this IBotHttpClient httpClient, string url)
    {
        return await httpClient.DeleteJsonAsAsync<TResponse>(url, new Headers());
    }

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> DeleteJsonAsAsync<TResponse>(this IBotHttpClient httpClient, Uri url)
    {
        return await httpClient.DeleteJsonAsAsync<TResponse>(url, new Headers());
    }

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> DeleteJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        string url,
        Headers headers)
        => await httpClient.DeleteJsonAsAsync<TResponse>(new Uri(url), headers);

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> DeleteJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        Uri url,
        Headers headers)
    {
        return await httpClient.SendJsonAsAsync<TResponse>(url, HttpMethod.Delete, null, headers).RequireSuccess();
    }

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to patch.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PatchJsonAsAsync<TResponse>(this IBotHttpClient httpClient, string url, object content)
    {
        return await httpClient.PatchJsonAsAsync<TResponse>(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to patch.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PatchJsonAsAsync<TResponse>(this IBotHttpClient httpClient, Uri url, object content)
    {
        return await httpClient.PatchJsonAsAsync<TResponse>(url, content, new Headers());
    }

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to patch.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PatchJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        string url,
        object content,
        Headers headers)
        => await httpClient.PatchJsonAsAsync<TResponse>(new Uri(url), content, headers);

    /// <summary>
    /// Makes an HTTP DELETE request for the url and returns the JSON as a dynamic object.
    /// </summary>
    /// <param name="httpClient">The <see cref="IBotHttpClient"/> instance.</param>
    /// <param name="url">The url to request.</param>
    /// <param name="content">The content to patch.</param>
    /// <param name="headers">The HTTP headers to send.</param>
    /// <returns>A <typeparam name="TResponse"/> from the returned JSON.</returns>
    public static async Task<TResponse?> PatchJsonAsAsync<TResponse>(
        this IBotHttpClient httpClient,
        Uri url,
        object content,
        Headers headers)
    {
        return await httpClient.SendJsonAsAsync<TResponse>(url, HttpMethod.Patch, content, headers).RequireSuccess();
    }
}
