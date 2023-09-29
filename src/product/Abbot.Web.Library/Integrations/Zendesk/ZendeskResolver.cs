using System.Linq;
using Microsoft.Extensions.Logging;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Integrations.Zendesk;

public record struct SlackMessageAuthor(string DisplayName, string? AvatarUrl, Member? Member);

public interface IZendeskResolver
{
    /// <summary>
    /// Resolves a Zendesk user identity for the provided user.
    /// If no identity exists, one is created and saved as a <see cref="LinkedIdentity"/> for the user.
    /// </summary>
    /// <param name="client">A <see cref="IZendeskClient"/> configured for the Zendesk account in question.</param>
    /// <param name="homeOrganization">The home organization that is linked to the Zendesk account.</param>
    /// <param name="member">The <see cref="Member"/> to resolve the identity for.</param>
    /// <param name="zendeskOrganizationId">The ID of a zendesk organization ID to associate with the user.</param>
    /// <returns>A <see cref="ZendeskUserLink"/> representing the linked identity, if one can be resolved.</returns>
    Task<ZendeskUser?> ResolveZendeskIdentityAsync(
        IZendeskClient client,
        Organization homeOrganization,
        Member member,
        long? zendeskOrganizationId);

    /// <summary>
    /// Resolves Slack message author metadata for the provided Zendesk user.
    /// If a Slack user cannot be found, returns information based on the Zendesk user's profile.
    /// </summary>
    /// <param name="client">A <see cref="IZendeskClient"/> configured for the Zendesk account in question.</param>
    /// <param name="homeOrganization">The home organization that is linked to the Zendesk account.</param>
    /// <param name="zendeskUser">A <see cref="ZendeskUserLink"/> representing the Zendesk user identity.</param>
    /// <returns>A <see cref="SlackMessageAuthor"/> value containing information that can be used when posting a Slack message as this user.</returns>
    Task<SlackMessageAuthor> ResolveSlackMessageAuthorAsync(
        IZendeskClient client,
        Organization homeOrganization,
        ZendeskUserLink zendeskUser);
}

public class ZendeskResolver : IZendeskResolver
{
    static readonly ILogger<ZendeskResolver> Log = ApplicationLoggerFactory.CreateLogger<ZendeskResolver>();
    readonly ILinkedIdentityRepository _linkedIdentityRepository;
    readonly IUserRepository _userRepository;
    readonly ISlackApiClient _slackApiClient;

    public ZendeskResolver(
        ILinkedIdentityRepository linkedIdentityRepository,
        IUserRepository userRepository,
        ISlackApiClient slackApiClient)
    {
        _linkedIdentityRepository = linkedIdentityRepository;
        _userRepository = userRepository;
        _slackApiClient = slackApiClient;
    }

    public async Task<ZendeskUser?> ResolveZendeskIdentityAsync(
        IZendeskClient client,
        Organization homeOrganization,
        Member member,
        long? zendeskOrganizationId)
    {
        // Try an already-linked Zendesk User
        var linkedUser = await GetLinkedZendeskUserAsync(client, homeOrganization, member, zendeskOrganizationId);
        if (linkedUser is not null)
        {
            return linkedUser;
        }

        // Try to find an existing Zendesk User for the member
        var foundUser = await FindExistingZendeskUserAsync(client, homeOrganization, member);
        if (foundUser is not null)
        {
            // Save this mapping for later
            var link = ZendeskUserLink.Parse(foundUser.Url).Require();
            await _linkedIdentityRepository.LinkIdentityAsync(homeOrganization,
                member,
                LinkedIdentityType.Zendesk,
                link.ApiUrl.ToString(),
                foundUser.Name,
                ZendeskUserMetadata.FromUser(foundUser, link, isFacade: false));
            return foundUser;
        }

        // That means:
        // 1. They have no email in their profile, OR
        // 2. They don't have a Zendesk account, OR
        // 3. Their Zendesk account is associated with a different email.

        // So we create a facade.
        // The only case we'll kinda regret doing this for is case 3.
        // We can provide a way for users to link their Zendesk account manually to resolve this though.
        var facadeUser = await CreateAbbotFacadeUserAsync(client, homeOrganization, member, zendeskOrganizationId);
        if (facadeUser is not null)
        {
            // Save this mapping for later
            var link = ZendeskUserLink.Parse(facadeUser.Url).Require();
            await _linkedIdentityRepository.LinkIdentityAsync(homeOrganization,
                member,
                LinkedIdentityType.Zendesk,
                link.ApiUrl.ToString(),
                facadeUser.Name,
                ZendeskUserMetadata.FromUser(facadeUser, link, isFacade: true));
            return facadeUser;
        }

        return null;
    }

