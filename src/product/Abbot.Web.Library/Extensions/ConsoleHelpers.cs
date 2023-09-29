using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Skills;

namespace Serious.Abbot.Extensions;

public static class ConsoleHelpers
{
    /// <summary>
    /// Used to parse mentions from a set of arguments. This is used by the Bot console and by the Abbot CLI.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="arguments">The set of arguments.</param>
    /// <param name="organization">The organization.</param>
    public static async Task<IReadOnlyList<Member>> ParseMentions(
        this IUserRepository userRepository,
        string arguments,
        Organization organization)
    {
        var potentialMentions = SkillPatterns.ParseMentions(arguments);

        var mentions = new List<Member>();
        foreach (var potentialMention in potentialMentions)
        {
            var mention = await userRepository.GetByPlatformUserIdAsync(potentialMention, organization);
            if (mention is not null)
            {
                mentions.Add(mention);
            }
        }

        return mentions;
    }
}
