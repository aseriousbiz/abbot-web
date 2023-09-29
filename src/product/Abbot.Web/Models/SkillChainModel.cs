using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

/// <summary>
/// Models a set of skill calls that are chained together through signals.
/// </summary>
public class SkillChainModel
{
    public SkillChainModel(Guid identifier, IEnumerable<AuditEventBase> relatedEvents)
    {
        Identifier = identifier;
        Items = IterateSignalChain(relatedEvents, identifier).Reverse();
    }

    public SkillChainModel()
    {
        Items = Enumerable.Empty<SignalChainItem>();
    }

    /// <summary>
    /// Identifier of the current audit event.
    /// </summary>
    public Guid Identifier { get; }

    public IEnumerable<SignalChainItem> Items { get; }

    static IEnumerable<SignalChainItem> IterateSignalChain(IEnumerable<AuditEventBase> relatedEvents, Guid currentIdentifier)
    {
        var events = relatedEvents.OfType<SkillRunAuditEvent>().ToList();
        // A chain includes the current skill, in which it's not a chain unless there are other skills involved.
        if (events is { Count: <= 1 })
        {
            return Enumerable.Empty<SignalChainItem>();
        }

        var chain = events.GetLineages(
                            e => e.Identifier,
                            e => e.ParentIdentifier)
                        .FirstOrDefault(l => l.Any(a => a.Identifier == currentIdentifier))
                    ?? Enumerable.Empty<SkillRunAuditEvent>();

        return chain.Select(e => new SignalChainItem(e));
    }
}
