using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Slack;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Resolves and translates incoming Slack Entities to the corresponding
/// Abbot types. For example, an incoming Slack channel Id can be resolved
/// to a <see cref="Room"/>. This interface will do everything necessary
/// including creating records that don't exist.
/// </summary>
public interface ISlackResolver
{
    /// <summary>
    /// Resolves a Slack team Id to an <see cref="Organization"/>.
    /// </summary>
    /// <param name="teamId">The Slack team id.</param>
    /// <param name="currentOrganization"></param>
    /// <returns>An <see cref="Organization"/> or null if not found.</returns>
    Task<Organization> ResolveOrganizationAsync(string teamId, Organization currentOrganization);

    /// <summary>
    /// Resolves a Slack channel Id to a <see cref="Room"/>. If the <see cref="Room"/>
    /// doesn't exist, queries the Slack API to get information about the channel and
    /// creates the room.
    /// </summary>
    /// <param name="channelId">The Slack channel Id.</param>
    /// <param name="organization">The organization the room is in.</param>
    /// <param name="forceRefresh">A boolean indicating if the room should be refreshed from the Slack API even if it already exists.</param>
    /// <returns>The resolved <see cref="Room"/> or <c>null</c> if the room doesn't exist in Slack.</returns>
    Task<Room?> ResolveRoomAsync(string channelId, Organization organization, bool forceRefresh);

    /// <summary>
    /// Updates a <see cref="Room"/> based on the <see cref="ConversationInfo"/> returned by the Slack API.
    /// If <paramref name="room"/> is null, it creates the room.
    /// </summary>
    /// <param name="room">The room to update.</param>
    /// <param name="conversationInfo">Information about the channel from the Slack API.</param>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    Task<Room> UpdateFromConversationInfoAsync(
        Room? room,
        ConversationInfoItem conversationInfo,
        Organization organization);

    /// <summary>
    /// Creates or Updates a <see cref="Room"/> based on the <see cref="ConversationInfo"/> returned by the Slack API.
    /// If the <see cref="Room"/> doesn't exist, creates the room.
    /// </summary>
    /// <param name="conversationInfo">The Slack channel as returned by the slack api.</param>
    /// <param name="organization">The organization the room is in.</param>
    /// <returns>The resolved <see cref="Room"/> or <c>null</c> if the room doesn't exist in Slack.</returns>
    Task<Room?> ResolveAndUpdateRoomAsync(ConversationInfoItem conversationInfo, Organization organization);

    /// <summary>
    /// Resolves a set of Slack channel Ids to a set of <see cref="Room"/> instances. If the <see cref="Room"/>
    /// doesn't exist, queries the Slack API to get information about the channel and
    /// creates the room.
    /// </summary>
    /// <param name="channelIds">The Slack channel Ids.</param>
    /// <param name="organization">The organization the room is in.</param>
    /// <param name="forceRefresh">A boolean indicating if the room should be refreshed from the Slack API even if it already exists.</param>
    /// <returns>The resolved <see cref="Room"/> or <c>null</c> if the room doesn't exist in Slack.</returns>
    Task<IReadOnlyList<Room>> ResolveRoomsAsync(IEnumerable<string> channelIds, Organization organization, bool forceRefresh);

    /// <summary>
    /// Returns a <see cref="Member"/> for the specified Slack user Id. If the <see cref="User"/> doesn't exist,
    /// queries the Slack API to get information about the user and the user's organization and creates the necessary
    /// <see cref="User"/>, <see cref="Member"/>, and <see cref="Organization"/> (if needed).
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="organization">
    /// The organization for the current message. This might not match the user's organization.</param>
    /// <param name="forceRefresh">A boolean indicating if the member should be refreshed from the Slack API even if they already exist.</param>
    /// <returns>The resolved <see cref="Member"/> or <c>null</c> if the member doesn't exist in Slack.</returns>
    Task<Member?> ResolveMemberAsync(string userId, Organization organization, bool forceRefresh = false);

    /// <summary>
    /// Resolves a <see cref="InstallEvent"/> representing all the metadata needed to install Abbot into a Slack workspace.
    /// </summary>
    /// <param name="apiToken">The API token for the organization.</param>
    /// <param name="teamId">The ID of the Slack Team.</param>
    /// <param name="botId">The ID of the Abbot Slack Bot in the workspace.</param>
    /// <param name="appId">The ID of the Abbot Slack App in the workspace.</param>
    /// <param name="installer">The authenticated installer, if available.</param>
    /// <returns>A <see cref="InstallEvent"/> containing all the resolved metadata needed to install Abbot.</returns>
    /// <exception cref="InvalidOperationException">The installation could not proceed because the Slack API returned an error.</exception>
    Task<InstallEvent> ResolveInstallEventAsync(string apiToken, string teamId, string botId, string? appId, ClaimsPrincipal? installer = null);

    /// <summary>
    /// Resolves a <see cref="InstallEvent"/> representing all the metadata needed to install Abbot into a Slack workspace.
    /// </summary>
    /// <param name="oauthCode">The OAuth 'code' returned by Slack.</param>
    /// <param name="clientId">The OAuth client ID of the Abbot Slack App.</param>
    /// <param name="clientSecret">The OAuth client secret of the Abbot Slack App.</param>
    /// <param name="installer">The authenticated installer.</param>
    /// <returns>A <see cref="InstallEvent"/> containing all the resolved metadata needed to install Abbot.</returns>
    /// <exception cref="InvalidOperationException">The installation could not proceed because the Slack API returned an error.</exception>
    Task<InstallEvent> ResolveInstallEventFromOAuthResponseAsync(
        string oauthCode,
        string clientId,
        string clientSecret,
        ClaimsPrincipal installer);
}
