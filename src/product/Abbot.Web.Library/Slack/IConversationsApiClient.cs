using System.Threading;
using Refit;

namespace Serious.Slack;

/// <summary>
/// Client for managing conversations in Slack.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/methods?filter=conversations"/>.
/// </remarks>
public interface IConversationsApiClient
{
    /// <summary>
    /// Lists all channels in a Slack team.
    /// </summary>
    /// <remarks>
    /// <see href="https://api.slack.com/methods/conversations.list"/>
    /// </remarks>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="types">Mix and match channel types by providing a comma-separated list of any combination of <c>public_channel</c>, <c>private_channel</c>, <c>mpim</c>, <c>im</c>. Default is <c>public_channel</c>.</param>
    /// <param name="limit">The number of conversations to return (default: 100), no larger than 1000.</param>
    /// <param name="teamId">The Slack team id to list conversations in, required if org token is used</param>
    /// <param name="excludeArchived">Set to <c>true</c> to exclude archived channels from the list</param>
    /// <param name="cursor">Used to paginate through a collection</param>
    [Get("/conversations.list")]
    Task<ConversationsResponse> GetConversationsAsync(
        [Authorize] string accessToken,
        int limit = 100,
        string? types = "public_channel",
        [AliasAs("team_id")]
        string? teamId = null,
        [AliasAs("exclude_archived")]
        bool excludeArchived = false,
        string? cursor = null);

    /// <summary>
    /// Retrieve information about a conversation.
    /// </summary>
    /// <remarks>
    /// <see href="https://api.slack.com/methods/conversations.info"/>
    /// </remarks>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">The Id of the channel to retrieve information about.</param>
    /// <param name="includeLocale">Whether or not to include the locale for the conversation.</param>
    /// <param name="includeMemberCount">Whether or not to include the number of members in the conversation.</param>
    [Get("/conversations.info")]
    Task<ConversationInfoResponse> GetConversationInfoAsync(
        [Authorize] string accessToken,
        string channel,
        [AliasAs("include_locale")]
        bool includeLocale = false,
        [AliasAs("include_num_members")]
        bool includeMemberCount = false);

    /// <summary>
    /// Opens or resumes a direct message or multi-person direct message.
    /// </summary>
    /// <remarks>
    /// <see href="https://api.slack.com/methods/conversations.open"/>
    /// </remarks>
    /// <returns></returns>
    [Post("/conversations.open")]
    Task<ConversationInfoResponse> OpenConversationAsync([Authorize] string accessToken, OpenConversationRequest request);

    /// <summary>
    /// Create a conversation.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="conversation">The conversation to create</param>
    [Post("/conversations.create")]
    Task<ConversationInfoResponse> CreateConversationAsync(
        [Authorize] string accessToken,
        ConversationCreateRequest conversation);

    /// <summary>
    /// Invites users to a channel.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="conversation">The conversation to create</param>
    [Post("/conversations.invite")]
    Task<ConversationInfoResponse> InviteUsersToConversationAsync(
        [Authorize] string accessToken,
        UsersInviteRequest conversation);

    /// <summary>
    /// Invites users to a shared slack connect channel.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">The channel to invite.</param>
    /// <param name="emails">An email address (yes, the name is plural but you can only pass one).</param>
    /// <param name="externalLimited">Optional boolean on whether invite is to a external limited member. Defaults to true.</param>
    /// <remarks>
    /// Even though the docs seems to suggest you can pass channel and other parameters via the body of the request,
    /// in practice it seems to only work if you pass it as a query parameter.
    /// </remarks>
    [Post("/conversations.inviteShared")]
    Task<SlackConnectInviteResponse> InviteToSlackConnectChannelViaEmailAsync(
        [Authorize] string accessToken,
        string channel,
        string emails,
        [AliasAs("external_limited")]
        bool externalLimited = true);

    /// <summary>
    /// Invites users to a shared slack connect channel.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">The channel to invite.</param>
    /// <param name="userIds">A slack user id (yes, the name is plural but you can only pass one).</param>
    /// <param name="externalLimited">Optional boolean on whether invite is to a external limited member. Defaults to true.</param>
    /// <remarks>
    /// Even though the docs seems to suggest you can pass channel and other parameters via the body of the request,
    /// in practice it seems to only work if you pass it as a query parameter.
    /// </remarks>
    [Post("/conversations.inviteShared")]
    Task<SlackConnectInviteResponse> InviteToSlackConnectChannelViaUserIdAsync(
        [Authorize] string accessToken,
        string channel,
        [AliasAs("user_ids")]
        string userIds,
        [AliasAs("external_limited")]
        bool externalLimited = true);

    /// <summary>
    /// Joins an existing conversation.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="request">The request which contains the conversation id to join.</param>
    [Post("/conversations.join")]
    Task<ConversationInfoResponse> JoinConversationAsync(
        [Authorize] string accessToken,
        ConversationJoinRequest request);

