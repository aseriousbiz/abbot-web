using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Provides services for managing <see cref="Hub"/>s.
/// </summary>
public interface IHubRepository
{
    /// <summary>
    /// Gets the existing <see cref="Hub"/> associated with this room, if any.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> to get the associated <see cref="Hub"/> for.</param>
    /// <returns>The associated <see cref="Hub"/>, or <c>null</c> if there is no associated Hub.</returns>
    Task<Hub?> GetHubAsync(Room room);

    /// <summary>
    /// Gets an existing <see cref="Hub"/> by ID
    /// </summary>
    /// <param name="hubId">The ID of the <see cref="Hub"/> to retrieve.</param>
    /// <returns>The <see cref="Hub"/>, or <c>null</c> if there is no Hub with the specified ID.</returns>
    Task<Hub?> GetHubByIdAsync(Id<Hub> hubId);

    /// <summary>
    /// Gets all <see cref="Hub"/>s in the specified <see cref="Organization"/>.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to find <see cref="Hub"/>s in.</param>
    /// <returns>A list of all <see cref="Hub"/>s in the <see cref="Organization"/></returns>
    Task<IReadOnlyList<Hub>> GetAllHubsAsync(Organization organization);

    /// <summary>
    /// Creates a new <see cref="Hub"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="Hub"/></param>
    /// <param name="room">The <see cref="Room"/> associated with the <see cref="Hub"/></param>
    /// <param name="actor">The <see cref="Member"/> who is creating this <see cref="Hub"/></param>
    /// <returns>The created <see cref="Hub"/></returns>
    Task<Hub> CreateHubAsync(string name, Room room, Member actor);

    /// <summary>
    /// Deletes a <see cref="Hub"/>
    /// </summary>
    /// <param name="hub">The <see cref="Hub"/> to delete</param>
    /// <param name="actor">The <see cref="Member"/> performing this action.</param>
    Task DeleteHubAsync(Hub hub, Member actor);

    /// <summary>
    /// Gets a list of the <see cref="Room"/>s attached to the specified <see cref="Hub"/>.
    /// </summary>
    /// <param name="hub">The <see cref="Hub"/> to list attached <see cref="Room"/>s for.</param>
    /// <returns>A list of <see cref="Room"/>s</returns>
    Task<IReadOnlyList<Room>> GetAttachedRoomsAsync(Hub hub);

    /// <summary>
    /// Gets the default <see cref="Hub"/> for <paramref name="organization"/>,
    /// or <see langword="null" /> if not set.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/>.</param>
    /// <returns>The <see cref="Hub"/> or <see langword="null"/>.</returns>
    Task<Hub?> GetDefaultHubAsync(Organization organization);

    /// <summary>
    /// Sets <paramref name="hub"/> as the default <see cref="Hub"/> for its <see cref="Organization"/>.
    /// </summary>
    /// <param name="hub">The <see cref="Hub"/>.</param>
    /// <param name="actor">The actor.</param>
    /// <returns>The previous default <see cref="Hub"/>, or <paramref name="hub"/> if it is already the default.</returns>
    Task<Hub?> SetDefaultHubAsync(Hub hub, Member actor);

    /// <summary>
    /// Clears <see cref="OrganizationSettings.DefaultHubId"/> for <paramref name="organization"/>.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/>.</param>
    /// <param name="actor">The actor.</param>
    /// <returns>The unset <see cref="Hub"/> or <see langword="null"/>.</returns>
    Task<Hub?> ClearDefaultHubAsync(Organization organization, Member actor);
}
