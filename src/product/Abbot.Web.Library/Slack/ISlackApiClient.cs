using Refit;
using Serious.Abbot;

namespace Serious.Slack;

/// <summary>
/// A client used to call the Slack API.
/// </summary>
public interface ISlackApiClient
{
    public static Uri ApiUrl => new("https://slack.com/api");

    static T CreateClient<T>(Uri baseUri) => RestService.For<T>(baseUri.ToString(), SlackSerializer.RefitSettings);

    /// <summary>
    /// Client to read and write reactions.
    /// </summary>
    IReactionsApiClient Reactions => CreateClient<IReactionsApiClient>(ApiUrl);

    /// <summary>
    /// Client to manage conversations.
    /// </summary>
    IConversationsApiClient Conversations => CreateClient<IConversationsApiClient>(ApiUrl);

    /// <summary>
    /// Client to manage conversations.
    /// </summary>
    IFilesApiClient Files => CreateClient<IFilesApiClient>(ApiUrl);

    /// <summary>
    /// Client to read the list of custom emojis.
    /// </summary>
    IEmojiClient Emoji => CreateClient<IEmojiClient>(ApiUrl);

    /// <summary>
    /// Creates a client to delete and modify messages for a response URL.
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    IResponseUrlClient GetResponseUrlClient(Uri url) => CreateClient<IResponseUrlClient>(url);

    /// <summary>
    /// Gets information about a bot user.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="bot">The Bot Id. Example, B12345678.</param>
    [Get("/bots.info")]
    Task<BotInfoResponse> GetBotsInfoAsync([Authorize] string accessToken, string bot);

    /// <summary>
    /// Retrieve's information about a Slack Team via the "team.info"
    /// API endpoint. https://api.slack.com/methods/team.info
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="team">The team ID to fetch information on. If omitted, retrieves information from the team to which the <paramref name="accessToken"/> is attached.</param>
    [Get("/team.info")]
    Task<ApiResponse<TeamInfoResponse>> GetTeamInfoAsync([Authorize] string accessToken, string? team);

    /// <summary>
    /// Retrieve information about a Slack user via the "users.info" API endpoint.
    /// https://api.slack.com/methods/users.info
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="user">The slack user id</param>
    [Get("/users.info")]
    Task<UserInfoResponse> GetUserInfo([Authorize] string accessToken, string user);

    /// <summary>
    /// Find a user with an email address.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="email">The email address for the user.</param>
    [Get("/users.lookupByEmail")]
    Task<UserInfoResponse> LookupUserByEmailAsync([Authorize] string accessToken, string email);

    /// <summary>
    /// Retrieves a list of slack users.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="limit">The number of users to return (default: 200).</param>
    /// <param name="cursor">Used to paginate through a collection</param>
    [Get("/users.list")]
    Task<UserListResponse> GetUsersListAsync(
        [Authorize] string accessToken,
        int limit,
        string? cursor = null);

    /// <summary>
    /// List conversations the calling (or specified) user may access.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="user">Browse conversations by a specific user ID's membership. Non-public channels are restricted to those where the calling user shares membership.</param>
    /// <param name="types">Mix and match channel types by providing a comma-separated list of any combination of <c>public_channel</c>, <c>private_channel</c>, <c>mpim</c>, <c>im</c>. Default is <c>public_channel</c>.</param>
    /// <param name="limit">The number of conversations to return (default: 100), no larger than 1000.</param>
    /// <param name="teamId">The Slack team id to list conversations in, required if org token is used</param>
    /// <param name="excludeArchived">Set to <c>true</c> to exclude archived channels from the list</param>
    /// <param name="cursor">Used to paginate through a collection</param>
    [Get("/users.conversations")]
    Task<ConversationsResponse> GetUsersConversationsAsync(
        [Authorize] string accessToken,
        int limit = 100,
        string? user = null,
        string? types = "public_channel",
        [AliasAs("team_id")]
        string? teamId = null,
        [AliasAs("exclude_archived")]
        bool excludeArchived = false,
        string? cursor = null);

    /// <summary>
    /// Gets user profile details for a user, given their ID
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="user">The ID of the user to retrieve profile information for.</param>
    /// <param name="includeLabels">Include labels for each ID in custom profile fields. Using this parameter will heavily rate-limit your requests and is not recommended.</param>
    [Get("/users.profile.get")]
    Task<UserProfileResponse> GetUserProfileAsync(
        [Authorize] string accessToken,
        string? user = null,
        [AliasAs("include_labels")]
        bool includeLabels = false);

    /// <summary>
    /// Posts a message to the specified channel via the <c>chat.postMessage</c> endpoint.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="messageRequest">The message to send.</param>
    [Post("/chat.postMessage")]
    Task<MessageResponse> PostMessageAsync([Authorize] string accessToken, MessageRequest messageRequest);

