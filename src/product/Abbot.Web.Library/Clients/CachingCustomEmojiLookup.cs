using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Refit;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Clients;

/// <summary>
/// This class is used to look up custom emojis in Slack. It does some caching to be a bit more efficient.
/// </summary>
public class CachingCustomEmojiLookup : ICustomEmojiLookup
{
    static readonly ILogger<CachingCustomEmojiLookup> Log = ApplicationLoggerFactory.CreateLogger<CachingCustomEmojiLookup>();

    readonly IEmojiClient _emojiClient;
    readonly IMemoryCache _memoryCache;
    readonly IClock _clock;

    public CachingCustomEmojiLookup(IEmojiClient emojiClient, IMemoryCache memoryCache, IClock clock)
    {
        _emojiClient = emojiClient;
        _memoryCache = memoryCache;
        _clock = clock;
    }

    /// <summary>
    /// Attempts to get the custom emoji for the given team based on the Access token. Uses an HMACSHA256
    /// hash of the access token as the cache key.
    /// </summary>
    /// <param name="emojiName">The emoji name.</param>
    /// <param name="accessToken">The access token.</param>
    public async Task<Emoji?> GetEmojiAsync(string emojiName, string accessToken)
    {
        var unicodeEmojis = await UnicodeEmojiLookup.GetAllAsync();
        var all = await GetAllAsync(unicodeEmojis, accessToken);
        return all.FirstOrDefault(x => x.Name == emojiName) ?? new Emoji(emojiName);
    }

    /// <summary>
    /// Attempts to get the set of custom emojis for the given team based on the Access token. Uses an HMACSHA256
    /// hash of the access token as the cache key.
    /// </summary>
    /// <param name="unicodeEmojis">The unicode emojis.</param>
    /// <param name="accessToken">The access token.</param>
    public async Task<IReadOnlyList<Emoji>> GetAllAsync(
        IReadOnlyDictionary<string, UnicodeEmoji> unicodeEmojis,
        string accessToken)
    {
        var cacheKey = accessToken.ComputeHMACSHA256Hash(WebConstants.EmojiCacheKeyHashSeed);
        var resolvedEmojis = await _memoryCache.GetOrCreateAsync(cacheKey, async cacheEntry => {
            try
            {
                var response = await _emojiClient.GetCustomEmojiListAsync(accessToken);

                if (response.Ok)
                {
                    cacheEntry.SlidingExpiration = TimeSpan.FromHours(1);
                    cacheEntry.Value = response;
                    return response.ResolveAll(unicodeEmojis);
                }
                Log.ErrorRetrievingCustomEmoji(response.Error);
            }
            catch (Exception e)
            {
                Log.ExceptionRetrievingCustomEmoji(e);
                if (e is ApiException { Headers.RetryAfter.Delta: { } retryDelta })
                {
                    cacheEntry.AbsoluteExpiration = _clock.UtcNow.Add(retryDelta);
                }
            }

            // If something goes wrong, we want to cache the empty response for a brief period of time
            // so we don't have to wait 2 hours for it to correct itself, but also don't want to hammer
            // the Slack API with requests in the case of an unrecoverable error. Over time, we can check
            // the logs to see if there are exceptions that we can either handle better or would cause us to
            // set an even larger expiration.
            cacheEntry.AbsoluteExpiration ??= _clock.UtcNow.AddMinutes(30);
            return Array.Empty<Emoji>();
        });

        Expect.NotNull(resolvedEmojis);

        return resolvedEmojis;
    }
}

public static partial class CachingCustomEmojiLookupLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Could not retrieve custom emoji.")]
    public static partial void ExceptionRetrievingCustomEmoji(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Could not retrieve custom emoji. {Error}")]
    public static partial void ErrorRetrievingCustomEmoji(this ILogger logger, string error);
}
