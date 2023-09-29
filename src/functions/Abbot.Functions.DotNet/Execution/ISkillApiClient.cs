using System;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Execution;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Functions.Storage;

/// <summary>
/// Api Client for the skill runner APIs hosted on abbot-web. This class understands the authentication
/// mechanism when calling a skill runner API.
/// </summary>
public interface ISkillApiClient
{
    /// <summary>
    /// Retrieve the base skill API URL. This URL already includes the skill Id.
    /// </summary>
    /// <returns>The <see cref="Uri"/> pointing to the API endpoint.</returns>
    Uri BaseApiUrl { get; }

    /// <summary>
    /// Sends a request to the specified <paramref name="url"/> and deserializes the response as the
    /// specified <typeparamref name="TResponse"/>.
    /// </summary>
    /// <param name="url">The <see cref="Uri"/> to request.</param>
    /// <param name="method">The <see cref="HttpMethod"/> to use for the request.</param>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <returns>A task with the response body deserialized as a <typeparamref name="TResponse"/>.</returns>
    Task<TResponse?> SendAsync<TResponse>(Uri url, HttpMethod method) where TResponse : class;

    /// <summary>
    /// Sends a request to the specified <paramref name="url"/> and deserializes the response as
    /// an <see cref="AbbotResponse"/>
    /// </summary>
    /// <param name="url">The <see cref="Uri"/> to request.</param>
    /// <param name="method">The <see cref="HttpMethod"/> to use for the request.</param>
    Task<AbbotResponse> SendApiAsync(Uri url, HttpMethod method);

    /// <summary>
    /// Sends a request to the specified <paramref name="url"/> and deserializes the response as
    /// an <see cref="AbbotResponse{T}"/>
    /// </summary>
    /// <param name="url">The <see cref="Uri"/> to request.</param>
    /// <param name="method">The <see cref="HttpMethod"/> to use for the request.</param>
    /// <typeparam name="TResponse">The expected response type (often an interface).</typeparam>
    /// <typeparam name="TResponseContent">The expected response concrete type (for deserialization).</typeparam>
    Task<AbbotResponse<TResponse>> SendApiAsync<TResponse, TResponseContent>(Uri url, HttpMethod method)
        where TResponse : class
        where TResponseContent : class, TResponse;

    /// <summary>
    /// Sends a request to the specified <paramref name="url"/> and deserializes the response as
    /// an <see cref="AbbotResponse{T}"/>
    /// </summary>
    /// <param name="url">The <see cref="Uri"/> to request.</param>
    /// <param name="method">The <see cref="HttpMethod"/> to use for the request.</param>
    /// <param name="requestContent">The body of the request.</param>
    /// <typeparam name="TResponse">The expected response type (often an interface).</typeparam>
    /// <typeparam name="TResponseContent">The expected response concrete type (for deserialization).</typeparam>
    /// <typeparam name="TRequestContent">The type of the request body.</typeparam>
    Task<AbbotResponse<TResponse>> SendApiAsync<TRequestContent, TResponse, TResponseContent>(
        Uri url,
        HttpMethod method,
        TRequestContent requestContent)
        where TResponse : class
        where TResponseContent : class, TResponse;

    /// <summary>
    /// Sends a request to the specified <paramref name="url"/>.
    /// </summary>
    /// <param name="url">The <see cref="Uri"/> to request.</param>
    /// <param name="method">The <see cref="HttpMethod"/> to use for the request.</param>
    /// <returns>A <see cref="HttpResponseMessage"/> with information about the response to the request.</returns>
    Task<HttpResponseMessage> SendAsync(Uri url, HttpMethod method);

    /// <summary>
    /// Calls the api/skills/{id}/compilation api to download a compiled skill and
    /// symbols (if appropriate) and returns it.
    /// </summary>
    /// <param name="skillIdentifier">Uniquely identifies a compiled skill.</param>
    /// <param name="recompile">Whether to force recompile</param>
    Task<ICompiledSkill> DownloadCompiledSkillAsync(ICompiledSkillIdentifier skillIdentifier, bool recompile);

    /// <summary>
    /// Sends <paramref name="data"/> as a JSON payload to an HTTP endpoint and deserializes the JSON response
    /// to <typeparamref name="TResponseContent"/>.
    /// </summary>
    /// <param name="url">The URL of the request.</param>
    /// <param name="method">The HTTP Method of the request.</param>
    /// <param name="data">The data to send in the body of the request as JSON.</param>
    Task<TResponseContent?> SendJsonAsync<TRequestContent, TResponseContent>(
        Uri url,
        HttpMethod method,
        TRequestContent data)
        where TResponseContent : class;
}
