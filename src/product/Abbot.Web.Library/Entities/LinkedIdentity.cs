using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a link between a <see cref="Member"/> and an identity in an external service.
/// </summary>
/// <remarks>
/// This entity references both <see cref="Member"/> and <see cref="Organization"/> via separate relations.
/// This would seem to be unnecessary since a <see cref="Member"/> is already related to an <see cref="Organization"/>.
/// However, we need to consider foreign members in shared channels.
/// A foreign member is a member that is not in the same organization as the channel.
/// They might have some linked identities in their home organization that should not apply when they are acting as a foreign member in a shared channel.
/// So the <see cref="LinkedIdentity"/> is tied to the <see cref="Organization"/> in which it should apply separately from the <see cref="Member"/> to whom it applies.
/// </remarks>
public class LinkedIdentity : OrganizationEntityBase<LinkedIdentity>
{
    /// <summary>
    /// The ID of the <see cref="Member"/> this link relates to.
    /// </summary>
    public int MemberId { get; set; }

    /// <summary>
    /// The <see cref="Member"/> this link relates to.
    /// </summary>
    public Member Member { get; set; } = null!;

    /// <summary>
    /// The type of the linked entity.
    /// </summary>
    [Column(TypeName = "citext")]
    public LinkedIdentityType Type { get; set; }

    /// <summary>
    /// The ID of the linked identity.
    /// </summary>
    public string ExternalId { get; set; } = null!;

    /// <summary>
    /// The name or username associated with the external identity.
    /// </summary>
    public string? ExternalName { get; set; }

    /// <summary>
    /// Optional. Metadata about the linked identity.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? ExternalMetadata { get; set; }
}

public enum LinkedIdentityType
{
    /// <summary>
    /// A Zendesk user that Abbot should use when acting as this user.
    /// </summary>
    [Display(Name = "Zendesk User")]
    Zendesk,

    /// <summary>
    /// A HubSpot Contact that Abbot should use when acting as this user.
    /// </summary>
    [Display(Name = "HubSpot Contact")]
    HubSpot,
}
