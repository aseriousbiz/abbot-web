using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Collections;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository used to manage users and members of an organization.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a Slack <see cref="User"/> by their slack User Id with all the <see cref="Member"/> instances
    /// loaded. For most users, this will be a single <see cref="Member"/>, but for Enterprise Grid users, it could
    /// be more than one..
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the <see cref="Member"/> is missing, it's the caller's responsibility to create it.
    /// </para>
    /// </remarks>
    /// <param name="platformUserId">The Slack User Id.</param>
    /// <returns>A <see cref="User"/> for the specified Id.</returns>
    Task<User?> GetUserByPlatformUserId(string platformUserId);

    /// <summary>
    /// Retrieves a <see cref="Member"/> by their slack User Id and <see cref="Organization"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the <see cref="Member"/> is missing, it's the caller's responsibility to create it.
    /// </para>
    /// </remarks>
    /// <param name="platformUserId">The Slack User Id.</param>
    /// <param name="organization">The organization.</param>
    /// <returns>A <see cref="User"/> for the specified Id.</returns>
    Task<Member?> GetMemberByPlatformUserId(string platformUserId, Organization organization);

    /// <summary>
    /// Retrieves a member based on their authorization token.
    /// </summary>
    /// <param name="token">An API Key used by a member via the Abbot Command Line tool to call the Abbot API</param>
    /// <returns>Returns the <see cref="Member"/> with the specified API token.</returns>
    Task<Member?> GetMemberByApiToken(string token);

    /// <summary>
    /// Get user by billing email and organization. This is used when trying to determine the user that's taking
    /// an action in Stripe. Stripe sends us their email address, but it might or might not match up with an
    /// actual user. If none is found, returns our Abbot Billing user.
    /// </summary>
    /// <param name="email">The email of the user.</param>
    /// <param name="organization">The user's organization</param>
    Task<User> GetBillingUserByEmailAsync(string? email, Organization organization);

    /// <summary>
    /// Retrieves a user by member id.
    /// </summary>
    /// <param name="id">The Id of the member.</param>
    /// <param name="organizationId">The database Id of the organization the member is expected to be in.</param>
    /// <returns>The <see cref="Member"/> identified by the id.</returns>
    Task<Member?> GetMemberByIdAsync(Id<Member> id, Id<Organization> organizationId);

    /// <summary>
    /// Retrieves a user by member id, regardless of organization. This should only be used in places where it's fine
    /// that the <see cref="Member"/> is from another org such as when we explicitly pass the member Id to an API
    /// such as Hangfire and Mass Transit.
    /// </summary>
    /// <param name="id">The Id of the member.</param>
    /// <returns>The <see cref="Member"/> identified by the id.</returns>
    Task<Member?> GetMemberByIdAsync(Id<Member> id);

    /// <summary>
    /// Get <see cref="Member"/> by platform user id (such as U012345) and organization.
    /// </summary>
    /// <remarks>
    /// Use this method when building UI when you need to ensure the member is in the current
    /// organization.
    /// </remarks>
    /// <param name="platformUserId">The id of the user on the chat platform. For Slack, this is the Slack user id (ex. U0123456878)</param>
    /// <param name="organization">The user's organization</param>
    Task<Member?> GetByPlatformUserIdAsync(string platformUserId, Organization organization);

    /// <summary>
    /// Attempts to retrieve a single <see cref="Member"/> by the email address in their profile.
    /// </summary>
    /// <param name="organization">The organization the member belongs to.</param>
    /// <param name="email">The email address to look up</param>
    Task<Member?> GetMemberByEmailAsync(Organization organization, string email);

    /// <summary>
    /// Commits any outstanding changes to the database. Just calls SaveChangesAsync under the hood.
    /// </summary>
    Task UpdateUserAsync();

    /// <summary>
    /// Update working hours and days for a member.
    /// </summary>
    /// <param name="member">The <see cref="Member"/> to update.</param>
    /// <param name="workingHours">The updated working hours.</param>
    /// <param name="workingDays">The working days.</param>
    Task UpdateWorkingHoursAsync(
        Member member,
        WorkingHours workingHours,
        WorkingDays workingDays);

    /// <summary>
    /// Retrieve the current member based on the logged in principal.
    /// </summary>
    /// <param name="principal">The current logged in website user.</param>
    /// <exception cref="System.InvalidOperationException">Thrown if no user matches the current principal, or if the principal is missing the necessary claims to identify the user.</exception>
    Task<Member?> GetCurrentMemberAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Creates an API key for the member.
    /// </summary>
    /// <param name="name">The friendly name of the API Key</param>
    /// <param name="expiresInDays">The number of days the key expires in.</param>
    /// <param name="owner">The owner of the API Key</param>
    Task<ApiKey> CreateApiKeyAsync(string name, int expiresInDays, Member owner);

    /// <summary>
    /// Updates the token for an API key and resets the expiration.
    /// </summary>
    /// <param name="apiKey">The api key to regenerate</param>
    Task RegenerateApiKeyAsync(ApiKey apiKey);

    /// <summary>
    /// Deletes the API key.
    /// </summary>
    /// <param name="apiKey">The api key to delete</param>
    /// <param name="member">The member that owns the key</param>
    Task DeleteApiKeyAsync(ApiKey apiKey, Member member);

    /// <summary>
    /// Retrieves API keys for the provided member, and loads them in to the <see cref="Member.ApiKeys"/> property.
    /// </summary>
    /// <param name="member">The user to retrieve API keys for.</param>
    Task<IReadOnlyList<ApiKey>> GetApiKeysAsync(Member member);

    /// <summary>
    /// Retrieves the current <see cref="Member"/> along with their <see cref="Role"/> records based on the
    /// current logged in principal. If the member does not exist, creates one. This method always updates the
    /// <see cref="User.NameIdentifier"/>, <see cref="User.Email"/>, and <see cref="User.SlackTeamId"/> properties
    /// from the current principal claims.
    /// </summary>
    /// <remarks>
    /// This method is called in our Authentication handler when a user authenticates to
    /// the website.
    /// </remarks>
    /// <param name="principal">The current logged in website user.</param>
    /// <param name="organization">The organization the member belongs to.</param>
    Task<Member> EnsureCurrentMemberWithRolesAsync(ClaimsPrincipal principal, Organization organization);

    /// <summary>
    /// Ensures the <see cref="Member"/> exists for the specified <see cref="User"/> and <see cref="Organization"/>.
    /// If not, creates the <see cref="Member"/>. Then updates the <see cref="User"/> and <see cref="Member"/>
    /// from the specified <see cref="UserEventPayload"/>.
    /// </summary>
    /// <param name="userEventPayload">The <see cref="UserEventPayload"/> with information about a user change event coming from the chat platform.</param>
    /// <param name="user">The <see cref="User" /> to update the member for.</param>
    /// <param name="userOrganization">The organization the user belongs to.</param>
    Task<Member> EnsureMemberAsync(User? user, UserEventPayload userEventPayload, Organization userOrganization);

    /// <summary>
    /// Ensures the <see cref="User"/> and <see cref="Member"/> exists for the organization. If not, creates the
    /// <see cref="User"/> and <see cref="Member"/>. Then updates the <see cref="User"/> and <see cref="Member"/>
    /// from the specified <see cref="UserEventPayload"/>.
    /// </summary>
    /// <param name="userEventPayload">The <see cref="UserEventPayload"/> with information about a user change event coming from the chat platform.</param>
    /// <param name="userOrganization">The organization the user belongs to.</param>
    Task<Member> EnsureAndUpdateMemberAsync(UserEventPayload userEventPayload, Organization userOrganization);

    /// <summary>
    /// Archives a member by clearing their roles and setting them to inactive.
    /// </summary>
    /// <param name="subject">The member to archive.</param>
    /// <param name="actor">The <see cref="Member"/> that is archiving the subject.</param>
    Task ArchiveMemberAsync(Member subject, Member actor);

    /// <summary>
    /// Retrieves the set of users near a location.
    /// </summary>
    /// <param name="me">The user to exclude from the results.</param>
    /// <param name="location">The location.</param>
    /// <param name="radiusKilometers">The radius in kilometers around the location to query.</param>
    /// <param name="count">The number of users to return.</param>
    /// <param name="organization">The organization the users belong to.</param>
    Task<IPartialList<User>> GetUsersNearAsync(
        Member me,
        Point location,
        double radiusKilometers,
        int count,
        Organization organization);

    /// <summary>
    /// Finds members by a loose substring match on their name.
    /// </summary>
    /// <param name="organization">The organization the members belong to.</param>
    /// <param name="nameQuery">The name to search for.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="requiredRole">If specified, only members of this role are returned.</param>
    Task<IReadOnlyList<Member>> FindMembersAsync(
        Organization organization,
        string? nameQuery,
        int limit,
        string? requiredRole = null);

    /// <summary>
    /// Gets a list of members that match the type-ahead query.
    /// </summary>
    /// <param name="organization">The organization to fetch rooms for</param>
    /// <param name="memberNameFilter">The name to search for.</param>
    /// <param name="limit">The number of results to return.</param>
    /// <returns>A list of <see cref="Member"/>s that match the name filter.</returns>
    Task<IReadOnlyList<Member>> GetMembersForTypeAheadQueryAsync(
        Organization organization,
        string? memberNameFilter,
        int limit);

    /// <summary>
    /// Gets a queryable for active members of the organization.
    /// </summary>
    /// <remarks>
    /// An "active" member is a "home" member (see <see cref="GetHomeMembersQueryable"/>) who has logged in to Abbot.
    /// </remarks>
    /// <param name="organization">The organization the members belong to.</param>
    IQueryable<Member> GetActiveMembersQueryable(Organization organization);

    /// <summary>
    /// Gets a queryable for pending members of the organization.
    /// </summary>
    /// <param name="organization">The organization the members belong to.</param>
    IQueryable<Member> GetPendingMembersQueryable(Organization organization);

    /// <summary>
    /// Gets a queryable for archived members of the organization.
    /// </summary>
    /// <param name="organization">The organization the members belong to.</param>
    IQueryable<Member> GetArchivedMembersQueryable(Organization organization);

    /// <summary>
    /// Gets the Abbot system member for the specified organization.
    /// </summary>
    /// <param name="organization">The the organization to get the Abbot user for</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The <see cref="Member"/> representing Abbot for that organization.</returns>
    Task<Member> EnsureAbbotMemberAsync(Organization organization, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the Abbot user. If the user does not exist, creates it.
    /// </summary>
    Task<User> EnsureAbbotUserAsync();

    /// <summary>
    /// Retrieves the Billing user. If the user does not exist, creates it.
    /// </summary>
    Task<User> EnsureBillingUserAsync();

    /// <summary>
    /// Retrieve a <see cref="User"/> by their database Id.
    /// </summary>
    /// <remarks>
    /// This is used when we want to pass the "actor" to a consumer. The actor isn't always in the same org as where
    /// the action is happening. For example, if a staff user adds someone to a role, we want to pass the staff user.
    /// </remarks>
    /// <param name="id">The database Id for the user.</param>
    Task<User?> GetByIdAsync(Id<User> id);

    /// <summary>
    /// Retrieves the set of default responders. This requires that the <see cref="Member"/> is a member of the
    /// Agent role and has <see cref="Member.IsDefaultFirstResponder"/> set to <c>true</c>.
    /// </summary>
    /// <param name="organization">The organization to get the first responders for.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<IReadOnlyList<Member>> GetDefaultFirstRespondersAsync(Organization organization, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the set of default escalation responders. This requires that the <see cref="Member"/> is a member of
    /// the Agent role and has <see cref="Member.IsDefaultEscalationResponder"/> set to <c>true</c>.
    /// </summary>
    /// <param name="organization">The organization to get the first responders for.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<IReadOnlyList<Member>> GetDefaultEscalationRespondersAsync(Organization organization, CancellationToken cancellationToken = default);
}
