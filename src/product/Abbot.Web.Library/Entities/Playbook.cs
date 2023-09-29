using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serious.Cryptography;
using Serious.EntityFrameworkCore;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a Playbook
/// </summary>
public partial class Playbook : OrganizationEntityBase<Playbook>
{
    /// <summary>
    /// A <see cref="Regex"/> that can be used to validate a slug.
    /// </summary>
    /// <returns></returns>
    public static readonly Regex SlugRegex = CreateSlugRegex();

    [GeneratedRegex(WebConstants.NameCharactersPattern)]
    private static partial Regex CreateSlugRegex();

    public Playbook()
    {
        Versions = new EntityList<PlaybookVersion>();
        Runs = new EntityList<PlaybookRun>();
        RunGroups = new EntityList<PlaybookRunGroup>();
    }

    public Playbook(DbContext db)
    {
        Versions = new EntityList<PlaybookVersion>(db, this, nameof(Versions));
        Runs = new EntityList<PlaybookRun>(db, this, nameof(Runs));
        RunGroups = new EntityList<PlaybookRunGroup>(db, this, nameof(RunGroups));
    }

    /// <summary>
    /// The name of the playbook
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The slug for the playbook.
    /// This is the short name used in URLs.
    /// It is case-insensitive and can only contain letters, numbers, and hyphens.
    /// </summary>
    [Column(TypeName = "citext")]
    public required string Slug { get; set; }

    /// <summary>
    /// A description of the playbook
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Specifies if a playbook is enabled.
    /// A disabled playbook will never be triggered, even if there are published versions and the triggering conditions are met.
    /// </summary>
    public required bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="PlaybookProperties"/> representing additional properties of this playbook.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public PlaybookProperties Properties { get; set; } = new();

    /// <summary>
    /// The set of versions for this playbook.
    /// This collection is usually not materialized automatically.
    /// </summary>
    public EntityList<PlaybookVersion> Versions { get; set; }

    /// <summary>
    /// The set of runs for this playbook.
    /// This collection is usually not materialized automatically.
    /// </summary>
    public EntityList<PlaybookRun> Runs { get; set; }

    /// <summary>
    /// The set of run groups for this playbook.
    /// This collection is usually not materialized automatically.
    /// </summary>
    public EntityList<PlaybookRunGroup> RunGroups { get; set; }
}

public static class PlaybookExtensions
{
    public static string GetWebhookTriggerToken(this Playbook playbook)
    {
        var tokenBytes = $"{playbook.Id}".ComputeHMACSHA256HashBytes(playbook.Properties.WebhookTokenSeed);

        return Base64UrlEncoder.Encode($"{tokenBytes}:{playbook.OrganizationId}");
    }

    public static bool TryGetOrganizationIdFromToken(string token, out int organizationId)
    {
        organizationId = default;
        string decoded;
        try
        {
            decoded = Base64UrlEncoder.Decode(token);
        }
        catch (FormatException)
        {
            // There is no TryDecode sadly.
            return false;
        }

        return decoded.Split(':') is [_, var organizationIdText]
               && int.TryParse(organizationIdText, out organizationId);
    }

    public static bool IsValidWebhookTriggerToken(this Playbook playbook, string token) => GetWebhookTriggerToken(playbook) == token;
}

/// <summary>
/// Represents additional properties of a playbook.
/// </summary>
public record PlaybookProperties
{
    /// <summary>
    /// The seed used to generate the webhook token for a webhook trigger.
    /// </summary>
    public string WebhookTokenSeed { get; init; } = TokenCreator.CreateRandomString(64);
}
