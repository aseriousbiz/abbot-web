using System.Threading.Tasks;
using Serious.Slack;

namespace Serious.TestHelpers;

public class FakeEmojiClient : IEmojiClient
{
    public Task<EmojiListResponse> GetCustomEmojiListAsync(string accessToken)
    {
        return Task.FromResult(new EmojiListResponse());
    }
}
