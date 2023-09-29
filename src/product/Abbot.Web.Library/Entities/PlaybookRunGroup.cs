using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Playbooks;
using Serious.EntityFrameworkCore;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a playbook run group, which is a collection of runs dispatched at the same time.
/// </summary>
public class PlaybookRunGroup : EntityBase<PlaybookRunGroup>
{
    public PlaybookRunGroup()
    {
        Runs = new EntityList<PlaybookRun>();
    }

    public PlaybookRunGroup(DbContext db)
    {
        Runs = new EntityList<PlaybookRun>(db, this, nameof(Runs));
    }

    /// <summary>
    /// Gets or inits the ID of the <see cref="Entities.Playbook"/> for this run group.
    /// </summary>
    public int PlaybookId { get; init; }

    /// <summary>
    /// Gets or inits the <see cref="Entities.Playbook"/> for this run group.
    /// </summary>
    public required Playbook Playbook { get; init; }

    /// <summary>
    /// Gets or sets the version number of <see cref="Playbook"/> for this run group.
    /// </summary>
    /// <remarks>
    /// This should be created from a <see cref="PlaybookVersion"/>,
    /// but uses <see cref="Entities.Playbook"/> for the foreign key in case we allow deleting old versions.
    /// </remarks>
    public required int Version { get; init; }

    /// <summary>
    /// Gets or sets a correlation ID that can be used to ensure messages for an entire run group are processed together.
    /// </summary>
    // I'm not 100% sure we'll need this, but it's cheap to add.
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="PlaybookRunGroupProperties"/> representing additional properties of this run group.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public PlaybookRunGroupProperties Properties { get; set; } = new();

    /// <summary>
    /// The set of runs for this playbook.
    /// </summary>
    public EntityList<PlaybookRun> Runs { get; set; }

    /// <summary>
    /// The <see cref="Member"/> who created this playbook run group.
    /// </summary>
    public Member? CreatedBy { get; set; }

    /// <summary>
    /// The Id of the <see cref="Member"/> who created this playbook run group.
    /// </summary>
    public int? CreatedById { get; set; }
}

public record PlaybookRunGroupProperties
{
    /// <summary>
    /// Gets or inits the ID of the audit event for this run group.
    /// </summary>
    public Guid? RootAuditEventId { get; set; }

    /// <summary>
    /// Gets or inits the <see cref="DispatchType"/> used to dispatch this run group.
    /// </summary>
    public DispatchType DispatchType { get; init; }

    /// <summary>
    /// Gets or inits the <see cref="DispatchType"/> used to dispatch this run group.
    /// </summary>
    public DispatchSettings DispatchSettings { get; init; } = DispatchSettings.Default;

    /// <summary>
    /// Gets or sets the total number of dispatches performed for this run group.
    /// </summary>
    public int? TotalDispatchCount { get; set; }

    /// <summary>
    /// The ID of the trigger that started this playbook run.
    /// </summary>
    public string? Trigger { get; set; }

    /// <summary>
    /// The type of the trigger that started this playbook run.
    /// </summary>
    public string? TriggerType { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating if this run group has triggered a failure notification yet.
    /// </summary>
    public IDictionary<PlaybookRunOutcome, int> RunCountsByOutcome { get; set; } = new Dictionary<PlaybookRunOutcome, int>();
}
