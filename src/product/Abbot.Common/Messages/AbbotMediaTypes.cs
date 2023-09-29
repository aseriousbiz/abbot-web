using System.Net.Http.Headers;

namespace Serious.Abbot.Messages;

/// <summary>
/// The media types that the Abbot API accepts. This allows us to version the API in the future.
/// </summary>
public static class AbbotMediaTypes
{
    /// <summary>
    /// The media type for the Abbot API, version 1 which is a sub-type of application/json.
    /// </summary>
    public static readonly MediaTypeHeaderValue ApplicationJsonV1 = new("application/vnd.abbot.v1+json");
}