    public async Task<SlackMessageAuthor> ResolveSlackMessageAuthorAsync(
        IZendeskClient client,
        Organization homeOrganization,
        ZendeskUserLink zendeskUser)
    {
        if (zendeskUser.UserId < 0)
        {
            Log.InvalidUserLink(zendeskUser.ToString());
            return new("System", null, null);
        }

        // Let's try easy mode
        var (mappedIdentity, _) = await _linkedIdentityRepository.GetLinkedIdentityAsync<ZendeskUserMetadata>(
            homeOrganization,
            LinkedIdentityType.Zendesk,
            zendeskUser.ApiUrl.ToString());

        if (mappedIdentity is not null)
        {
            // Huzzah! We have a linked identity for this user
            return new(
                mappedIdentity.Member.User.DisplayName,
                mappedIdentity.Member.User.Avatar,
                mappedIdentity.Member);
        }

        // Ok, no luck. Let's start off by fetching Zendesk info for the user
        var result = await client.GetUserAsync(zendeskUser.UserId);
        var user = result.Body.Require();

        // Check if there's a home-org member with this email address
        var homeMember = await _userRepository.GetMemberByEmailAsync(homeOrganization, user.Email);
        if (homeMember is not null)
        {
            // There is! Save this link for later.
            await _linkedIdentityRepository.LinkIdentityAsync(
                homeOrganization,
                homeMember,
                LinkedIdentityType.Zendesk,
                zendeskUser.ApiUrl.ToString());

            return new(homeMember.User.DisplayName, homeMember.User.Avatar, homeMember);
        }

        // Ok, just use their Zendesk profile information
        // TODO: Zendesk also appears to default to using a Gravatar as the last fallback. We could too.
        return new(user.Name, user.RemotePhotoUrl ?? user.Photo?.ContentUrl, null);
    }

    static async Task<ZendeskUser?>
        CreateAbbotFacadeUserAsync(IZendeskClient client, Organization homeOrganization,
            Member member, long? organizationId)
    {
        // Create an Abbot Facade user
        var email =
            $"{member.User.PlatformUserId}@{member.Organization.Slug}.{member.Organization.PlatformId}.{member.Organization.PlatformType.ToString().ToLowerInvariant()}.{WebConstants.EmailDomain}";

        try
        {
            var response = await client.CreateOrUpdateUserAsync(new UserMessage
            {
                Body = new()
                {
                    Name = $"{member.DisplayName} (via {homeOrganization.BotName})",
                    Role = "end-user",
                    OrganizationId = organizationId,
                    ExternalId = $"Serious.Abbot:{member.User.PlatformUserId}",
                    Email = email,
                    Verified = true,
                }
            });

            return response.Body;
        }
        catch (ApiException ex) when (ex.TryGetErrorDetail(out var code, out var description, out _))
        {
            Log.ErrorCreatingFacadeUser(code, description);
        }
        catch (Exception ex)
        {
            Log.ExceptionCreatingFacadeUser(ex);
        }

        return null;
    }

    /// <summary>
    /// Gets the Zendesk user linked to the specified Member if any.
    /// If the linked Zendesk user is not valid to create a ticket in the provided organization, it will not be returned.
    /// </summary>
    async Task<ZendeskUser?> GetLinkedZendeskUserAsync(IZendeskClient client, Organization homeOrganization, Member member, long? ticketOrganizationId)
    {
        // Check if we have a linked identity for them already
        var (identity, metadata) =
            await _linkedIdentityRepository.GetLinkedIdentityAsync<ZendeskUserMetadata>(homeOrganization,
                member,
                LinkedIdentityType.Zendesk);

        if (identity is not null)
        {
            // Sweet, they've already got a linked identity.
            if (ZendeskUserLink.Parse(identity.ExternalId) is not { } userLink
                || userLink.UserId < 0)
            {
                // But it's invalid.
                Log.InvalidUserLink(identity.ExternalId);
                return null;
            }

            // Fetch the user from Zendesk before we try to use it.
            var user = (await client.GetUserAsync(userLink.UserId)).Body;
            if (user is not null)
            {
                // Update cached metadata.
                if (metadata is null || identity.ExternalName is null)
                {
                    identity.ExternalName = user.Name;
                    metadata = new ZendeskUserMetadata(user.Role, userLink.Subdomain);
                    await _linkedIdentityRepository.UpdateLinkedIdentityAsync(identity, metadata);
                }

                return user;
            }
        }

        return null;
    }

    /// <summary>
    /// Searches for an existing Zendesk user for the specified member, if one exists AND is appropriate for creating a ticket in the provided organization.
    /// </summary>
    async Task<ZendeskUser?> FindExistingZendeskUserAsync(
        IZendeskClient client,
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
            // Home user with known email
            // Try to find a user from their profile email.
            try
            {
                var response = await client.SearchUsersAsync($"email:{email}", null);
                if (response is { Body.Count: > 0 })
                {
                    var user = response.Body.FirstOrDefault(u =>
                        string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
                    return user;
                }
            }
            catch (ApiException ex) when (ex.TryGetErrorDetail(out var code, out var description, out _))
            {
                Log.ErrorSearchingUsers(code, description);
            }
            catch (Exception ex)
            {
                Log.ExceptionSearchingUsers(ex);
            }
        }

        return null;
    }
}

static partial class ZendeskResolverLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Error creating Zendesk façade user: {ZendeskErrorCode} - {ZendeskErrorDetail}")]
    public static partial void
        ErrorCreatingFacadeUser(this ILogger<ZendeskResolver> logger, string? zendeskErrorCode,
            string? zendeskErrorDetail);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Error creating Zendesk façade user.")]
    public static partial void
        ExceptionCreatingFacadeUser(this ILogger<ZendeskResolver> logger, Exception ex);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Error searching Zendesk users: {ZendeskErrorCode} - {ZendeskErrorDetail}")]
    public static partial void
        ErrorSearchingUsers(this ILogger<ZendeskResolver> logger, string? zendeskErrorCode, string? zendeskErrorDetail);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Error searching Zendesk users.")]
    public static partial void
        ExceptionSearchingUsers(this ILogger<ZendeskResolver> logger, Exception ex);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Invalid Zendesk user link. ExternalId={LinkedIdentityExternalId}")]
    public static partial void
        InvalidUserLink(this ILogger<ZendeskResolver> logger, string? linkedIdentityExternalId);
}
