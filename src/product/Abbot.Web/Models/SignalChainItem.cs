using System;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

public class SignalChainItem
{
    public SignalChainItem(SkillRunAuditEvent skillRunEvent)
    {
        Signal = skillRunEvent.Signal;
        Source = skillRunEvent switch
        {
            ScheduledTriggerRunEvent => "schedule",
            HttpTriggerRunEvent => "HTTP request", { PatternDescription: { Length: > 0 } } => "pattern",
            _ => "chat"
        };
        SourceSkill = skillRunEvent.SkillName;
        Identifier = skillRunEvent.Identifier;
    }

    public string Source { get; }

    public string? Signal { get; }

    public string SourceSkill { get; }

    public Guid Identifier { get; }
}
