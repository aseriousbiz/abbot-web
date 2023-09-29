using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Slack;

namespace Serious.TestHelpers
{
    public class FakeReactionsApiClient : IReactionsApiClient
    {
        public IList<(string AccessToken, string Name, string Channel, string Timestamp)> AddedReactions { get; } =
            new List<(string AccessToken, string Name, string Channel, string Timestamp)>();
        public IList<(string AccessToken, string Name, string Channel, string Timestamp)> RemovedReactions { get; } =
            new List<(string AccessToken, string Name, string Channel, string Timestamp)>();

        public Task<ApiResponse> AddReactionAsync(string accessToken, string name, string channel, string timestamp)
        {
            AddedReactions.Add((accessToken, name, channel, timestamp));
            return Task.FromResult(new ApiResponse { Ok = true });
        }

        public Task<ApiResponse> RemoveReactionAsync(string accessToken, string name, string channel, string timestamp)
        {
            RemovedReactions.Add((accessToken, name, channel, timestamp));
            return Task.FromResult(new ApiResponse { Ok = true });
        }

        public Task<ReactionsResponse> GetMessageReactionsAsync(string accessToken, string? channel = null, string? timestamp = null)
        {
            throw new System.NotImplementedException();
        }
    }
}
