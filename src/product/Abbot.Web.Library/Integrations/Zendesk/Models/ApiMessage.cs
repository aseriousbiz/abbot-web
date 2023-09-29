using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public abstract class ApiMessage<T>
{
    // Why abstract? Because we need the subclass to apply 'JsonProperty' attributes to set the name.
    public abstract T? Body { get; set; }

    /// <summary>
    /// Contains cursor pagination metadata from the response, if any.
    /// </summary>
    public PaginationMetadata? Meta { get; set; }

    /// <summary>
    /// Gets the total number of items available, if using offset pagination.
    /// </summary>
    [JsonProperty("count")]
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    /// <summary>
    /// Gets the URL to the next page of results, if using offset pagination.
    /// </summary>
    [JsonProperty("next_page")]
    [JsonPropertyName("next_page")]
    public string? NextPage { get; set; }

    /// <summary>
    /// Gets the URL to the previous page of results, if using offset pagination.
    /// </summary>
    [JsonProperty("previous_page")]
    [JsonPropertyName("previous_page")]
    public string? PreviousPage { get; set; }
}

/// <summary>
/// Metadata about a paginated response.
/// </summary>
public class PaginationMetadata
{
    /// <summary>
    /// A boolean indicating if there are more results to be fetched.
    /// </summary>
    [JsonProperty("has_more")]
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    /// <summary>
    /// A cursor that can be used as the 'after' parameter in a paginated request to get the next page of results.
    /// </summary>
    /// <remarks>This can be null if the request API call 0 records.</remarks>
    [JsonProperty("after_cursor")]
    [JsonPropertyName("after_cursor")]
    public string? AfterCursor { get; set; }

    /// <summary>
    /// A cursor that can be used as the 'before' parameter in a paginated request to get the previous page of results.
    /// </summary>
    [JsonProperty("before_cursor")]
    [JsonPropertyName("before_cursor")]
    public string? BeforeCursor { get; set; }
}

// 🎉 🙄 🤬
// This is a super flexible type to read whatever weird stuff Zendesk sends us as an error.
/// <summary>
/// Represents an error returned by a Zendesk API.
/// </summary>
public class ApiError
{
    /// <summary>
    /// A machine-readable name for the error.
    /// </summary>
    [JsonProperty("code")]
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>
    /// A summary of the error.
    /// </summary>
    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// A detailed message describing the error.
    /// </summary>
    [JsonProperty("detail")]
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// A detailed message describing the error.
    /// </summary>
    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
