using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Routing;

/// <summary>
/// Matches incoming messages with skill patterns.
/// </summary>
public interface ISkillPatternMatcher
{
    /// <summary>
    /// Applies the set of skill patterns in the system to the incoming message and returns all matching patterns.
    /// If two patterns match for the same skill, only returns the first one to
    /// match.
    /// </summary>
    /// <remarks>At the moment, priority order is determined by when the skill is created.</remarks>
    /// <param name="message">The incoming chat message.</param>
    /// <param name="fromMember">The <see cref="Member"/> the message is from.</param>
    /// <param name="organization">The current organization.</param>
    /// <returns>
    /// Returns a list of the <see cref="SkillPattern"/> instances that matches the message in priority order.
    /// </returns>
    Task<IReadOnlyList<SkillPattern>> GetMatchingPatternsAsync(
        IPatternMatchableMessage message,
        Member fromMember,
        Organization organization);
}
