using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a link from a room to an external resource, such as a Zendesk organization.
/// </summary>
public class RoomLink : OrganizationEntityBase<RoomLink>
{
    /// <summary>
    /// Gets or sets the ID of the <see cref="Room"/> this link references.
    /// </summary>
    public int RoomId { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Room"/> this link references.
    /// </summary>
    public Room Room { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of this link
    /// </summary>
    [Column(TypeName = "text")]
    public RoomLinkType LinkType { get; set; }

    /// <summary>
    /// Gets or sets the ID of the linked resource.
    /// </summary>
    public string ExternalId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the display name of the linked resource.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ID of the <see cref="Member"/> who created this link, if any.
    /// </summary>
    public int? CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the the <see cref="Member"/> who created this link, if any.
    /// </summary>
    public Member? CreatedBy { get; set; }
}

public enum RoomLinkType
{
    [Display(Name = "Unknown Resource")]
    Unknown = 0,

    [Display(Name = "Zendesk Organization")]
    ZendeskOrganization,

    [Display(Name = "HubSpot Company")]
    HubSpotCompany,
}
