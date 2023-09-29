using System;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Functions.Storage;

/// <summary>
/// Convenience extensions to <see cref="ISkillApiClient"/>.
/// </summary>
public static class SkillApiClientExtensions
{
    /// <summary>
    /// Sends <paramref name="data"/> as a JSON payload as an HTTP POST request to an HTTP endpoint and deserializes
    /// the JSON response to <typeparamref name="TResponseContent"/>.
    /// </summary>
    /// <param name="skillApiClient">The <see cref="ISkillApiClient"/> this extends.</param>
    /// <param name="url">The URL of the request.</param>
    /// <param name="data">The data to send in the body of the request as JSON.</param>
    public static Task<TResponseContent?> PostJsonAsync<TRequestContent, TResponseContent>(
        this ISkillApiClient skillApiClient,
        Uri url,
        TRequestContent data) where TResponseContent : class
        => skillApiClient.SendJsonAsync<TRequestContent, TResponseContent>(url, HttpMethod.Post, data);

    /// <summary>
    /// Makes a GET request to the specified <paramref name="url"/> and deserializes the response as the specified
    /// type.
    /// </summary>
    /// <param name="skillApiClient">The <see cref="ISkillApiClient"/> this extends.</param>
    /// <param name="url">The URL of the request.</param>
    /// <typeparam name="TResponseContent">The type to deserialize as.</typeparam>
    public static Task<TResponseContent?> GetAsync<TResponseContent>(this ISkillApiClient skillApiClient, Uri url)
        where TResponseContent : class
            => skillApiClient.SendAsync<TResponseContent>(url, HttpMethod.Get);

    /// <summary>
    /// Makes a GET request to the specified <paramref name="url"/> and deserializes the response as the specified
    /// type.
    /// </summary>
    /// <param name="skillApiClient">The <see cref="ISkillApiClient"/> this extends.</param>
    /// <param name="url">The URL of the request.</param>
    /// <typeparam name="TResponse">The expected response type (often an interface).</typeparam>
    /// <typeparam name="TResponseContent">The expected response concrete type (for deserialization).</typeparam>
    public static Task<AbbotResponse<TResponse>> GetApiAsync<TResponse, TResponseContent>(this ISkillApiClient skillApiClient, Uri url)
        where TResponse : class
        where TResponseContent : class, TResponse
            => skillApiClient.SendApiAsync<TResponse, TResponseContent>(url, HttpMethod.Get);

    /// <summary>
    /// Makes a GET request to the specified <paramref name="url"/> and deserializes the response as the specified
    /// type.
    /// </summary>
    /// <param name="skillApiClient">The <see cref="ISkillApiClient"/> this extends.</param>
    /// <param name="url">The URL of the request.</param>
    /// <typeparam name="TResponse">The expected response type (often an interface).</typeparam>
    public static Task<AbbotResponse<TResponse>> GetApiAsync<TResponse>(this ISkillApiClient skillApiClient, Uri url)
        where TResponse : class
        => skillApiClient.GetApiAsync<TResponse, TResponse>(url);

    /// <summary>
    /// Sends a request to the specified <paramref name="url"/> and deserializes the response as
    /// an <see cref="AbbotResponse{T}"/>
    /// </summary>
    /// <param name="skillApiClient">The <see cref="ISkillApiClient"/> this extends.</param>
    /// <param name="url">The <see cref="Uri"/> to request.</param>
    /// <param name="method">The <see cref="HttpMethod"/> to use for the request.</param>
    /// <typeparam name="TResponseContent">The expected response type (often an interface).</typeparam>
    public static async Task<AbbotResponse<TResponseContent>> SendApiAsync<TResponseContent>(
        this ISkillApiClient skillApiClient,
        Uri url,
        HttpMethod method)
        where TResponseContent : class
        => await skillApiClient.SendApiAsync<TResponseContent, TResponseContent>(url, method);

    /// <summary>
    /// Sends a request to the specified <paramref name="url"/> and deserializes the response as
    /// an <see cref="AbbotResponse{T}"/>
    /// </summary>
    /// <param name="skillApiClient">The <see cref="ISkillApiClient"/> this extends.</param>
    /// <param name="url">The <see cref="Uri"/> to request.</param>
    /// <param name="method">The <see cref="HttpMethod"/> to use for the request.</param>
    /// <param name="requestContent">The body of the request.</param>
    /// <typeparam name="TResponseContent">The expected response type (often an interface).</typeparam>
    /// <typeparam name="TRequestContent">The type of the request body.</typeparam>
    public static async Task<AbbotResponse<TResponseContent>> SendApiAsync<TRequestContent, TResponseContent>(
        this ISkillApiClient skillApiClient,
        Uri url,
        HttpMethod method,
        TRequestContent requestContent)
        where TResponseContent : class
        => await skillApiClient.SendApiAsync<TRequestContent, TResponseContent, TResponseContent>(
            url,
            method,
            requestContent);
}
