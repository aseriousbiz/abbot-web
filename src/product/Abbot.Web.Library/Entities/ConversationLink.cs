using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a link from a conversation to an external resource, such as a Zendesk ticket.
/// </summary>
public class ConversationLink : OrganizationEntityBase<ConversationLink>
{
    /// <summary>
    /// Gets or sets the ID of the <see cref="Conversation"/> this link references.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Conversation"/> this link references.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of this link
    /// </summary>
    [Column(TypeName = "text")]
    public ConversationLinkType LinkType { get; set; }

    /// <summary>
    /// Gets or sets the ID of the linked resource.
    /// </summary>
    public string ExternalId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ID of the <see cref="Member"/> who created this link, if any.
    /// </summary>
    public int? CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the the <see cref="Member"/> who created this link, if any.
    /// </summary>
    public Member? CreatedBy { get; set; }

    /// <summary>
    /// Additional settings for this link. For example, in HubSpot this might also store the Id of the
    /// conversation thread.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Settings { get; set; }
}

public enum ConversationLinkType
{
    Unknown = 0,
    ZendeskTicket,
    HubSpotTicket,
    GitHubIssue,
    MergeDevTicket,
}

public static class ConversationLinkTypeExtensions
{
    /// <summary>
    /// Returns a string suitable for displaying to a user.
    /// </summary>
    /// <param name="linkType"></param>
    /// <returns></returns>
    public static string ToDisplayString(this ConversationLinkType linkType)
    {
        return linkType switch
        {
            ConversationLinkType.ZendeskTicket => "Zendesk ticket",
            ConversationLinkType.HubSpotTicket => "HubSpot ticket",
            ConversationLinkType.GitHubIssue => "GitHub issue",
            ConversationLinkType.Unknown => "Unknown resource",
            _ => linkType.ToString(),
        };
    }
}
