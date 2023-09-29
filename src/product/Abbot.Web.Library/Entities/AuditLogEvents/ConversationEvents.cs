using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event raised when someone uses the website to change the title of a conversation.
/// </summary>
public class ConversationTitleChangedEvent : LegacyAuditEvent
{
    /// <summary>
    /// The old title of the conversation.
    /// </summary>
    public string OldTitle { get; set; } = null!;

    /// <summary>
    /// The new title of the conversation.
    /// </summary>
    public string NewTitle { get; set; } = null!;

    [NotMapped]
    public override bool HasDetails => true;
}

/// <summary>
/// Event raised when someone links a conversation to an external resource.
/// </summary>
public class ConversationLinkedEvent : LegacyAuditEvent
{
    /// <summary>
    /// The type of the link
    /// </summary>
    [Column(TypeName = "text")]
    public ConversationLinkType LinkType { get; set; }

    /// <summary>
    /// The external ID that the conversation is linked to.
    /// </summary>
    public string ExternalId { get; set; } = null!;
}
