using System.Collections.Generic;
using System.IO;
using System.Linq;
using Refit;
using Serious.Abbot.Extensions;
using Serious.Abbot.Messages;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack;

/// <summary>
/// Extensions to the <see cref="ISlackApiClient" />.
/// </summary>
public static class ApiExtensions
{
    /// <summary>
    /// Posts a message to a specified Slack channel.
    /// </summary>
    /// <param name="apiClient">The slack API client.</param>
    /// <param name="accessToken">The Slack API token.</param>
    /// <param name="channel">The channel to post to.</param>
    /// <param name="text">The text to post.</param>
    /// <returns>An <see cref="ApiResponse"/> with the results of the API call.</returns>
    public static Task<MessageResponse> PostMessageAsync(this ISlackApiClient apiClient,
        string accessToken,
        string channel,
        string text)
    {
        return apiClient.PostMessageWithRetryAsync(accessToken, new MessageRequest(channel, text));
    }

    /// <summary>
    /// Retrieve's information about a Slack Team via the "team.info"
    /// API endpoint. https://api.slack.com/methods/team.info
    /// </summary>
    /// <param name="apiClient">The slack API client.</param>
    /// <param name="teamId">The team ID to fetch information on. If omitted, retrieves information from the team to which the <paramref name="accessToken"/> is attached.</param>
    /// <param name="accessToken">The slack api access token.</param>
    public static async Task<TeamInfoWithScopesResponse> GetTeamInfoWithOAuthScopesAsync(
        this ISlackApiClient apiClient,
        string accessToken,
        string teamId)
    {
        var response = await GetWithScopesAsync(() => apiClient.GetTeamInfoAsync(accessToken, teamId));

        return new TeamInfoWithScopesResponse(response.ApiResponse, response.Scopes);
    }

    /// <summary>
    /// Gets information about the provided authentication token along with the Slack Scopes.
    /// </summary>
    /// <param name="apiClient">The slack API client.</param>
    /// <param name="accessToken">The slack api access token.</param>
    public static async Task<ApiResponseWithScopes<AuthTestResponse>> AuthTestWithScopesAsync(
        this ISlackApiClient apiClient,
        string accessToken)
    {
        return await GetWithScopesAsync(() => apiClient.AuthTestAsync(accessToken));
    }

    /// <summary>
    /// Gets information about the provided authentication token.
    /// </summary>
    /// <remarks>
    /// This is used in cases where we don't care about scopes. It's also here so I don't have to rewrite all the
    /// call sites to the old method on the Slack API.
    /// </remarks>
    /// <param name="apiClient">The slack API client.</param>
    /// <param name="accessToken">The slack api access token.</param>
    public static async Task<AuthTestResponse> TestAuthAsync(this ISlackApiClient apiClient, string accessToken)
    {
        var response = await GetWithScopesAsync(() => apiClient.AuthTestAsync(accessToken));
        return response.ApiResponse;
    }

    /// <summary>
    /// Calls an API message that returns <see cref="Refit.ApiResponse{T}"/> and packages the response content
    /// along with the Slack Scopes.
    /// </summary>
    /// <param name="apiCall">The API Call to make.</param>
    static async Task<ApiResponseWithScopes<TApiResponse>> GetWithScopesAsync<TApiResponse>(
        Func<Task<ApiResponse<TApiResponse>>> apiCall) where TApiResponse : ApiResponse
    {
        var response = await apiCall();
        if (response.Content is null || !response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to call the api.");
        }

        var scopes = response.Headers.TryGetValues("X-OAuth-Scopes", out var scopeHeaders)
            ? scopeHeaders.FirstOrDefault() ?? string.Empty
            : string.Empty;

        var info = response.Content;

        return new ApiResponseWithScopes<TApiResponse>(info, scopes);
    }

