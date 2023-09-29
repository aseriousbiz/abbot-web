using System;
using System.Linq;

namespace Serious.Text;

public static class RankedMatcher
{
    public static int Match(string skill, string search)
    {
        if (search.Length == 0)
        {
            return 0;
        }

        if (skill.Equals(search, StringComparison.Ordinal))
        {
            return 10; // Exact match
        }

        if (skill.StartsWith(search, StringComparison.OrdinalIgnoreCase))
        {
            return 9;
        }

        var parts = skill.Split('-')
            .Where(p => p.Length > 0)
            .ToList();

        if (search.Length < 2 || parts.Count < 2)
        {
            return 0;
        }

        if (parts.Any(p => p.Equals(search, StringComparison.Ordinal)))
        {
            return 8;
        }

        var acronym = new string(parts.Select(p => p[0]).ToArray());
        if (acronym.Equals(search, StringComparison.Ordinal))
        {
            return 7;
        }

        return parts.Any(p => p.StartsWith(search, StringComparison.Ordinal))
            ? 6
            : 0;
    }
}
