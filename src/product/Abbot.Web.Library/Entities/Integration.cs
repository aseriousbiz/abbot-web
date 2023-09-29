using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

// We don't care that Integration collides with Microsoft.Bot.Builder.Integration.
// It'll be fine.
#pragma warning disable CA1724

/// <summary>
/// Represents an integration between an Abbot organization and a third-party service, such as Zendesk.
/// </summary>
public class Integration // TODO: EntityBase<Integration>
{
    /// <summary>
    /// Gets or sets the unique ID of the integration record.
    /// </summary>
    public int Id { get; set; }

    public static implicit operator Id<Integration>(Integration entity) => new(entity.Id);

    /// <summary>
    /// Gets or sets the ID of the <see cref="Organization"/> that owns the integration record.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the organization that owns the integration record.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// Gets or sets the service this integration record is for.
    /// </summary>
    [Column(TypeName = "text")]
    public IntegrationType Type { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating if this integration is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the raw JSON that contains the integration settings.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Settings { get; set; } = null!;

    /// <summary>
    /// An identifier for the external integration if applicable. For HubSpot, this would be the Portal Id.
    /// </summary>
    public string? ExternalId { get; set; }
}

/// <summary>
/// Specifies the names of services that can be integrated with an Abbot organization.
/// </summary>
#pragma warning disable CA1027
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum IntegrationType
#pragma warning restore CA1027
{
    None = 0,

    Zendesk = 1,

    [Display(Name = "Custom Slack App")]
    SlackApp = 2,

    // Removed: Steamship = 3
    // Was never enabled in production, so this value could be reclaimed.

    [Display(Name = "HubSpot")]
    HubSpot = 4,

    [Display(Name = "GitHub")]
    GitHub = 5,

    [Display(Name = "Create Ticket")]
    Ticketing = 6, // Merge.dev
}
