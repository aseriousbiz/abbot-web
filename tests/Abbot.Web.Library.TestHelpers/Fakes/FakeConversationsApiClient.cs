using Serious.Slack;
using Serious.Slack.InteractiveMessages;

namespace Serious.TestHelpers;

public class FakeConversationsApiClient : FakeClient, IConversationsApiClient
{
    public Task<ConversationsResponse> GetConversationsAsync(
        string accessToken,
        int limit = 100,
        string? types = "public_channel",
        string? teamId = null,
        bool excludeArchived = false,
        string? cursor = null)
    {
        throw new NotImplementedException();
    }

    public Task<ConversationInfoResponse> GetConversationInfoAsync(
        string accessToken,
        string channel,
        bool includeLocale = false,
        bool includeMemberCount = false)
    {
        return Task.FromResult(GetInfoResponse<ConversationInfoResponse, ConversationInfo>(accessToken, channel));
    }

    public Task<ConversationInfoResponse> OpenConversationAsync(string accessToken, OpenConversationRequest request)
    {
        var id = (request.Channel, request.Users) switch
        {
            ({ } channel, _) => channel,
            (_, { } users) => string.Join('-', users.Split(',')),
            _ => throw new ArgumentException("Either channel or users must be specified")
        };

        if (TryGetInfoResponse<ConversationInfoResponse, ConversationInfo>(accessToken, id, out var response))
        {
            return Task.FromResult(response);
        }

        return Task.FromResult(new ConversationInfoResponse
        {
            Ok = true,
            Body = new ConversationInfo
            {
                Id = id,
            }
        });
    }

    public Task<ConversationInfoResponse> CreateConversationAsync(string accessToken, ConversationCreateRequest conversation)
    {
        throw new NotImplementedException();
    }

    public Task<ConversationInfoResponse> InviteUsersToConversationAsync(string accessToken, UsersInviteRequest conversation)
    {
        throw new NotImplementedException();
    }

    public Task<SlackConnectInviteResponse> InviteToSlackConnectChannelViaEmailAsync(
        string accessToken,
        string channel,
        string emails,
        bool externalLimited = true)
    {
        throw new NotImplementedException();
    }

    public Task<SlackConnectInviteResponse> InviteToSlackConnectChannelViaUserIdAsync(
        string accessToken,
        string channel,
        string userIds,
        bool externalLimited = true)
    {
        throw new NotImplementedException();
    }

    public async Task<ConversationInfoResponse> JoinConversationAsync(string accessToken, ConversationJoinRequest request)
    {
        return GetInfoResponse<ConversationInfoResponse, ConversationInfo>(accessToken, request.Channel);
    }

    public Task<ApiResponse> KickUserFromConversationAsync(string accessToken, KickUserRequest kickUserRequest)
    {
        throw new NotImplementedException();
    }

    public Task<ConversationInfoResponse> RenameConversationAsync(string accessToken, RenameConversationRequest renameRequest)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> ArchiveConversationAsync(string accessToken, string channel)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UnarchiveConversationAsync(string accessToken, string channel)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SetConversationPurposeAsync(string accessToken, PurposeRequest purpose)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SetConversationTopicAsync(string accessToken, TopicRequest topic)
    {
        throw new NotImplementedException();
    }

    public Task<ConversationHistoryResponse> GetConversationHistoryAsync(
        string accessToken,
        string channel,
        string? latest = null,
        string? oldest = null,
        int limit = 100,
        bool inclusive = false,
        bool includeAllMetadata = false,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(channel, oldest);
        var response = GetInfoResponse<ConversationHistoryResponse, IReadOnlyList<SlackMessage>>(accessToken, key);

        if (limit == 1 && latest is not null && inclusive)
        {
            // Special case.
            response = new ConversationHistoryResponse
            {
                Ok = response.Ok,
                Body = response.Body?.Where(m => m.Timestamp == latest).ToList()
            };
        }

        return Task.FromResult(response);
    }

