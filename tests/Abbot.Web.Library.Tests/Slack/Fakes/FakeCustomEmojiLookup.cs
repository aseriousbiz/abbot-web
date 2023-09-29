using Serious.Slack;

public class FakeCustomEmojiLookup : ICustomEmojiLookup
{
    readonly Dictionary<string, Dictionary<string, Emoji>> _responses = new();

    public void AddEmoji(string accessToken, Emoji emoji)
    {
        var emojis = GetAllForAccessToken(accessToken);
        emojis.Add(emoji.Name, emoji);
        _responses[accessToken] = emojis;
    }

    public Task<Emoji?> GetEmojiAsync(string emojiName, string accessToken)
    {
        var emojis = GetAllForAccessToken(accessToken);
        var result = emojis.TryGetValue(emojiName, out var emoji)
                ? emoji
                : null;

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Emoji>> GetAllAsync(
        IReadOnlyDictionary<string, UnicodeEmoji> unicodeEmojis,
        string accessToken)
    {
        var emojis = GetAllForAccessToken(accessToken);
        return Task.FromResult(emojis.Values.ToReadOnlyList());
    }

    Dictionary<string, Emoji> GetAllForAccessToken(string accessToken)
        => _responses.TryGetValue(accessToken, out var existing)
            ? existing
            : new Dictionary<string, Emoji>();
}
