using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// The repository of <see cref="UserList"/>s.
/// </summary>
public interface IListRepository : IOrganizationScopedNamedEntityRepository<UserList>
{
    /// <summary>
    /// Add an entry to a list.
    /// </summary>
    /// <param name="list">The list to update.</param>
    /// <param name="content">The content to add to the list.</param>
    /// <param name="user">The user adding the content to the list.</param>
    Task<UserListEntry> AddEntryToList(UserList list, string content, User user);

    /// <summary>
    /// Removes an entry from a list.
    /// </summary>
    /// <param name="list">The list to update.</param>
    /// <param name="content">The content to remove from the list.</param>
    /// <param name="user">The user removing the content from the list.</param>
    /// <returns>True if the element was found and removed. False otherwise.</returns>
    Task<bool> RemovesEntryFromList(UserList list, string content, User user);
}
