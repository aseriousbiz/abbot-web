using Serious.Slack;

namespace Serious.TestHelpers;

public class FakeEmojiLookup : IEmojiLookup
{
    public Task<Emoji> GetEmojiAsync(string emojiName, string accessToken)
    {
        return Task.FromResult(new Emoji(emojiName));
    }

    public Task<IReadOnlyList<Emoji>> SearchAsync(string? query, string[] currentValues, int limit, string accessToken)
    {
        return Task.FromResult(Array.Empty<Emoji>() as IReadOnlyList<Emoji>);
    }
}
