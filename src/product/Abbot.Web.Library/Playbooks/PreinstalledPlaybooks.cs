using System.Collections.Generic;
using System.IO;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Describes a playbook to be preinstalled.
/// </summary>
/// <param name="Name">The name of the playbook.</param>
/// <param name="Description">A description of the playbook.</param>
public record PreinstalledPlaybook(string Slug, string Name, string Description)
{
    public static readonly IReadOnlyList<PreinstalledPlaybook> ForNewOrganizations = new List<PreinstalledPlaybook>()
    {
        FromResource("trial-management", "Trial Management", "Manage trials using Abbot."),
        FromResource("customer-activity-check-in", "Customer Activity Check-In", "Reminds your agents about upcoming events in your customer lifecycle, like quarterly reviews and renewals."),
    };

    /// <summary>
    /// The playbook definition, serialized as JSON.
    /// </summary>
    public required string SerializedDefinition { get; init; }

    static PreinstalledPlaybook FromResource(string Slug, string Name, string Description)
    {
        var resourcePath = $"{typeof(PreinstalledPlaybook).Namespace}.Preinstalled.{Slug}.json";
        using var resourceStream = typeof(PreinstalledPlaybook).Assembly.GetManifestResourceStream(resourcePath).Require();
        using var reader = new StreamReader(resourceStream);

        // Using ReadToEndAsync isn't really necessary since this should be a mostly in-memory stream. Making this async is HARD.
        var definition = reader.ReadToEnd();

        return new(Slug, Name, Description)
        {
            SerializedDefinition = definition
        };
    }
}
