using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Signals;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Entities;

/// <summary>
/// Skills can subscribe to signals by name and arguments. This entity represents a <see cref="ISignalEvent"/>
/// that a skill can subscribe to.
/// </summary>
public class SignalSubscription : EntityBase<SignalSubscription>, INamedEntity, IAuditableEntity
{
    static readonly ILogger<SignalSubscription> Log = ApplicationLoggerFactory.CreateLogger<SignalSubscription>();

    /// <summary>
    /// The <see cref="ISignalEvent.Name"/> to subscribe to. Must be stored as lowercase culture invariant.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// An optional pattern to match against <see cref="ISignalEvent.Arguments"/>. If not specified, then the signal
    /// is matched by <see cref="ISignalEvent.Name"/> only and ignores the arguments.
    /// </summary>
    public string? ArgumentsPattern { get; set; }

    /// <summary>
    /// Determines how the <see cref="ArgumentsPattern"/> is matched against the signal arguments.
    /// If <see cref="ArgumentsPattern"/> is null, this is ignored.
    /// </summary>
    [Column(TypeName = "text")]
    public PatternType ArgumentsPatternType { get; set; }

    /// <summary>
    /// Whether or not the <see cref="ArgumentsPattern"/> is case sensitive. By default, it is not.
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// The skill that is subscribed to this signal.
    /// </summary>
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// The Id of the skill that is subscribed to this signal.
    /// </summary>
    public int SkillId { get; set; }

    /// <summary>
    /// The <see cref="User"/> that created this subscription.
    /// </summary>
    public User Creator { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="User"/> that created this entity.
    /// </summary>
    public int CreatorId { get; set; }

    /// <summary>
    /// Creates an audit event for adding or removing a signal subscription.
    /// </summary>
    /// <param name="auditOperation">The type of audit event.</param>
    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        if (auditOperation is AuditOperation.Changed)
        {
            throw new ArgumentException("Signal subscriptions may only be added or removed, never changed.", nameof(auditOperation));
        }

        var description = auditOperation is AuditOperation.Created
            ? $"Subscribed {DescribeSubscription()}"
            : $"Unsubscribed {DescribeSubscription()}";

        return new SkillAuditEvent
        {
            EntityId = Id,
            Description = description,
            SkillId = SkillId,
            SkillName = Skill.Name,
            Language = Skill.Language
        };
    }

    /// <summary>
    /// Returns true if this subscription matches the signal arguments. Assumes the name has already been checked.
    /// </summary>
    /// <param name="arguments">The signal arguments.</param>
    public bool Match(string arguments)
    {
        if (ArgumentsPattern is null)
        {
            return true;
        }
        try
        {
            return ArgumentsPatternType.IsMatch(arguments, ArgumentsPattern, CaseSensitive);
        }
        catch (ArgumentException e)
        {
            // This should never happen because we validate patterns going in, but
            // just in case, let's be defensive about it.
            Log.ExceptionMatchingPattern(e, ArgumentsPattern, Id, SkillId, Skill.Organization.PlatformId);
            return false;
        }
    }

    string DescribeSubscription()
    {
        var action = ArgumentsPatternType is PatternType.None
            ? string.Empty
            : $" when arguments {ArgumentsPatternType.Humanize()} `{ArgumentsPattern}`";
        return $"skill `{Skill.Name}` to signal `{Name}`{action}.";
    }
}
