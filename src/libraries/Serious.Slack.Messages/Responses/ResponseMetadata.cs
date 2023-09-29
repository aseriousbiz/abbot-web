using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;


/// <summary>
/// Additional metadata that includes warnings and more information about errors. For APIs that
/// support pagination, includes the <c>next_cursor</c> value used to request the next page of the results.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/changelog/2016-09-28-response-metadata-is-on-the-way"/> for more
/// information.
/// </remarks>
public class ResponseMetadata
{
    /// <summary>
    /// Provides a reference to use when there are additional results to be retrieved. Pass this
    /// as the <c>cursor</c> parameter to retrieve the next page of results.
    /// </summary>
    [JsonProperty("next_cursor")]
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }

    /// <summary>
    /// Warnings that can be returned by the API.
    /// </summary>
    [JsonProperty("warnings")]
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string>? Warnings { get; init; }

    /// <summary>
    /// Messages that can be returned by the API, prefixed by "[WARN]" and ["ERROR"].
    /// </summary>
    [JsonProperty("messages")]
    [JsonPropertyName("messages")]
    public IReadOnlyList<string>? Messages { get; init; }

    /// <summary>
    /// Returns the properties of this object as a string including all the
    /// <see cref="Messages"/> and <see cref="Warnings"/>
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        var response = string.Empty;
        if (NextCursor is { })
        {
            response += $"NextCursor: {NextCursor}";
        }
        if (Messages is { Count: > 0 })
        {
            response += $"Messages: {string.Join("\n- ", Messages)}";
        }
        if (Warnings is { Count: > 0 })
        {
            response += $"Warnings: {string.Join("\n- ", Warnings)}";
        }

        return response;
    }
}
