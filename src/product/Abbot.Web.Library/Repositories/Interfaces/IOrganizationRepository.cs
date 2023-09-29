using System.Collections.Generic;
using System.Security.Claims;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Models;
using Serious.Slack;

namespace Serious.Abbot.Repositories;

/// <summary>
/// A result from call to ensuring an entity.
/// </summary>
/// <param name="Entity">The retrieved or created entity.</param>
/// <param name="IsNew">Whether the entity was created by this call.</param>
/// <typeparam name="TEntity">The entity type</typeparam>
public record EnsureResult<TEntity>(TEntity Entity, bool IsNew) where TEntity : IEntity;

/// <summary>
/// The repository for organizations.
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>
    /// Retrieves an organization by its Id.
    /// </summary>
    /// <param name="id">The Id of the organization.</param>
    Task<Organization?> GetAsync(int id);

    /// <summary>
    /// Retrieves an organization by its primary key.
    /// </summary>
    /// <param name="primaryKey">The primary key of the organization.</param>
    /// <returns></returns>
    Task<Organization?> GetAsync(Id<Organization> primaryKey);

    /// <summary>
    /// Attempts to retrieve an <see cref="Organization"/> associated with the authenticated user. If none is found,
    /// creates one.
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    Task<EnsureResult<Organization>> EnsureAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Retrieves an organization based on the platformId and <see cref="PlatformType"/>.
    /// </summary>
    /// <param name="platformId">The id of the organization on the chat platform.</param>
    /// <returns>Returns an <see cref="Organization"/> or <c>null</c> if not found.</returns>
    Task<Organization?> GetAsync(string platformId);

    /// <summary>
    /// Retrieves an organization based on the <see cref="Organization.StripeCustomerId"/>
    /// </summary>
    /// <param name="stripeCustomerId">The Stripe Customer ID to use to look up the organization</param>
    Task<Organization?> GetByStripeCustomerIdAsync(string stripeCustomerId);

    /// <summary>
    /// Retrieves all the organizations and all their C# skills for the purposes
    /// of garbage collection.
    /// </summary>
    Task<IReadOnlyList<Organization?>> GetAllForGarbageCollectionAsync();

    /// <summary>
    /// Retrieves all the organizations that need to be updated from the API
    /// </summary>
    /// <param name="daysSinceLastUpdate">Retrieves organizations that were last updated this many days ago or longer.</param>
    Task<IReadOnlyList<Organization>> GetOrganizationsToUpdateFromApiAsync(int daysSinceLastUpdate);

    /// <summary>
    /// Ensures that the rooms for an organization are loaded.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    Task<IReadOnlyList<Room>> EnsureRoomsLoadedAsync(Organization organization);

    /// <summary>
    /// Updates an organization with information from the logged in principal.
    /// </summary>
    /// <remarks>
    /// This is called when a user logs in during the OnTokenValidated event.
    /// </remarks>
    /// <param name="organization">The organization to update</param>
    /// <param name="principal">The logged in user</param>
    Task UpdateOrganizationAsync(Organization organization, ClaimsPrincipal principal);

    /// <summary>
    /// Updates an organization with information from a <see cref="TeamInfo" /> instance.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="teamInfo">Information about the team from the Slack API.</param>
    Task UpdateOrganizationAsync(Organization organization, TeamInfo teamInfo);

    /// <summary>
    /// Sets the override runner endpoint in the organization for a specific language.
    /// </summary>
    /// <param name="organization">The organization to update</param>
    /// <param name="codeLanguage">The language to update the runner endpoint for.</param>
    /// <param name="endpoint">The updated <see cref="SkillRunnerEndpoint"/>.</param>
    /// <param name="actor">The <see cref="Member"/> who ended the trial, if any.</param>
    Task SetOverrideRunnerEndpointAsync(Organization organization, CodeLanguage codeLanguage, SkillRunnerEndpoint endpoint, Member actor);

    /// <summary>
    /// Clears the override runner endpoint in the organization for a specific language.
    /// </summary>
    /// <param name="organization">The organization to update</param>
    /// <param name="codeLanguage">The language to clear the runner endpoint for.</param>
    /// <param name="actor">The <see cref="Member"/> who ended the trial, if any.</param>
    Task ClearOverrideRunnerEndpointAsync(Organization organization, CodeLanguage codeLanguage, Member actor);

    /// <summary>
    /// Creates or updates an existing organization when Abbot is installed to the organization's chat platform.
    /// </summary>
    /// <param name="installEvent">The event reported when Abbot is installed.</param>
    /// <returns>True if the organization was created. False if it existed and was updated.</returns>
    Task<Organization> InstallBotAsync(InstallEvent installEvent);

    /// <summary>
    /// Just saves any changes in the data context. TODO: This is hacky. Let's do better.
    /// </summary>
    Task SaveChangesAsync();

    /// <summary>
    /// Creates an organization with the specified values.
    /// </summary>
    /// <param name="platformId">The Id of the org on the chat platform.</param>
    /// <param name="plan">The plan the organization is on.</param>
    /// <param name="name">The name of the organization.</param>
    /// <param name="domain">The domain of the organization.</param>
    /// <param name="slug">The slug to use for the organization.</param>
    /// <param name="avatar">The url to the organization avatar.</param>
    /// <param name="enterpriseGridId">The Id of the Enterprise Grid organization if applicable.</param>
    /// <param name="onboardingState">The <see cref="OnboardingState"/> for the new organization. Defaults to <see cref="OnboardingState.Unactivated"/>.</param>
    Task<Organization> CreateOrganizationAsync(string platformId,
        PlanType plan,
        string? name,
        string? domain,
        string slug,
        string? avatar,
        string? enterpriseGridId = null,
        OnboardingState onboardingState = OnboardingState.Unactivated);

    /// <summary>
    /// Activates an organization if it had previously been inactive.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to activate.</param>
    /// <param name="actor">The <see cref="Member"/> who activated the organization.</param>
    /// <returns>True if the organization was activated by this call. False if it was already active.</returns>
    Task<bool> EnsureActivatedAsync(Organization organization, Member actor);

    /// <summary>
    /// Whether or not the organization has an existing member of the specified role
    /// </summary>
    /// <param name="organization">The organization</param>
    /// <param name="roleName">The name of the role to check</param>
    Task<bool> ContainsAtLeastOneUserInRoleAsync(Organization organization, string roleName);

    /// <summary>
    /// Retrieves the Abbot member for an organization.
    /// </summary>
    /// <param name="organization">The organization to retrieve the system member for.</param>
    /// <returns>The Abbot member.</returns>
    Task<Member> EnsureAbbotMember(Organization organization);

    /// <summary>
    /// Updates the plan for the organization, and associated any relevant billing metadata with the organization.
    /// Any trial period will be ended as well.
    /// </summary>
    /// <param name="organization">The organization to update.</param>
    /// <param name="newPlan">The new plan to put the organization on.</param>
    /// <param name="stripeCustomerId">The Stripe Customer ID that is associated with the organization. If <c>null</c>, the organization will be disassociated from a Stripe Customer.</param>
    /// <param name="stripeSubscriptionId">The Stripe Subscription ID that is associated with the organization. If <c>null</c>, the organization will be disassociated from a Stripe Subscription.</param>
    /// <param name="seatCount">If the plan type is Business, this represents the number of seats the plan has.</param>
    Task UpdatePlanAsync(
        Organization organization,
        PlanType newPlan,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        int seatCount);

    /// <summary>
    /// Gets a list of all <see cref="Organization"/>s that have a trial that has expired.
    /// </summary>
    /// <param name="nowUtc">The current time to use when evaluating trial expiry.</param>
    Task<IReadOnlyList<Organization>> GetExpiredTrialsAsync(DateTime nowUtc);

    /// <summary>
    /// Gets a list of all <see cref="Organization"/>s that have a trial that is about to expire in
    /// <paramref name="daysTillExpiration"/> days.
    /// </summary>
    /// <param name="nowUtc">The current time to use when evaluating trial expiry.</param>
    /// <param name="daysTillExpiration">The number of days till the trial expires.</param>
    Task<IReadOnlyList<Organization>> GetExpiringTrialsAsync(DateTime nowUtc, int daysTillExpiration);

    /// <summary>
    /// Begins a trial of a new plan for the organization.
    /// </summary>
    /// <returns>Returns the actor that started the trial, aka Abbot.</returns>
    /// <param name="organization">The <see cref="Organization"/> to begin the trial for.</param>
    /// <param name="trial">The <see cref="TrialPlan"/> to begin.</param>
    Task<Member> StartTrialAsync(Organization organization, TrialPlan trial);

    /// <summary>
    /// Extends the provided organization's trial access by the specified amount of time.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to end the trial for.</param>
    /// <param name="actor">The <see cref="Member"/> who ended the trial, if any.</param>
    /// <param name="extension">The amount of time to extend the trial.</param>
    /// <param name="reason">The reason the trial ended.</param>
    Task ExtendTrialAsync(Organization organization, TimeSpan extension, string reason, Member? actor);

    /// <summary>
    /// Ends the provided organization's trial access to a different plan and reverts them to their previous plan.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to end the trial for.</param>
    /// <param name="actor">The <see cref="Member"/> who ended the trial, if any.</param>
    /// <param name="reason">The reason the trial ended.</param>
    Task EndTrialAsync(Organization organization, string reason, Member? actor);

    /// <summary>
    /// Adds a <see cref="Member"/> in the <c>Agent</c> role to the organization's default first responders list.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/>.</param>
    /// <param name="subject">The <see cref="Member"/> to add as a default first responder.</param>
    /// <param name="actor">The <see cref="Member"/> who assigned the default first responder.</param>
    /// <returns><c>true</c> if the member is successfully added (and wasn't already in the list).</returns>
    Task<bool> AssignDefaultFirstResponderAsync(Organization organization, Member subject, Member actor);

    /// <summary>
    /// Removes a <see cref="Member"/> from the organization's default first responders list.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/>.</param>
    /// <param name="subject">The <see cref="Member"/> to add as a default first responder.</param>
    /// <param name="actor">The <see cref="Member"/> who assigned the default first responder.</param>
    /// <returns><c>true</c> if the member is successfully removed (and wasn't already removed).</returns>
    Task<bool> UnassignDefaultFirstResponderAsync(Organization organization, Member subject, Member actor);

    /// <summary>
    /// Adds a <see cref="Member"/> in the <c>Agent</c> role to the organization's default escalation responders list.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/>.</param>
    /// <param name="subject">The <see cref="Member"/> to add as a default first responder.</param>
    /// <param name="actor">The <see cref="Member"/> who assigned the default first responder.</param>
    /// <returns><c>true</c> if the member is successfully added (and wasn't already in the list).</returns>
    Task<bool> AssignDefaultEscalationResponderAsync(Organization organization, Member subject, Member actor);

    /// <summary>
    /// Removes a <see cref="Member"/> from the organization's default escalation responders list.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/>.</param>
    /// <param name="subject">The <see cref="Member"/> to add as a default first responder.</param>
    /// <param name="actor">The <see cref="Member"/> who assigned the default first responder.</param>
    /// <returns><c>true</c> if the member is successfully removed (and wasn't already removed).</returns>
    Task<bool> UnassignDefaultEscalationResponderAsync(Organization organization, Member subject, Member actor);

    /// <summary>
    /// Returns the count of active agents for this organization.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    Task<int> GetAgentCountAsync(Organization organization);

    /// <summary>
    /// Deletes the organization and all its associated data.
    /// </summary>
    /// <remarks>
    /// This deletes historical data, so use it sparingly.
    /// </remarks>
    /// <param name="platformId">The platform-specific Id of the organization to delete.</param>
    /// <param name="reason">The reason to delete the organization.</param>
    /// <param name="actor">The staff member that is deleting the org.</param>
    Task DeleteOrganizationAsync(string platformId, string reason, Member actor);

    /// <summary>
    /// Sets the AI settings for the organization and audits the change.
    /// </summary>
    /// <param name="aiEnhancementsEnabled">The new setting.</param>
    /// <param name="ignoreSocialConversations">If <c>true</c> and AI Enhancements are enabled, social messages do not create new conversations.</param>
    /// <param name="organization">The organization to change the setting for.</param>
    /// <param name="actor">The person doing the setting.</param>
    Task SetAISettingsWithAuditing(
        bool aiEnhancementsEnabled,
        bool ignoreSocialConversations,
        Organization organization,
        Member actor);

    /// <summary>
    /// Links the specified <see cref="Customer"/> in the A Serious Business tenant to the specified <see cref="Organization"/>.
    /// </summary>
    Task AssociateSeriousCustomerAsync(Organization organization, Customer customer, Member actor);

    /// <summary>
    /// Sets onboarding state for the organization.
    /// </summary>
    Task SetOnboardingStateAsync(Organization organization, OnboardingState state, Member actor);
}
