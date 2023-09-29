using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.MergeDev.Models;

/// <summary>
/// Creates a Merge object (ticket, comment, attachment) with the given model values.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>https://docs.merge.dev/ticketing/tickets/#tickets_create</item>
/// <item>https://docs.merge.dev/ticketing/comments/#comments_create</item>
/// <item>https://docs.merge.dev/ticketing/attachments/#attachments_create</item>
/// </list>
/// </remarks>
public class Create
{
    [JsonProperty("model")]
    [JsonPropertyName("model")]
    public IReadOnlyDictionary<string, object?> Model { get; set; } = null!;

    /// <summary>
    /// Whether to include debug fields (such as log file links) in the response.
    /// </summary>
    [JsonProperty("is_debug_mode")]
    [JsonPropertyName("is_debug_mode")]
    public bool IsDebugMode { get; set; }

    /// <summary>
    /// Whether or not third-party updates should be run asynchronously.
    /// </summary>
    [JsonProperty("run_async")]
    [JsonPropertyName("run_async")]
    public bool RunAsync { get; set; }
}
