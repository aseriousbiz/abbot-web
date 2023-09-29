using System.Net;
using Refit;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.TestHelpers
{
    public class FakeSimpleSlackApiClient : FakeClient, ISlackApiClient
    {
        int _counter = 0;

        readonly IClock _clock;
        readonly List<MessageRequest> _postedMessages = new();
        readonly List<MessageRequest> _updatedMessages = new();
        readonly List<PublishAppHomeRequest> _postedAppHomes = new();

        IConversationsApiClient ISlackApiClient.Conversations => Conversations;
        public FakeConversationsApiClient Conversations { get; } = new();

        IEmojiClient ISlackApiClient.Emoji => Emoji;
        public FakeEmojiClient Emoji { get; } = new();

        IReactionsApiClient ISlackApiClient.Reactions => Reactions;
        public FakeReactionsApiClient Reactions { get; } = new();

        // NOTE: If we have to call this often, we'll set up a fake client.
        public IFilesApiClient Files { get; } = NSubstitute.Substitute.For<IFilesApiClient>();

        public FakeSimpleSlackApiClient() : this(new TimeTravelClock())
        {
        }

        public FakeSimpleSlackApiClient(IClock clock)
        {
            _clock = clock;
        }

        public Task<BotInfoResponse> GetBotsInfoAsync(string accessToken, string bot)
        {
            return Task.FromResult(GetInfoResponse<BotInfoResponse, BotInfo>(accessToken, bot));
        }

        public void AddTeamInfoHeader(string accessToken, string? team, string headerName, IEnumerable<string> headerValues)
        {
            AddInfoResponseHeaders<TeamInfoResponse, TeamInfo>(accessToken, team, headerName, headerValues);
        }

        public Task<ApiResponse<TeamInfoResponse>> GetTeamInfoAsync(string accessToken, string? team)
        {
            var teamInfoResponse = GetInfoResponse<TeamInfoResponse, TeamInfo>(accessToken, team);

            var key = (typeof(TeamInfoResponse), accessToken, team);

            var response = teamInfoResponse.Ok
                ? new ApiResponse<TeamInfoResponse>(
                    new HttpResponseMessage(HttpStatusCode.OK),
                    teamInfoResponse,
                    new RefitSettings())
                : new ApiResponse<TeamInfoResponse>(
                    new HttpResponseMessage(HttpStatusCode.InternalServerError),
                        null,
                        new RefitSettings());

            if (Headers.TryGetValue(key, out var headers))
            {
                foreach ((string? headerName, var headerValue) in headers)
                {
                    response.Headers.Add(headerName, headerValue);
                }
            }
            return Task.FromResult(response);
        }

        public Task<UserInfoResponse> GetUserInfo(string accessToken, string user)
        {
            return Task.FromResult(GetInfoResponse<UserInfoResponse, UserInfo>(accessToken, user));
        }

        public Task<UserInfoResponse> LookupUserByEmailAsync(string accessToken, string email)
        {
            throw new NotImplementedException();
        }

        public Task<UserListResponse> GetUsersListAsync(string accessToken, int limit, string? cursor)
        {
            return Task.FromResult(GetInfoResponse<UserListResponse, IReadOnlyList<UserInfo>>(accessToken, cursor));
        }

        public Task<ConversationsResponse> GetUsersConversationsAsync(
            string accessToken,
            int limit = 100,
            string? user = null,
            string? types = null,
            string? teamId = null,
            bool excludeArchived = false,
            string? cursor = null)
        {
            return Task.FromResult(GetInfoResponse<ConversationsResponse, IReadOnlyList<ConversationInfoItem>>(accessToken, user));
        }

        public Task<UserProfileResponse> GetUserProfileAsync(string accessToken, string? user = null, bool includeLabels = false)
        {
            return Task.FromResult(GetInfoResponse<UserProfileResponse, UserProfile>(accessToken, user));
        }

        public Task<MessageResponse> PostMessageAsync(string accessToken, MessageRequest message)
        {
            var response = TryGetInfoResponse<MessageResponse, MessageBody>(
                accessToken,
                message.Text,
                out var res)
                ? res
                : new MessageResponse
                {
                    Ok = true,
                    Body = new MessageBody
                    {
                        Text = message.Text,
                        Channel = message.Channel,
                        Blocks = message.Blocks ?? Array.Empty<ILayoutBlock>(),
                        Timestamp = new SlackTimestamp(_clock.UtcNow, $"{_postedMessages.Count + 1}:D6").ToString(),
                        ThreadTimestamp = message.ThreadTs,
                    },
                };

            if (message.Timestamp is not { Length: > 0 })
            {
                message.Timestamp = GenerateTimestamp();
            }

            _postedMessages.Add(message);

            if (response.Timestamp is not { Length: > 0 })
            {
                response.Timestamp = message.Timestamp;
            }
            return Task.FromResult(response);
        }

        public Task<EphemeralMessageResponse> PostEphemeralMessageAsync(string accessToken, EphemeralMessageRequest message)
        {
            var response = new EphemeralMessageResponse()
            {
                Ok = true,
                Body = "12345345.32423",
            };
            _postedMessages.Add(message);
            return Task.FromResult(response);
        }

        public IReadOnlyList<MessageRequest> PostedMessages => _postedMessages;

        public IReadOnlyList<MessageRequest> UpdatedMessages => _updatedMessages;

        public IReadOnlyList<PublishAppHomeRequest> PostedAppHomes => _postedAppHomes;

        public Task<MessageResponse> UpdateMessageAsync(string accessToken, MessageRequest message)
        {
            var response = TryGetInfoResponse<MessageResponse, MessageBody>(
                accessToken,
                message.Text,
                out var res)
                ? res
                : new MessageResponse
                {
                    Ok = true,
                    Body = new MessageBody
                    {
                        Text = message.Text,
                        Channel = message.Channel,
                        Blocks = message.Blocks ?? Array.Empty<ILayoutBlock>(),
                        Timestamp = new SlackTimestamp(_clock.UtcNow, $"{_postedMessages.Count + 1}:D6").ToString(),
                        ThreadTimestamp = message.ThreadTs,
                    },
                };
            _updatedMessages.Add(message);
            return Task.FromResult(response);
        }

        public Task<ApiResponse> DeleteMessageAsync(string accessToken, string channel, string ts)
        {
            throw new NotImplementedException();
        }

        public Task<ViewResponse> OpenModalViewAsync(string accessToken, OpenViewRequest request)
        {
            return Task.FromResult(new ViewResponse { Ok = true, Body = new ModalView() });
        }

        public Task<ViewResponse> PushModalViewAsync(string accessToken, OpenViewRequest request)
        {
            return Task.FromResult(new ViewResponse { Ok = true, Body = new ModalView() });
        }

        public Task<ViewResponse> UpdateModalViewAsync(string accessToken, UpdateViewRequest request)
        {
            return Task.FromResult(new ViewResponse { Ok = true, Body = new ModalView() });
        }

        public Task<ViewResponse> PublishViewAsync(string accessToken, PublishAppHomeRequest request)
        {
            _postedAppHomes.Add(request);
            return Task.FromResult(new ViewResponse { Ok = true, Body = new ModalView() });
        }

        public Task<ApiResponse<AuthTestResponse>> AuthTestAsync(string accessToken)
        {
            var response = new ApiResponse<AuthTestResponse>(
                new HttpResponseMessage(HttpStatusCode.OK),
                content: new AuthTestResponse()
                {
                    Ok = true
                },
                new RefitSettings());
            return Task.FromResult(response);
        }

        public Task<OAuthExchangeResponse> ExchangeOAuthCodeAsync(string authorizationHeader, string redirectUri, string code)
        {
            throw new NotImplementedException();
        }

        public void AddTeamInfo(string apiToken, string teamId, TeamInfo teamInfo)
        {
            AddInfoResponse<TeamInfoResponse, TeamInfo>(apiToken, teamId, teamInfo);
        }

        public void AddTeamInfoResponse(string apiToken, string teamId, TeamInfoResponse teamInfoResponse)
        {
            AddInfoResponse<TeamInfoResponse, TeamInfo>(apiToken, teamId, teamInfoResponse);
        }

        public void AddBotsInfo(string apiToken, string botId, BotInfo botInfo)
        {
            AddInfoResponse<BotInfoResponse, BotInfo>(apiToken, botId, botInfo);
        }

        public void AddUserList(string apiToken, string? cursor, string? nextCursor, IEnumerable<UserInfo> users)
        {
            var response = new UserListResponse
            {
                Ok = true,
                Body = users.ToList(),
                ResponseMetadata = new ResponseMetadata
                {
                    NextCursor = nextCursor
                }
            };

            AddInfoResponse<UserListResponse, IReadOnlyList<UserInfo>>(apiToken, cursor, response);
        }

        public void AddUserInfoResponse(string apiToken, UserInfo userInfo)
        {
            var response = new UserInfoResponse
            {
                Ok = true,
                Body = userInfo
            };
            AddInfoResponse<UserInfoResponse, UserInfo>(apiToken, userInfo.Id, response);
        }

        public void AddUserInfoResponse(string apiToken, string userId, UserInfoResponse response)
        {
            AddInfoResponse<UserInfoResponse, UserInfo>(apiToken, userId, response);
        }

        public void AddMessageInfoResponse(string apiToken, string? text, MessageResponse messageResponse)
        {
            AddInfoResponse<MessageResponse, MessageBody>(apiToken, text, messageResponse);
        }

        string GenerateTimestamp()
        {
            var next = Interlocked.Increment(ref _counter);
            return $"{next:D10}.000000";
        }
    }
}
