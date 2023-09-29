using System.Collections;
using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// The repository for memories stored via the `rem` command.
/// </summary>
public interface IMemoryRepository : IOrganizationScopedNamedEntityRepository<Memory>
{
    Task<IReadOnlyList<Memory>> SearchAsync(IReadOnlyList<string> terms, Organization organization);
}
