using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event when a skill is triggered by an HTTP event.
/// </summary>
public class HttpTriggerRunEvent : TriggerRunEvent
{
    /// <summary>
    /// The collection of HTTP headers sent as part of the request.
    /// </summary>
    public string Headers { get; set; } = null!;

    /// <summary>
    /// The response headers set by the skill, if any.
    /// </summary>
    public string? ResponseHeaders { get; set; }

    /// <summary>
    /// The content type sent for the response.
    /// </summary>
    public string? ResponseContentType { get; set; }

    /// <summary>
    /// The status code returned by the trigger.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// The response.
    /// </summary>
    [Column(nameof(Response))]
    public string Response { get; set; } = null!;

    /// <summary>
    /// The request URL.
    /// </summary>
    [Column("Url")]
    public string Url { get; set; } = null!;
}
