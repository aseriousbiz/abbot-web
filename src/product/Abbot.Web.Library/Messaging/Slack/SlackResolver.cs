using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Slack;
using Serious.Tasks;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Resolves and translates incoming Slack Entities to the corresponding
/// Abbot types. For example, an incoming Slack channel Id can be resolved
/// to a <see cref="Room"/>. This interface will do everything necessary
/// including creating records that don't exist.
/// </summary>
public class SlackResolver : ISlackResolver
{
    static readonly ILogger<SlackResolver> Log = ApplicationLoggerFactory.CreateLogger<SlackResolver>();

    readonly ISlackApiClient _apiClient;
    readonly IOrganizationRepository _organizationRepository;
    readonly IUserRepository _userRepository;
    readonly IRoomRepository _roomRepository;
    readonly IUrlGenerator _urlGenerator;
    readonly IClock _clock;
    readonly IDataProtectionProvider _dataProtectionProvider;

    public SlackResolver(
        ISlackApiClient apiClient,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IRoomRepository roomRepository,
        IUrlGenerator urlGenerator,
        IClock clock,
        IDataProtectionProvider dataProtectionProvider)
    {
        _apiClient = apiClient;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _roomRepository = roomRepository;
        _urlGenerator = urlGenerator;
        _clock = clock;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public async Task<Room?> ResolveRoomAsync(string channelId, Organization organization, bool forceRefresh)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(channelId, organization);

        room = await UpdateRoomFromApiAsync(channelId, room, forceRefresh, organization);
        if (room is not null)
        {
            Log.ResolvedRoom(room.Name, room.Id, room.PlatformRoomId);
        }

        return room;
    }