    /// <summary>
    /// Retrieve a single message using the timestamp and channel.
    /// </summary>
    /// <param name="apiClient">The Slack API client.</param>
    /// <param name="accessToken">The Slack API token.</param>
    /// <param name="channel">The channel for the message.</param>
    /// <param name="timestamp">The timestamp of the message to retrieve.</param>
    /// <returns>A single conversation or null.</returns>
    public static async Task<SlackMessage?> GetConversationAsync(
        this IConversationsApiClient apiClient,
        string accessToken,
        string channel,
        string timestamp)
    {
        var response = await apiClient.GetConversationHistoryAsync(
            accessToken,
            channel,
            timestamp,
            limit: 1,
            inclusive: true);

        if (TryGetMessageFromResponse(timestamp, response, out var message))
        {
            return message;
        }

        // Conversation History doesn't include replies, so we need to check here too.
        var repliesResponse = await apiClient.GetConversationRepliesAsync(
            accessToken,
            channel,
            timestamp,
            limit: 1,
            inclusive: true);

        return TryGetMessageFromResponse(timestamp, repliesResponse, out message)
            ? message
            : null;
    }

    /// <summary>
    /// Retrieves all of the members of a conversation. This may make multiple API calls in the case
    /// where the user is a member of more than 100 channels as this will follow the cursors to the end. If any request
    /// fails, this will fail and return the response of the first failed request.
    /// </summary>
    /// <param name="apiClient">The Slack API client.</param>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">The channel for the message.</param>
    public static async Task<ConversationMembersResponse> GetAllConversationMembersAsync(
        this IConversationsApiClient apiClient,
        string accessToken,
        string channel)
    {
        return await GetAllUsingCursorAsync<ConversationMembersResponse, string>(
            nextCursor => apiClient.GetConversationMembersAsync(
                accessToken,
                channel,
                1000,
                nextCursor));
    }

    /// <summary>
    /// Opens a new conversation with the provided users.
    /// </summary>
    /// <remarks>
    /// It's _possible_ to send to a single user by using their user ID as the channel, but it's not recommended.
    /// Only 'chat.postMessage' supports that, so you can't edit the message later.
    /// Instead, call this method to get a Direct Message Session ID (`D1234567890`) and use that as the channel.
    /// </remarks>
    /// <param name="apiClient">The Slack API client.</param>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="userIds">The Slack user IDs to include in the conversation.</param>
    /// <returns></returns>
    public static async Task<ConversationInfoResponse> OpenDirectMessageAsync(this IConversationsApiClient apiClient, string accessToken, IReadOnlyList<string> userIds)
    {
        return await apiClient.OpenConversationAsync(
            accessToken,
            OpenConversationRequest.FromUsers(userIds));
    }

    /// <summary>
    /// Lists all channels in a Slack team.
    /// This may make multiple API calls in the case where there are than 1000 channels, as this will follow the cursors to the end.
    /// If any request fails, this will fail and return the response of the first failed request.
    /// </summary>
    /// <param name="apiClient">The Slack API client.</param>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="types">Mix and match channel types by providing a comma-separated list of any combination of <c>public_channel</c>, <c>private_channel</c>, <c>mpim</c>, <c>im</c>. Default is <c>public_channel</c>.</param>
    /// <param name="teamId">The Slack team id to list conversations in, required if org token is used</param>
    /// <param name="excludeArchived">Set to <c>true</c> to exclude archived channels from the list</param>
    public static async Task<ConversationsResponse> GetAllConversationsAsync(
        this IConversationsApiClient apiClient,
        string accessToken,
        string? types = "public_channel",
        string? teamId = null,
        bool excludeArchived = false)
    {
        return await GetAllUsingCursorAsync<ConversationsResponse, ConversationInfoItem>(
            nextCursor => apiClient.GetConversationsAsync(
                accessToken,
                limit: 1000,
                types,
                teamId,
                excludeArchived,
                nextCursor));
    }

    /// <summary>
    /// List all conversations the calling (or specified) user may access.
    /// This may make multiple API calls in the case where the user is a member of more than 1000 channels as this will follow the cursors to the end.
    /// If any request fails, this will fail and return the response of the first failed request.
    /// </summary>
    /// <param name="apiClient">The Slack API client.</param>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="user">Browse conversations by a specific user ID's membership. Non-public channels are restricted to those where the calling user shares membership.</param>
    /// <param name="types">Mix and match channel types by providing a comma-separated list of any combination of <c>public_channel</c>, <c>private_channel</c>, <c>mpim</c>, <c>im</c>. Default is <c>public_channel</c>.</param>
    /// <param name="teamId">The Slack team id to list conversations in, required if org token is used</param>
    /// <param name="excludeArchived">Set to <c>true</c> to exclude archived channels from the list</param>
    public static async Task<ConversationsResponse> GetAllUsersConversationsAsync(
        this ISlackApiClient apiClient,
        string accessToken,
        string? user = null,
        string? types = "public_channel",
        string? teamId = null,
        bool excludeArchived = false)
    {
        return await GetAllUsingCursorAsync<ConversationsResponse, ConversationInfoItem>(
            nextCursor => apiClient.GetUsersConversationsAsync(
                accessToken,
                limit: 1000,
                user,
                types,
                teamId,
                excludeArchived,
                nextCursor));
    }

