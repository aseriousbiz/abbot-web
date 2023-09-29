using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Routing;

public class SkillPatternMatcher : ISkillPatternMatcher
{
    readonly IPatternRepository _patternRepository;

    /// <summary>
    /// Constructs a <see cref="SkillPatternMatcher"/>.
    /// </summary>
    /// <param name="patternRepository">The pattern repository to load patterns from.</param>
    public SkillPatternMatcher(IPatternRepository patternRepository)
    {
        _patternRepository = patternRepository;
    }

    public async Task<IReadOnlyList<SkillPattern>> GetMatchingPatternsAsync(
        IPatternMatchableMessage message,
        Member fromMember,
        Organization organization)
    {
        var patterns = await _patternRepository.GetAllAsync(organization, enabledPatternsOnly: true);

        return patterns
            .Where(p => p.AllowExternalCallers || fromMember.OrganizationId == organization.Id)
            .Where(p => p.Match(message))
            .DistinctBy(p => p.SkillId) // Don't call same skill twice. Only accept first pattern for a skill.
            .ToList();
    }
}
