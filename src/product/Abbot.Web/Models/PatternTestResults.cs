using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

/// <summary>
/// Provides information about the results of testing a message against a supplied pattern. Also returns
/// results for any other patterns that may have matched.
/// </summary>
public class PatternTestResults
{
    public PatternTestResults(bool matchesSuppliedPattern, IEnumerable<SkillPattern> patterns)
    {
        MatchesSuppliedPattern = matchesSuppliedPattern;
        MatchingPatterns = patterns.ToList();
    }

    /// <summary>
    /// Whether or not the message matched the supplied pattern.
    /// </summary>
    public bool MatchesSuppliedPattern { get; }

    /// <summary>
    /// The set of existing patterns matched by the message;
    /// </summary>
    public IReadOnlyList<SkillPattern> MatchingPatterns { get; }
}
