using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// A repository for member facts. Member facts are little bits of info attached to a member of an organization
/// via the who skill.
/// </summary>
public interface IMemberFactRepository : IRepository<MemberFact>
{
    /// <summary>
    /// Retrieves facts about a member of an organization.
    /// </summary>
    /// <param name="member">The member these facts are about.</param>
    /// <returns>A list of facts about the member.</returns>
    Task<IReadOnlyList<MemberFact>> GetFactsAsync(Member member);
}
