using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

public interface ILinkedIdentityRepository
{
    /// <summary>
    /// Retrieves the <see cref="LinkedIdentity"/> for the provided <see cref="Member"/>, of the provided <see cref="LinkedIdentityType"/>, if any.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> that owns this <see cref="LinkedIdentity"/>.</param>
    /// <param name="member">The <see cref="Member"/> to get the identity for.</param>
    /// <param name="type">The <see cref="LinkedIdentityType"/> representing the type of identity to find.</param>
    /// <returns>A <see cref="LinkedIdentity"/> representing the linked identity, if one exists.</returns>
    Task<(LinkedIdentity? Identity, T? Metadata)> GetLinkedIdentityAsync<T>(Organization organization, Member member, LinkedIdentityType type)
        where T : class;

    /// <summary>
    /// Looks up a <see cref="LinkedIdentity"/> using the ID from the linked system.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> that owns this <see cref="LinkedIdentity"/>.</param>
    /// <param name="type">The <see cref="LinkedIdentityType"/> representing the type of identity to find.</param>
    /// <param name="externalId">The ID of the linked identity in the external system.</param>
    /// <returns>A <see cref="LinkedIdentity"/> representing the linked identity, if one exists.</returns>
    Task<(LinkedIdentity? Identity, T? Metadata)> GetLinkedIdentityAsync<T>(
        Organization organization,
        LinkedIdentityType type,
        string externalId)
        where T : class;

    /// <summary>
    /// Links a new identity to the provided <see cref="Member"/>.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> that owns this <see cref="LinkedIdentity"/>.</param>
    /// <param name="member">The <see cref="Member"/> to add a linked identity for.</param>
    /// <param name="type">A <see cref="LinkedIdentityType"/> describing the type of the linked identity.</param>
    /// <param name="externalId">The ID of the linked identity.</param>
    /// <param name="externalName">The name or username associated with the external identity.</param>
    /// <param name="externalMetadata">External metadata that will be serialized as a JSON blob and stored with the identity.</param>
    /// <returns>A result with the new or conflicting <see cref="LinkedIdentity"/>.</returns>
    Task<EntityResult<LinkedIdentity>> LinkIdentityAsync(
        Organization organization,
        Member member,
        LinkedIdentityType type,
        string externalId,
        string? externalName = null,
        object? externalMetadata = null);

    /// <summary>
    /// Updates an existing <see cref="LinkedIdentity"/>
    /// </summary>
    /// <param name="identity">The <see cref="LinkedIdentity"/> to update.</param>
    /// <param name="updatedMetadata">Updated metadata to store with the identity. If <c>null</c>, the <see cref="LinkedIdentity.ExternalMetadata"/> property is left unchanged.</param>
    Task UpdateLinkedIdentityAsync(LinkedIdentity identity, object? updatedMetadata = null);

    /// <summary>
    /// Removes a <see cref="LinkedIdentity"/> from the <see cref="Member"/> to which it is attached.
    /// </summary>
    /// <param name="identity">The <see cref="LinkedIdentity"/> to remove</param>
    Task RemoveIdentityAsync(LinkedIdentity identity);

    /// <summary>
    /// Removes all linked identities for the integration. This is used when a change to the integration invalidates
    /// the linked identities.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> that owns this <see cref="LinkedIdentity"/>.</param>
    /// <param name="type">A <see cref="LinkedIdentityType"/> describing the type of the linked identity.</param>
    Task ClearIdentitiesAsync(Organization organization, LinkedIdentityType type);

    /// <summary>
    /// Retrieves all linked identities for the provided <see cref="Member"/> across ALL <see cref="Organization"/>s.
    /// </summary>
    /// <param name="subjectMember">The <see cref="Member"/> to retrieve linked identities for.</param>
    /// <returns>A list of <see cref="LinkedIdentity"/> objects representing the linked identities for the user.</returns>
    Task<IReadOnlyList<LinkedIdentity>> GetAllLinkedIdentitiesForMemberAsync(Member subjectMember);
}
