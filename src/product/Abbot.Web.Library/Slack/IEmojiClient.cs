using System.Collections.Generic;
using System.Linq;
using Refit;
using Serious.Slack;

namespace Serious;

/// <summary>
/// A refit client used to retrieve custom emojis.
/// </summary>
public interface IEmojiClient
{
    /// <summary>
    /// Get the list of custom emojis.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    [Get("/emoji.list")]
    Task<EmojiListResponse> GetCustomEmojiListAsync([Authorize] string accessToken);
}

public static class EmojiListResponseExtensions
{
    /// <summary>
    /// Looks up the custom emoji for the given name, resolving aliases. An alias could resolve to a Unicode emoji, or
    /// to a custom emoji, or not be found at all.
    /// </summary>
    /// <param name="response">The custom emoji list response</param>
    /// <param name="emojiName">The emoji name.</param>
    /// <returns>A <see cref="CustomEmoji"/> if the custom emoji has an image URL. If the emoji resolves to a unicode emoji, then this is set to a <see cref="UnicodeEmoji"/>. Otherwise it's <c>null</c>.</returns>
    public static async Task<Emoji?> ResolveCustomEmojiAsync(this EmojiListResponse response, string emojiName)
    {
        if (!response.Ok || !response.Body.TryGetValue(emojiName, out var emojiValue))
        {
            return null;
        }

        var body = response.Body;

        const string aliasPrefix = "alias:";

        if (!emojiValue.StartsWith(aliasPrefix, StringComparison.Ordinal))
        {
            // If it's not an alias, then it's a custom emoji.
            return new CustomEmoji(emojiName, new Uri(emojiValue));
        }

        // It's an alias. it could be a custom emoji or a unicode emoji.
        var alias = emojiValue[aliasPrefix.Length..];

        return await UnicodeEmojiLookup.GetEmojiOrDefaultAsync(alias)
               ?? (body.TryGetValue(alias, out emojiValue)
                   ? new CustomEmoji(alias, new Uri(emojiValue)) // There are no aliases to aliases, so this has to be a URL.
                   : default(Emoji));
    }

    public static IReadOnlyList<Emoji> ResolveAll(
        this EmojiListResponse response,
        IReadOnlyDictionary<string, UnicodeEmoji> unicodeEmojis)
    {
        if (!response.Ok)
        {
            return Array.Empty<Emoji>();
        }
        return EnumerateEmoji(response.Body, unicodeEmojis).ToList();
    }

    static IEnumerable<Emoji> EnumerateEmoji(
        IReadOnlyDictionary<string, string> customEmojis,
        IReadOnlyDictionary<string, UnicodeEmoji> unicodeEmojis)
    {
        const string aliasPrefix = "alias:";

        foreach (var (emojiName, emojiValue) in customEmojis)
        {
            if (!emojiValue.StartsWith(aliasPrefix, StringComparison.Ordinal))
            {
                // If it's not an alias, then it's a custom emoji.
                yield return new CustomEmoji(emojiName, new Uri(emojiValue));
            }

            // It's an alias. it could be a custom emoji or a unicode emoji.
            var alias = emojiValue[aliasPrefix.Length..];

            var result = unicodeEmojis.GetValueOrDefault(alias) as Emoji // It was an alias to a unicode emoji.
                 ?? (customEmojis.TryGetValue(alias, out var custom)
                     ? new CustomEmoji(alias, new Uri(custom)) // It was an alias to another custom emoji.
                     : null /* This shouldn't happen, but just in case. */);

            if (result is not null)
            {
                yield return result;
            }
        }
    }
}