    /// <summary>
    /// Removes a user from a conversation.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="kickUserRequest">Information about the user and channel to remove the user from.</param>
    [Post("/conversations.kick")]
    Task<ApiResponse> KickUserFromConversationAsync([Authorize] string accessToken, KickUserRequest kickUserRequest);

    /// <summary>
    /// Archive a conversation.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="renameRequest">Information about the request to rename.</param>
    [Post("/conversations.rename")]
    Task<ConversationInfoResponse> RenameConversationAsync(
        [Authorize] string accessToken,
        RenameConversationRequest renameRequest);

    /// <summary>
    /// Archive a conversation.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">The conversation to archive</param>
    [Post("/conversations.archive")]
    Task<ApiResponse> ArchiveConversationAsync([Authorize] string accessToken, string channel);

    /// <summary>
    /// Unarchive a conversation.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="channel">The conversation to archive</param>
    [Post("/conversations.unarchive")]
    Task<ApiResponse> UnarchiveConversationAsync([Authorize] string accessToken, string channel);

    /// <summary>
    /// Sets the purpose for a conversation.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="purpose">The purpose to set.</param>
    [Post("/conversations.setPurpose")]
    Task<ApiResponse> SetConversationPurposeAsync([Authorize] string accessToken, PurposeRequest purpose);

    /// <summary>
    /// Sets the topic for a conversation.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="topic">The topic to set.</param>
    [Post("/conversations.setTopic")]
    Task<ApiResponse> SetConversationTopicAsync([Authorize] string accessToken, TopicRequest topic);

    /// <summary>
    /// Retrieve conversation history.
    /// </summary>
    /// <remarks>
    /// The most recent messages in the time range are returned first.
    /// </remarks>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">The channel for the message.</param>
    /// <param name="latest">Only messages before this Unix timestamp will be included in results. Default is the current time.</param>
    /// <param name="oldest">Only messages after this Unix timestamp will be included in results.</param>
    /// <param name="limit">The maximum number of items to return. Fewer than the requested number of items may be returned, even if the end of the users list hasn't been reached.</param>
    /// <param name="inclusive">Include messages with oldest or latest timestamps in results. Ignored unless either timestamp is specified.</param>
    /// <param name="includeAllMetadata">Return all metadata associated with this message.</param>
    /// <param name="cursor">Paginate through collections of data by setting the cursor parameter to a <c>next_cursor</c> attribute returned by a previous request's response_metadata. Default value fetches the first "page" of the collection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Get("/conversations.history")]
    Task<ConversationHistoryResponse> GetConversationHistoryAsync(
        [Authorize] string accessToken,
        string channel,
        string? latest = null,
        string? oldest = null,
        int limit = 100,
        bool inclusive = false,
        [AliasAs("include_all_metadata")]
        bool includeAllMetadata = false,
        string? cursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the replies to a message.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">The channel for the message.</param>
    /// <param name="ts">Unique identifier of either a thread’s parent message or a message in the thread. <c>ts</c> must be the timestamp of an existing message with 0 or more replies. If there are no replies then just the single message referenced by <c>ts</c> will return - it is just an ordinary, unthreaded message.</param>
    /// <param name="latest">Only messages before this Unix timestamp will be included in results. Default is the current time.</param>
    /// <param name="oldest">Only messages after this Unix timestamp will be included in results.</param>
    /// <param name="limit">The maximum number of items to return. Fewer than the requested number of items may be returned, even if the end of the users list hasn't been reached. Must be an integer no larger than 1000.</param>
    /// <param name="inclusive">Include messages with oldest or latest timestamps in results. Ignored unless either timestamp is specified.</param>
    /// <param name="cursor">Paginate through collections of data by setting the cursor parameter to a <c>next_cursor</c> attribute returned by a previous request's response_metadata. Default value fetches the first "page" of the collection.</param>
    [Get("/conversations.replies")]
    Task<ConversationHistoryResponse> GetConversationRepliesAsync(
        [Authorize] string accessToken,
        string channel,
        string ts,
        string? latest = null,
        string? oldest = null,
        int limit = 100,
        bool inclusive = false,
        string? cursor = null);

    /// <summary>
    /// Retrieves the members of a conversation.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">The channel for the message.</param>
    /// <param name="limit">The maximum number of items to return. Fewer than the requested number of items may be returned, even if the end of the users list hasn't been reached.</param>
    /// <param name="cursor">Paginate through collections of data by setting the cursor parameter to a <c>next_cursor</c> attribute returned by a previous request's response_metadata. Default value fetches the first "page" of the collection.</param>
    [Get("/conversations.members")]
    Task<ConversationMembersResponse> GetConversationMembersAsync(
        [Authorize] string accessToken,
        string channel,
        int limit = 100,
        string? cursor = null);
}
