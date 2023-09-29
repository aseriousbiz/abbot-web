using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Integrations.HubSpot;

public interface IHubSpotResolver
{
    /// <summary>
    /// Resolves a HubSpot Contact for the provided <see cref="Member"/>.
    /// </summary>
    /// <param name="client">A <see cref="IHubSpotClient"/>.</param>
    /// <param name="integration">The HubSpot <see cref="Integration"/>.</param>
    /// <param name="member">The <see cref="Member"/> to resolve the identity for.</param>
    /// <returns>A <see cref="HubSpotContactLink"/> representing the linked identity, if one can be resolved.</returns>
    Task<HubSpotContactLink?> ResolveHubSpotContactAsync(
        IHubSpotClient client,
        Integration integration,
        Member member);
}

public class HubSpotResolver : IHubSpotResolver
{
    static readonly ILogger<HubSpotResolver> Log = ApplicationLoggerFactory.CreateLogger<HubSpotResolver>();
    readonly ILinkedIdentityRepository _linkedIdentityRepository;
    readonly ISlackApiClient _slackApiClient;

    public HubSpotResolver(
        ILinkedIdentityRepository linkedIdentityRepository,
        ISlackApiClient slackApiClient)
    {
        _linkedIdentityRepository = linkedIdentityRepository;
        _slackApiClient = slackApiClient;
    }

    public async Task<HubSpotContactLink?> ResolveHubSpotContactAsync(IHubSpotClient client, Integration integration, Member member)
    {
        // Try an already-linked HubSpot Contact
        var homeOrganization = integration.Organization;
        var linkedContact = await GetLinkedHubSpotContactAsync(client, integration, member);
        if (linkedContact is not null)
        {
            return linkedContact;
        }

        // Try to find an existing HubSpot Contact for the member
        var foundContact = await FindExistingHubSpotContactAsync(client, homeOrganization, member);
        if (foundContact is not null)
        {
            // Save this mapping for later
            var hubId = long.Parse(integration.ExternalId.Require(), CultureInfo.InvariantCulture);
            var link = new HubSpotContactLink(hubId, foundContact.Id);
            await _linkedIdentityRepository.LinkIdentityAsync(homeOrganization,
                member,
                LinkedIdentityType.HubSpot,
                link.ToString(),
                GetDisplayName(foundContact.Properties),
                new HubSpotContactMetadata());
            return link;
        }

        // That means:
        // 1. They have no email in their profile, OR
        // 2. They don't have a HubSpot account, OR
        // 3. Their HubSpot account is associated with a different email.

        // TODO: Create a Contact?

        return null;
    }

    /// <summary>
    /// Gets the HubSpot user linked to the specified Member if any.
    /// If the linked HubSpot user is not valid to create a ticket in the provided organization, it will not be returned.
    /// </summary>
    async Task<HubSpotContactLink?> GetLinkedHubSpotContactAsync(IHubSpotClient client, Integration integration, Member member)
    {
        // Check if we have a linked identity for them already
        var (identity, metadata) =
            await _linkedIdentityRepository.GetLinkedIdentityAsync<HubSpotContactMetadata>(
                integration.Organization,
                member,
                LinkedIdentityType.HubSpot);

        if (identity is not null)
        {
            // Sweet, they've already got a linked identity.
            if (HubSpotContactLink.Parse(identity.ExternalId) is not { } contactLink)
            {
                // But it's invalid.
                Log.InvalidContactLink(identity.ExternalId);
                return null;
            }

            // Fetch the contact from HubSpot before we try to use it.
            var contact = await client.SafelyGetContactAsync(contactLink.ContactId);
            if (contact is not null)
            {
                // Update cached metadata.
                if (identity.ExternalName is null)
                {
                    var displayName = GetDisplayName(contact.Properties);
                    identity.ExternalName = displayName;
                    await _linkedIdentityRepository.UpdateLinkedIdentityAsync(identity, metadata);
                }

                return contactLink;
            }
        }

        return null;
    }

    private static string GetDisplayName(IReadOnlyDictionary<string, string?> properties)
    {
        var firstName = properties.GetValueOrDefault("firstname");
        var lastName = properties.GetValueOrDefault("lastname");
        var displayName = $"{firstName} {lastName}".Trim();
        return displayName;
    }

    /// <summary>
    /// Searches for an existing HubSpot user for the specified member, if one exists AND is appropriate for creating a ticket in the provided organization.
    /// </summary>
    async Task<HubSpotSearchResult?> FindExistingHubSpotContactAsync(
        IHubSpotClient client,
        Organization homeOrganization,
        Member member)
    {
        var email = member.User.Email;
        if (email is not { Length: > 0 }
            && homeOrganization.TryGetUnprotectedApiToken(out var apiToken))
        {
            try
            {
                var response = await _slackApiClient.GetUserInfo(apiToken, member.User.PlatformUserId);
                if (response.Ok)
                {
                    email = response.Body.Profile.Email;
                }
                else
                {
                    Log.ErrorCallingSlackApi(response.Error);
                }
            }
            catch (Exception ex)
            {
                Log.ExceptionCallingSlackApi(ex);
            }
        }

        if (email is { Length: > 0 })
        {
            // Try to find a user from their profile email.
            try
            {
                var response = await client.SearchAsync("contacts", new("email", SearchOperator.EqualTo, email));
                if (response is { Results.Count: > 0 })
                {
                    var contact = response.Results.FirstOrDefault(u =>
                        string.Equals(u.Properties.GetValueOrDefault("email"), email, StringComparison.OrdinalIgnoreCase));
                    return contact;
                }
            }
            catch (Exception ex)
            {
                Log.ExceptionSearchingContacts(ex);
            }
        }

        return null;
    }
}

static partial class HubSpotResolverLoggingExtensions
{
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Error creating HubSpot contact.")]
    public static partial void
        ExceptionCreatingContact(this ILogger<HubSpotResolver> logger, Exception ex);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Error searching HubSpot contacts.")]
    public static partial void
        ExceptionSearchingContacts(this ILogger<HubSpotResolver> logger, Exception ex);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Invalid HubSpot contact link. ExternalId={LinkedIdentityExternalId}")]
    public static partial void
        InvalidContactLink(this ILogger<HubSpotResolver> logger, string? linkedIdentityExternalId);
}