    /// <inheritdoc cref="PostMessageAsync(string, MessageRequest)"/>
    Task<MessageResponse> PostMessageWithRetryAsync([Authorize] string accessToken, MessageRequest messageRequest) =>
        FaultHandler.RetryOnceAsync(() => PostMessageAsync(accessToken, messageRequest));

    /// <summary>
    /// Sends an ephemeral message to a user in a channel via the <c>chat.postEphemeral</c> endpoint.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="message">The message to send.</param>
    [Post("/chat.postEphemeral")]
    Task<EphemeralMessageResponse> PostEphemeralMessageAsync([Authorize] string accessToken, EphemeralMessageRequest message);

    /// <inheritdoc cref="PostEphemeralMessageAsync(string, EphemeralMessageRequest)"/>
    Task<EphemeralMessageResponse> PostEphemeralMessageWithRetryAsync([Authorize] string accessToken, EphemeralMessageRequest messageRequest) =>
        FaultHandler.RetryOnceAsync(() => PostEphemeralMessageAsync(accessToken, messageRequest));

    /// <summary>
    /// Updates an existing message via the <c>chat.update</c> endpoint.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="message">The message to send.</param>
    [Post("/chat.update")]
    Task<MessageResponse> UpdateMessageAsync([Authorize] string accessToken, MessageRequest message);

    /// <summary>
    /// Deletes a message.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="channel">Channel containing the message to be deleted.</param>
    /// <param name="ts">Timestamp of the message to be deleted.</param>
    [Post("/chat.delete")]
    Task<ApiResponse> DeleteMessageAsync([Authorize] string accessToken, string channel, string ts);

    /// <summary>
    /// Open a view for a user. Use this to open a modal with a user by exchanging a <c>trigger_id</c> received
    /// from another interaction. See the <see href="https://api.slack.com/block-kit/surfaces/modals">modals</see>
    /// documentation to learn how to obtain triggers from interactive components.
    /// <para>
    /// Within the view object, you can pass an <c>external_id</c> if you wish to use your own
    /// identifiers for views. The <c>external_id</c> should be a unique identifier of the view,
    /// determined by you. It must be unique for all views on a team, and it has a max length of 255 characters.
    /// </para>
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/views.open" /> for more info.
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="request">Information about the view to open.</param>
    [Post("/views.open")]
    Task<ViewResponse> OpenModalViewAsync([Authorize] string accessToken, OpenViewRequest request);

    /// <summary>
    /// Push a view onto the stack of a root view. Use this to Push a new view onto the existing view stack by
    /// passing a view object and a valid <c>trigger_id</c> generated from an interaction within the existing
    /// modal. The pushed view is added to the top of the stack, so the user will go back to the previous view
    /// after they complete or cancel the pushed view.
    /// <para>
    /// After a modal is opened, the app is limited to pushing 2 additional views.
    /// </para>
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/views.push" /> for more info.
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="request">Information about the modal to push.</param>
    [Post("/views.push")]
    Task<ViewResponse> PushModalViewAsync([Authorize] string accessToken, OpenViewRequest request);

    /// <summary>
    /// Update an existing view. Update a view by passing a new view definition object along with the
    /// <c>view_id</c> returned in views.open or the <c>external_id</c>. See the modals documentation
    /// to learn more about updating views and avoiding race conditions with the hash argument.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/views.update" /> for more info.
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="request">Information about the modal view to update.</param>
    [Post("/views.update")]
    Task<ViewResponse> UpdateModalViewAsync([Authorize] string accessToken, UpdateViewRequest request);

    /// <summary>
    /// Publish a static view for a User. Use this to create or update the view that
    /// comprises an app's Home tab for a specific user.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/views.publish" /> for more info.
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="request">Information about the view to publish and the user the view is for.</param>
    [Post("/views.publish")]
    Task<ViewResponse> PublishViewAsync([Authorize] string accessToken, PublishAppHomeRequest request);

    /// <summary>
    /// Gets information about the provided authentication token.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    [Post("/auth.test")]
    Task<ApiResponse<AuthTestResponse>> AuthTestAsync([Authorize] string accessToken);

    /// <summary>
    /// Exchanges a temporary OAuth code for an access token.
    /// </summary>
    /// <param name="authorizationHeader">The string 'Basic [encoded]' where '[encoded]' is the base64 encoding of '[clientId]:[clientSecret]'</param>
    /// <param name="redirectUri">The redirect uri passed in at the start of the oauth flow.</param>
    /// <param name="code">The temporary OAuth code.</param>
    [Post("/oauth.v2.access")]
    Task<OAuthExchangeResponse> ExchangeOAuthCodeAsync([Header("Authorization")] string authorizationHeader, [AliasAs("redirect_uri")] string redirectUri, string code);
}
