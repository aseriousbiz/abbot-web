using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class CommentListMessage : ApiMessage<IReadOnlyList<Comment>>
{
    [JsonProperty("comments")]
    [JsonPropertyName("comments")]
    public override IReadOnlyList<Comment>? Body { get; set; }
}

/// <summary>
/// Represents a comment on a Zendesk ticket.
/// </summary>
public class Comment
{
    /// <summary>
    /// The ID of the comment.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// The ID of the user who authored the comment.
    /// </summary>
    [JsonProperty("author_id")]
    [JsonPropertyName("author_id")]
    public long AuthorId { get; set; }

    /// <summary>
    /// The Markdown-formatted body of the comment.
    /// </summary>
    [JsonProperty("body")]
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    /// <summary>
    /// The plain-text body of the comment.
    /// </summary>
    [JsonProperty("plain_body")]
    [JsonPropertyName("plain_body")]
    public string? PlainBody { get; set; }

    /// <summary>
    /// The HTML-formatted body of the comment.
    /// </summary>
    [JsonProperty("html_body")]
    [JsonPropertyName("html_body")]
    public string? HtmlBody { get; set; }

    /// <summary>
    /// A boolean indicating whether the comment is a public reply.
    /// </summary>
    [JsonProperty("public")]
    [JsonPropertyName("public")]
    public bool? Public { get; set; }

    /// <summary>
    /// The ID of the audit record containing the comment.
    /// </summary>
    [JsonProperty("audit_id")]
    [JsonPropertyName("audit_id")]
    public long AuditId { get; set; }

    /// <summary>
    /// Metadata about the comment.
    /// </summary>
    [JsonProperty("metadata")]
    [JsonPropertyName("metadata")]
    public AuditMetadata? AuditMetadata { get; set; }

    /// <summary>
    /// If specified, points to an attachment.
    /// </summary>
    [JsonProperty("attachments")]
    [JsonPropertyName("attachments")]
    public IReadOnlyList<Attachment> Attachments { get; set; } = null!;
}
