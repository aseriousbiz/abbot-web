using System;
using System.Net.Http;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents an incoming http request that triggered a skill.
/// </summary>
public interface IHttpTriggerEvent
{
    /// <summary>
    /// The HTTP Method of the request.
    /// </summary>
    HttpMethod HttpMethod { get; }

    /// <summary>
    /// The raw body of the HTTP request.
    /// </summary>
    string RawBody { get; }

    /// <summary>
    /// The content type of the body, if any.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// The skill's trigger URL that triggered this event.
    /// </summary>
    Uri Url { get; }

    /// <summary>
    /// The incoming request headers.
    /// </summary>
    IRequestHeaders Headers { get; }

    /// <summary>
    /// The request body as a form.
    /// </summary>
    IFormCollection Form { get; }

    /// <summary>
    /// The query string parameters as a collection.
    /// </summary>
    IQueryCollection Query { get; }

    /// <summary>
    /// Returns true if this request contains a Json body as determined by the content type.
    /// </summary>
    public bool IsJson { get; }

    /// <summary>
    /// Returns true if this request contains a form body as determined by the content type.
    /// </summary>
    public bool IsForm { get; }

    /// <summary>
    /// Deserializes the incoming request body into a dynamic object.
    /// </summary>
    /// <returns>The incoming request body deserialized as a dynamic object.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the request is not Json. Check the <see cref="IsJson"/> property first.</exception>
    dynamic? DeserializeBody();

    /// <summary>
    /// Deserializes the incoming request body into the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>An instance of the type T or null.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the request is not Json. Check the <see cref="IsJson"/> property first.</exception>
    T? DeserializeBodyAs<T>();
}