    public Task<ConversationHistoryResponse> GetConversationRepliesAsync(
        string accessToken,
        string channel,
        string ts,
        string? latest = null,
        string? oldest = null,
        int limit = 100,
        bool inclusive = false,
        string? cursor = null)
    {
        var response = GetInfoResponse<ConversationHistoryResponse, IReadOnlyList<SlackMessage>>(accessToken,
            $"{nameof(GetConversationRepliesAsync)}:{channel}:{ts}");

        if (response.Body is not null && (latest is not null || oldest is not null))
        {
            SlackTimestamp? latestTs = latest is not null
                ? SlackTimestamp.Parse(latest)
                : null;

            SlackTimestamp? oldestTs = oldest is not null
                ? SlackTimestamp.Parse(oldest)
                : null;

            var filtered = new List<SlackMessage>();
            foreach (var message in response.Body)
            {
                var currentTs = SlackTimestamp.Parse(message.Timestamp.Require());

                if (message.Timestamp != message.ThreadTimestamp && message.ThreadTimestamp != null) // Slack includes the root message among the replies.
                {
                    // Easy to understand is best here :)
                    if (oldestTs is not null)
                    {
                        if (currentTs < oldestTs)
                        {
                            continue;
                        }

                        if (!inclusive && currentTs == oldestTs)
                        {
                            continue;
                        }
                    }

                    if (latestTs is not null)
                    {
                        if (currentTs > latestTs)
                        {
                            continue;
                        }

                        if (!inclusive && currentTs == latestTs)
                        {
                            continue;
                        }
                    }
                }

                filtered.Add(message);
            }

            response = new ConversationHistoryResponse
            {
                Ok = response.Ok,
                Error = response.Error,
                HasMore = response.HasMore,
                PinCount = response.PinCount,
                ResponseMetadata = response.ResponseMetadata,
                Body = filtered
            };
        }

        return Task.FromResult(response);
    }

    public Task<ConversationMembersResponse> GetConversationMembersAsync(string accessToken, string channel, int limit = 100, string? cursor = null)
    {
        return Task.FromResult(GetInfoResponse<ConversationMembersResponse, IReadOnlyList<string>>(accessToken, channel));
    }

    public void AddConversationMembersResponse(string accessToken, string channel, IReadOnlyList<string> members)
    {
        AddInfoResponse<ConversationMembersResponse, IReadOnlyList<string>>(
            accessToken,
            channel,
            members);
    }

    public void AddConversationRepliesResponse(string accessToken, string channel, string ts, IReadOnlyList<SlackMessage> messages)
    {
        AddInfoResponse<ConversationHistoryResponse, IReadOnlyList<SlackMessage>>(
            accessToken,
            $"{nameof(GetConversationRepliesAsync)}:{channel}:{ts}",
            messages);
    }

    public void AddConversationHistoryResponse(string apiToken, string channel, IReadOnlyList<SlackMessage> messages)
    {
        var response = new ConversationHistoryResponse
        {
            Ok = true,
            Body = messages
        };
        var key = GetKey(channel, null);
        AddInfoResponse<ConversationHistoryResponse, IReadOnlyList<SlackMessage>>(apiToken, key, response);
    }

    public void AddConversationHistoryResponse(string apiToken, string channel, string? oldest, IReadOnlyList<SlackMessage> messages)
    {
        var response = new ConversationHistoryResponse
        {
            Ok = true,
            Body = messages
        };
        var key = GetKey(channel, oldest);
        AddInfoResponse<ConversationHistoryResponse, IReadOnlyList<SlackMessage>>(apiToken, key, response);
    }

    public void AddConversationHistoryResponse(string apiToken, string channel, string? oldest, string error)
    {
        var response = new ConversationHistoryResponse
        {
            Ok = false,
            Error = error,
        };
        var key = GetKey(channel, oldest);
        AddInfoResponse<ConversationHistoryResponse, IReadOnlyList<SlackMessage>>(apiToken, key, response);
    }

    public void AddConversationHistoryResponse(string apiToken, string channel, string? oldest, Exception exception)
    {
        AddInfoExceptionResponse<ConversationHistoryResponse>(apiToken, GetKey(channel, oldest), exception);
    }

    static string GetKey(string channel, string? oldest)
    {
        return $"channel:{channel},oldest:{oldest}";
    }

    public ConversationInfoResponse AddConversationInfoResponse(string apiToken, string id, string error)
    {
        var response = new ConversationInfoResponse
        {
            Ok = false,
            Error = error,
        };
        return AddInfoResponse<ConversationInfoResponse, ConversationInfo>(apiToken, id, response);
    }

    public ConversationInfoResponse AddConversationInfoResponse(string apiToken, ConversationInfo conversationInfo)
    {
        var response = new ConversationInfoResponse
        {
            Ok = true,
            Body = conversationInfo
        };
        return AddInfoResponse<ConversationInfoResponse, ConversationInfo>(apiToken, conversationInfo.Id, response);
    }
}
