using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents when an Http trigger is created, changed, or removed.
/// </summary>
public class HttpTriggerChangeEvent : TriggerChangeEvent
{
    /// <summary>
    /// The Api Token required in order to send HTTP requests to the <see cref="Skill"/>.
    /// </summary>
    [Column("Url")]
    public string ApiToken { get; set; } = string.Empty;
}