    public async Task<Room?> ResolveAndUpdateRoomAsync(ConversationInfoItem conversationInfo, Organization organization)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(conversationInfo.Id, organization);
        return await UpdateFromConversationInfoAsync(room, conversationInfo, organization);
    }

    public async Task<IReadOnlyList<Room>> ResolveRoomsAsync(IEnumerable<string> channelIds, Organization organization, bool forceRefresh)
    {
        if (organization.PlatformType is not PlatformType.Slack)
        {
            throw new InvalidOperationException($"Cannot resolve Slack rooms for {organization.PlatformType} organization.");
        }

        var results = await _roomRepository.GetRoomsByPlatformRoomIdsAsync(
            channelIds,
            organization);

        // We can't do a `WhenAll` because we can't use the same DbContext in multiple threads at the same time.
        var rooms = await results
            .SelectFunc(r => UpdateRoomFromApiAsync(r.PlatformRoomId, r.Room, forceRefresh, organization))
            .WhenAllOneAtATimeAsync();

        return rooms.ToList();
    }

    async Task<Room?> UpdateRoomFromApiAsync(string channelId, Room? room, bool forceRefresh, Organization organization)
    {
        if (forceRefresh || room is null || room.NeedsPlatformUpdate())
        {
            var apiToken = organization.RequireAndRevealApiToken();

            // Query the Slack API.
            var conversationResponse = await _apiClient.Conversations.GetConversationInfoAsync(apiToken, channelId);
            if (!conversationResponse.Ok)
            {
                Log.ErrorRetrievingChannel(conversationResponse.ToString(), channelId);

                if (conversationResponse.Error is "channel_not_found")
                {
                    // Channel has been deleted.
                    // We don't delete the room record though.
                    if (room is not null)
                    {
                        room.Deleted = true;
                        room.LastPlatformUpdate = _clock.UtcNow;
                        await _roomRepository.UpdateAsync(room);
                    }

                    return room;
                }
                throw new InvalidOperationException($"Error retrieving room `{channelId}` for org `{organization.PlatformId}`\n {conversationResponse}");
            }

            return await UpdateFromConversationInfoAsync(room, conversationResponse.Body, organization);
        }

        return room;
    }

    public async Task<Room> UpdateFromConversationInfoAsync(
        Room? room,
        ConversationInfoItem conversationInfo,
        Organization organization)
    {
        var roomName = conversationInfo.Name;
        var channelId = conversationInfo.Id;

        RoomType type;
        if (conversationInfo.IsMultipartyInstantMessage)
        {
            type = RoomType.MultiPartyDirectMessage;
        }
        else if (conversationInfo.IsInstantMessage)
        {
            type = RoomType.DirectMessage;
        }
        else if (conversationInfo.IsGroup || (conversationInfo.IsPrivate && conversationInfo.IsChannel))
        {
            type = RoomType.PrivateChannel;
        }
        else
        {
            type = RoomType.PublicChannel;
        }

        // If the room is a DM, then <c>is_member</c> is not a property on the payload.
        // If Abbot wasn't in the DM, then it wouldn't even be able to resolve the room.
        bool? isMember = conversationInfo is not ConversationInfo
            ? null
            : conversationInfo is ConversationInfo { IsMember: true }
              || type is RoomType.DirectMessage or RoomType.MultiPartyDirectMessage;
        bool shared = conversationInfo.IsShared || conversationInfo.IsPendingExternallyShared;

        bool createRoom = false;
        if (room is null)
        {
            Log.CreatingRoom(channelId);
            room = new Room
            {
                PlatformRoomId = channelId,
                Organization = organization,
            };

            createRoom = true;
        }
        else
        {
            Log.UpdatingRoom(channelId, room.LastPlatformUpdate ?? DateTime.MinValue);
        }

        // Update metadata that could have changed.
        room.Name = roomName;
        room.RoomType = type;
        room.BotIsMember = isMember ?? room.BotIsMember;
        room.Archived = conversationInfo.IsArchived;
        room.Persistent = type.IsPersistent();
        room.LastPlatformUpdate = _clock.UtcNow;
        room.Deleted = false;
        room.Shared = shared;

        await (createRoom
            ? _roomRepository.CreateAsync(room)
            : _roomRepository.UpdateAsync(room));

        return room;
    }

    public async Task<Organization> ResolveOrganizationAsync(
        string teamId,
        Organization currentOrganization) =>
        await ResolveOrganizationAsync(teamId, null, currentOrganization);

    async Task<Organization> ResolveOrganizationAsync(
        string teamId,
        string? enterpriseId,
        Organization currentOrganization)
    {
        Organization? userOrganization = teamId.Equals(currentOrganization.PlatformId, StringComparison.Ordinal)
            ? currentOrganization
            : await _organizationRepository.GetAsync(teamId);

        if (userOrganization?.EnterpriseGridId is not null)
        {
            // Good news, the foreign user's organization exists in our database. We'll return it.
            return userOrganization;
        }

        // The organization doesn't exist, time to create it with information from the Slack API.
        if (userOrganization is null)
        {
            Log.OrganizationNull(teamId);
        }

        string? domain = null;
        string? avatar = null;
        string? name = null;
        string? slug = null;
        string? enterpriseGridId = enterpriseId ?? (SlackIdUtility.IsEnterpriseId(teamId) ? teamId : null);

        // Fetch information about the organization from the Slack API.
        // This seems to work just fine for Enterprise Ids.
        var apiToken = currentOrganization.RequireAndRevealApiToken();
        var response = await _apiClient.GetTeamInfoAsync(apiToken, teamId);
        if (response is { IsSuccessStatusCode: true, Content.Ok: true })
        {
            var teamInfo = response.Content.Body;
            name = teamInfo.Name;
            avatar = teamInfo.GetAvatar();
            slug = teamInfo.Domain;
            domain = teamInfo.GetHostName();

            // `teams.info` does not include the `enterprise_id` when making a request for a "foreign" org.
            // Instead, the `team` property is set to the enterprise Id rather than the Team Id passed in.
            // Hence we need to do this check here.
            enterpriseGridId = teamInfo.GetEnterpriseId();
        }

        if (userOrganization is not null)
        {
            // Only update the enterprise grid Id if we got it from the Slack API or it was passed in explicitly.
            // An empty value indicates there is no enterpriseGridId. A null value means we don't know yet.
#pragma warning disable CA1508
            if (enterpriseGridId is not null && userOrganization.EnterpriseGridId != enterpriseGridId)
#pragma warning restore CA1508
            {
                userOrganization.EnterpriseGridId = enterpriseGridId;
                await _organizationRepository.SaveChangesAsync();
                return userOrganization;
            }

            return userOrganization;
        }

        // Provision a new org to hold these foreign users.
        return await _organizationRepository.CreateOrganizationAsync(
            teamId,
            PlanType.None, // Foreign orgs have the "none" plan.
            name,
            domain,
            slug ?? teamId,
            avatar,
            enterpriseGridId);
    }

    public async Task<Organization> ResolveOrganizationForUserAsync(UserIdentifier userInfo, Organization currentOrganization)
    {
        var platformId = userInfo.TeamId ?? userInfo.EnterpriseId ?? userInfo.EnterpriseUser?.EnterpriseId;
        return await ResolveOrganizationAsync(platformId.Require(), userInfo.EnterpriseId, currentOrganization);
    }

    public async Task<Member?> ResolveMemberAsync(string userId, Organization organization, bool forceRefresh = false)
    {
        if (organization.PlatformType is not PlatformType.Slack)
        {
            throw new InvalidOperationException($"Cannot resolve Slack mention for {organization.PlatformType} organization.");
        }

        var user = await _userRepository.GetUserByPlatformUserId(userId);

        // Grab the member that matches the User's `SlackTeamId` or the member that matches the current organization.
        var member = user?.Members.SingleOrDefault(m => m.Organization.PlatformId == user.SlackTeamId)
            ?? user?.Members.SingleOrDefault(m => m.OrganizationId == organization.Id);

        if (user is not null && member is not null)
        {
            if (!forceRefresh)
            {
                // Ensure non-system Abbot is configured
                if (user.IsBot
                    && (!user.IsAbbot || user is not { NameIdentifier.Length: > 0 })
                    && user.PlatformUserId == organization.PlatformBotUserId)
                {
                    user.IsAbbot = true;
                    user.NameIdentifier = $"abbot|slack|{member.Organization.PlatformId}-{user.PlatformUserId}";
                    await _userRepository.UpdateUserAsync();
                }

                // We found the User and the correct Member!
                // But continue to force refresh if we don't know their RealName
                if (user.RealName is not null)
                {
                    return member;
                }
            }
        }

        // Query the Slack API and use that info to ensure a correct User and Member.
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            // Return what we have if the organization doesn't have an api token.
            Log.OrganizationHasNoSlackApiToken();
            return member;
        }
        var response = await _apiClient.GetUserInfo(apiToken, userId);
        if (!response.Ok || response.Body is { TeamId: null, EnterpriseId: null })
        {
            // Ignore malformed users.
            Log.ErrorCallingApi(userId, response.ToString());
            return member;
        }

        var userInfo = response.Body;
        var userOrganization = await ResolveOrganizationForUserAsync(userInfo, organization);
        var userEventPayload = UserEventPayload.FromSlackUserInfo(userInfo);
        return await (user is not null
            ? _userRepository.EnsureMemberAsync(user, userEventPayload, userOrganization)
            : _userRepository.EnsureAndUpdateMemberAsync(userEventPayload, userOrganization));
    }

    /// <inheritdoc />
    public async Task<InstallEvent> ResolveInstallEventFromOAuthResponseAsync(string oauthCode, string clientId, string clientSecret, ClaimsPrincipal installer)
    {
        var redirectUri = _urlGenerator.SlackInstallComplete().ToString();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        var exchangeResponse = await _apiClient.ExchangeOAuthCodeAsync(
            $"Basic {encoded}",
            redirectUri,
            oauthCode);

        if (!exchangeResponse.Ok)
        {
            throw new InvalidOperationException("Could not exchange OAuth code during installation. "
                + $"Error: {exchangeResponse.Error}."
                + "Response Metadata: "
                + $"{string.Join('\n', exchangeResponse.ResponseMetadata?.Messages ?? Array.Empty<string>())}");
        }

        // Not only test auth, but use it to get us the bot id
        var testResponse = await _apiClient.TestAuthAsync(exchangeResponse.AccessToken);
        if (!testResponse.Ok)
        {
            throw new InvalidOperationException("Access Token could not be used to authenticate. "
                + $"Error: {testResponse.Error}."
                + "Response Metadata: "
                + $"{string.Join('\n', testResponse.ResponseMetadata?.Messages ?? Array.Empty<string>())}");
        }

        return await ResolveInstallEventAsync(
            exchangeResponse.AccessToken,
            exchangeResponse.Team.Id,
            testResponse.BotId.Require(),
            exchangeResponse.AppId,
            installer);
    }

    /// <inheritdoc />
    public async Task<InstallEvent> ResolveInstallEventAsync(string apiToken, string teamId, string botId, string? appId, ClaimsPrincipal? installer)
    {
        var teamInfoResponse = await _apiClient.GetTeamInfoWithOAuthScopesAsync(apiToken, teamId);
        if (!teamInfoResponse.Ok)
        {
            // Install failed, let's not continue.
            throw new InvalidOperationException($"Could not get bot info for bot with id `{botId}` during "
                + $"installation. Error: {teamInfoResponse.Error}. Response Metadata: "
                + $"{string.Join('\n', teamInfoResponse.ResponseMetadata?.Messages ?? Array.Empty<string>())}");
        }

        var botInfo = await _apiClient.GetBotsInfoAsync(apiToken, botId);
        if (!botInfo.Ok)
        {
            // Install failed, let's not continue.
            throw new InvalidOperationException($"Could not get bot info for bot with id `{botId}` during "
                + $"installation. Error: {botInfo.Error}. Response Metadata: "
                + $"{string.Join('\n', botInfo.ResponseMetadata?.Messages ?? Array.Empty<string>())}");
        }

        var botAppName = botInfo.Body.Name;
        var botUserId = botInfo.Body.UserId;
        var botAvatar = botInfo.Body.Icons.Image72
            ?? botInfo.Body.Icons.Image48
            ?? botInfo.Body.Icons.Image36;

        // Every Bot App in Slack has an associated Bot User. So the Bot Name we care about
        // is the Bot User Name. Unfortunately we have to call the `users.info` API method
        // to get that name. It's not included in the `bots.info` API.
        var botUserInfoResponse = await _apiClient.GetUserInfo(apiToken, botUserId);
        if (!botUserInfoResponse.Ok)
        {
            // Install failed, let's not continue.
            throw new InvalidOperationException($"Could not get bot info for bot with id `{botId}` during "
                + $"installation. Error: {botUserInfoResponse.Error}. Response Metadata: "
                + $"{string.Join('\n', botUserInfoResponse.ResponseMetadata?.Messages ?? Array.Empty<string>())}");
        }

        var teamInfo = teamInfoResponse.Body;
        var botName = botUserInfoResponse.Body.RealName
            ?? botUserInfoResponse.Body.Name ?? "";
        var secretApiToken = new SecretString(apiToken, _dataProtectionProvider);
        return new InstallEvent(
            teamId,
            PlatformType.Slack,
            botId,
            botName,
            teamInfo.Name,
            teamInfo.Domain,
            secretApiToken,
            teamInfo.GetEnterpriseId(),
            teamInfo.GetHostName(),
            teamInfo.Scopes,
            botAppName,
            botAvatar,
            teamInfo.GetAvatar(),
            botUserId,
            appId,
            installer);
    }
}

static partial class SlackResolverLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Creating new room for {ChannelId}")]
    public static partial void CreatingRoom(this ILogger<SlackResolver> logger, string channelId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Updating metadata for {ChannelId} (last updated: {LastUpdated})")]
    public static partial void UpdatingRoom(this ILogger<SlackResolver> logger, string channelId, DateTime lastUpdated);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Resolved room {Name} with Id {Id} for {ChannelId}")]
    public static partial void ResolvedRoom(this ILogger<SlackResolver> logger, string? name, int id, string channelId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Error Retrieving Channel Error: `{Error}`, Channel: `{ChannelId}`")]
    public static partial void ErrorRetrievingChannel(this ILogger<SlackResolver> logger, string error, string channelId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "Error calling API to get info on user {PlatformUserId}, Response: {Response}")]
    public static partial void ErrorCallingApi(
        this ILogger<SlackResolver> logger,
        string platformUserId,
        string? response);


}