    /// <summary>
    /// Uploads an image to Slack.
    /// </summary>
    /// <param name="slackApiClient">A <see cref="ISlackApiClient"/> used to upload images.</param>
    /// <param name="apiToken">The api token.</param>
    /// <param name="imageUpload">Information about the image to upload.</param>
    /// <param name="initialComment">The initial comment.</param>
    /// <param name="channel">The channel to upload the file to.</param>
    /// <param name="threadTimestamp">The thread timestamp, if responding to a thread.</param>
    /// <returns>The URL of the uploaded image.</returns>
    public static async Task<FileResponse> UploadAttachmentImageAsync(
        this ISlackApiClient slackApiClient,
        string apiToken,
        ImageUpload imageUpload,
        string? initialComment,
        string channel,
        string? threadTimestamp)
    {
        var (imageBytes, title) = imageUpload;
        var imageType = imageBytes.ParseFileType();
        var fileName = title ?? "unnamed-file";

        var contentType = $"image/{imageType}";
        using var stream = new MemoryStream(imageBytes);
        var streamPart = new StreamPart(stream, fileName, contentType);

        return await slackApiClient.Files.UploadFileAsync(
            apiToken,
            streamPart,
            fileName,
            filetype: null,
            channels: channel,
            initialComment,
            threadTimestamp: threadTimestamp);
    }

    static async Task<TResponse> GetAllUsingCursorAsync<TResponse, TItem>(
        Func<string?, Task<TResponse>> responseGetter) where TResponse : InfoResponse<IReadOnlyList<TItem>>, new()
    {
        async Task<TResponse> BetterGetter(string? cursor)
        {
            if (cursor is { Length: > 0 })
            {
                try
                {
                    return await responseGetter(cursor);
                }
                catch (Exception)
                {
                    // TODO: Use something like Polly for retry policy. But for now, this is fine.
                    // TODO: Get a logger in here so we can log the exception to see if this ever happens.
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    return await responseGetter(cursor);
                }
            }

            return await responseGetter(cursor);
        }

        string? nextCursor = null;
        var cursors = new HashSet<string>();
        var conversations = new List<TItem>();
        while (true)
        {
            var response = await BetterGetter(nextCursor);

            if (!response.Ok || response.Body is null)
            {
                return new TResponse
                {
                    Ok = false, // We can't trust we got everything and might make bad decisions, so we fail.
                    Error = response.Error,
                };
            }

            conversations.AddRange(response.Body);
            if (nextCursor is { Length: > 0 })
            {
                cursors.Add(nextCursor);
            }
            nextCursor = response.ResponseMetadata?.NextCursor;
            if (nextCursor is not { Length: > 0 })
            {
                break;
            }
            // This should never happen, but I don't trust Slack enough to not have this here.
            if (cursors.Contains(nextCursor))
            {
                throw new InvalidOperationException($"Circular cursor detected. Cursors: {string.Join(", ", cursors)}");
            }
        }

        return new TResponse
        {
            Ok = true,
            Body = conversations
        };
    }

    static bool TryGetMessageFromResponse(
        string timestamp,
        ConversationHistoryResponse response,
        out SlackMessage? message)
    {
        // There's no direct way to request a single message. The best we can do is pass the timestamp as the
        // "latest" message (meaning Only messages before this Unix timestamp will be included in results), but with
        // the inclusive flag so we include the message we're looking for and a limit of 1.
        // However, if the message doesn't exist, the API will still return 1 message. The one that is most
        // recently before the one we're looking for.
        // That's why we check the timestamp here.
        if (response.Ok && response.Body.FirstOrDefault(m => m.Timestamp == timestamp) is { } found)
        {
            message = found;
            return true;
        }

        message = null;
        return false;
    }
}
