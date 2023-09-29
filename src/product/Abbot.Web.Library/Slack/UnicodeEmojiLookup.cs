using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack;

namespace Serious;

/// <summary>
/// Service used to look up an emoji, whether it be custom or Unicode.
/// </summary>
public interface IEmojiLookup
{
    /// <summary>
    /// Given an emoji name, returns the <see cref="Emoji"/> associated with it. If the emoji can be represented in
    /// unicode, then a <see cref="UnicodeEmoji"/> with the Html entity code is returned. If the emoji is a custom
    /// emoji, then <see cref="CustomEmoji"/> is returned containing the URL for the emoji. If the
    /// emoji is not found, then an <see cref="Emoji"/> with just the emoji name is returned.
    /// </summary>
    /// <param name="emojiName">The name of the emoji.</param>
    /// <param name="accessToken">The access token to use to look up the emoji.</param>
    /// <returns>A task containing the emoji.</returns>
    Task<Emoji> GetEmojiAsync(string emojiName, string accessToken);

    /// <summary>
    /// Given a search string, returns all emojis that contain the string.
    /// </summary>
    /// <param name="query">The search string.</param>
    /// <param name="currentValues">The current value(s).</param>
    /// <param name="limit">The number of results to return.</param>
    /// <param name="accessToken">The access token to use to look up the emoji.</param>
    Task<IReadOnlyList<Emoji>> SearchAsync(string? query, string[] currentValues, int limit, string accessToken);
}

/// <summary>
/// Wraps an <see cref="IEmojiClient" /> with a simpler interface. This exists so clients can replace this with a
/// caching implementation if they choose.
/// </summary>
public interface ICustomEmojiLookup
{
    /// <summary>
    /// Returns an <see cref="Emoji"/> given the emoji name.
    /// </summary>
    /// <remarks>
    /// Some custom emojis are aliases to unicode emojis. Hence this method returns a list of <see cref="Emoji"/>.
    /// </remarks>
    /// <param name="emojiName">The name of the emoji.</param>
    /// <param name="accessToken">The access token used to request the custom emoji list from the Slack API.</param>
    /// <returns></returns>
    Task<Emoji?> GetEmojiAsync(string emojiName, string accessToken);

    /// <summary>
    /// Retrieves all resolved custom emojis.
    /// </summary>
    /// <remarks>
    /// Some custom emojis are aliases to unicode emojis. Hence this method returns a list of <see cref="Emoji"/>.
    /// </remarks>
    /// <param name="unicodeEmojis">The set of unicode emojis.</param>
    /// <param name="accessToken">The access token used to request the custom emoji list from the Slack API.</param>
    /// <returns></returns>
    Task<IReadOnlyList<Emoji>> GetAllAsync(IReadOnlyDictionary<string, UnicodeEmoji> unicodeEmojis, string accessToken);
}

#pragma warning disable CA1812
class CustomEmojiLookup : ICustomEmojiLookup
{
    readonly IEmojiClient _emojiClient;

    public CustomEmojiLookup(IEmojiClient emojiClient)
    {
        _emojiClient = emojiClient;
    }

    public async Task<Emoji?> GetEmojiAsync(string emojiName, string accessToken)
    {
        var unicodeEmojis = await UnicodeEmojiLookup.GetAllAsync();
        var all = await GetAllAsync(unicodeEmojis, accessToken);
        return all.FirstOrDefault(x => x.Name == emojiName) ?? new Emoji(emojiName);
    }

    public async Task<IReadOnlyList<Emoji>> GetAllAsync(
        IReadOnlyDictionary<string, UnicodeEmoji> unicodeEmojis,
        string accessToken)
    {
        var results = await _emojiClient.GetCustomEmojiListAsync(accessToken);
        return results.ResolveAll(unicodeEmojis);
    }
}
#pragma warning restore CA1812

public class EmojiLookup : IEmojiLookup
{
    readonly ICustomEmojiLookup _emojiClient;

    public EmojiLookup(ICustomEmojiLookup emojiClient)
    {
        _emojiClient = emojiClient;
    }

    public async Task<Emoji> GetEmojiAsync(string emojiName, string accessToken)
    {
        return await UnicodeEmojiLookup.GetEmojiOrDefaultAsync(emojiName)
           ?? await _emojiClient.GetEmojiAsync(emojiName, accessToken)
           ?? new Emoji(emojiName);
    }

    public async Task<IReadOnlyList<Emoji>> SearchAsync(string? query, string[] currentValues, int limit, string accessToken)
    {
        var unicodeEmoji = await UnicodeEmojiLookup.GetAllAsync();
        var customEmoji = (await _emojiClient.GetAllAsync(unicodeEmoji, accessToken)).ToDictionary(e => e.Name);

        var currentEmojis = currentValues
            .Select(name =>
                unicodeEmoji.TryGetValue(name, out var u) ? u :
                customEmoji.TryGetValue(name, out var c) ? c :
                new Emoji(name))
            .ToList();

        if (query is not { Length: > 0 })
        {
            return currentEmojis;
        }

        // Limit the number of results to a reasonable number.
        limit = Math.Max(20, Math.Min(3, limit));

        // Concat the lists into a single list.

        int Score(string name) => name switch
        {
            _ when name == query => 3,
            _ when name.StartsWith(query, StringComparison.OrdinalIgnoreCase) => 2,
            _ when name.Contains(query, StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0,
        };

        return currentEmojis
            .Concat(unicodeEmoji.Values)
            .Concat(customEmoji.Values)
            .DistinctBy(e => e.Name)
            .Select(e => new { Emoji = e, Score = Score(e.Name) })
            .Where(e => e.Score > 0)
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.Emoji.Name)
            .Select(e => e.Emoji)
            .Take(limit)
            .ToList();
    }
}

public static class UnicodeEmojiLookup
{
    /// <summary>
    /// Returns the HTML entity for the given emoji name, if found, otherwise <c>null</c>.
    /// </summary>
    /// <param name="emojiName">The emoji name (without the : characters).</param>
    /// <returns></returns>
    public static async Task<UnicodeEmoji?> GetEmojiOrDefaultAsync(string emojiName)
    {
        var emojiDictionary = await GetAllAsync();
        return emojiDictionary.TryGetValue(emojiName, out var emoji)
            ? emoji
            : null;
    }

    /// <summary>
    /// Retrieves all the unicode-based emojis.
    /// </summary>
    /// <returns>A dictionary of unicode emojis.</returns>
    public static async Task<IReadOnlyDictionary<string, UnicodeEmoji>> GetAllAsync() => await LazyEmojiDictionary;

    static readonly AsyncLazy<IReadOnlyDictionary<string, UnicodeEmoji>> LazyEmojiDictionary = new(GetEmojiLookupAsync);

    static async Task<IReadOnlyDictionary<string, UnicodeEmoji>> GetEmojiLookupAsync()
    {
        const string resourceName = "Serious.Abbot.EmbeddedResources.emoji-names.json";

        var assembly = typeof(UnicodeEmojiLookup).Assembly;
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new FileNotFoundException($"Missing embedded resource: {resourceName}");
        }

        // this will take care of closing the underlying stream
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        var emojis = JsonConvert.DeserializeObject<EmojiItem[]>(json)
            ?? throw new InvalidOperationException("Could not deserialize the embedded resource");

        var dict = new Dictionary<string, UnicodeEmoji>();
        var str = new StringBuilder();
        foreach (var emoji in emojis)
        {
            str.Clear();
            var chars = emoji.Unified.Split('-');
            var parsed = true;
            foreach (var chr in chars)
            {
                if (int.TryParse(chr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
                {
                    var rune = new Rune(codePoint);
                    str.Append(rune.ToString());
                }
                else
                {
                    // Unlikely, but possible
                    parsed = false;
                }
            }

            if (!parsed)
            {
                // Skip this emoji if we can't parse it.
                continue;
            }

            foreach (var shortName in emoji.ShortNames)
            {
                dict[shortName] = new UnicodeEmoji(shortName, str.ToString()) { CanonicalName = emoji.ShortName };
            }
        }

        return dict;
    }


    // CREDIT: https://blog.stephencleary.com/2012/08/asynchronous-lazy-initialization.html

    /// <summary>
    /// Provides support for asynchronous lazy initialization. This type is fully threadsafe.
    /// </summary>
    /// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
    sealed class AsyncLazy<T>
    {
        /// <summary>
        /// The underlying lazy task.
        /// </summary>
        readonly Lazy<Task<T>> _instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="factory">The delegate that is invoked on a background thread to produce the value when it is needed.</param>
        public AsyncLazy(Func<T> factory)
        {
            _instance = new Lazy<Task<T>>(() => Task.Run(factory));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="factory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        public AsyncLazy(Func<Task<T>> factory)
        {
            _instance = new Lazy<Task<T>>(() => Task.Run(factory));
        }

        /// <summary>
        /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy&lt;T&gt;"/> to be await'ed.
        /// </summary>
        public TaskAwaiter<T> GetAwaiter()
        {
            return _instance.Value.GetAwaiter();
        }

        /// <summary>
        /// Starts the asynchronous initialization, if it has not already started.
        /// </summary>
        public void Start()
        {
            var unused = _instance.Value;
        }
    }
}

public record EmojiItem(
    [property: JsonProperty("name")]
    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonProperty("unified")]
    [property: JsonPropertyName("unified")]
    string Unified,

    [property: JsonProperty("short_name")]
    [property: JsonPropertyName("short_name")]
    string ShortName,

    [property: JsonProperty("short_names")]
    [property: JsonPropertyName("short_names")]
    IReadOnlyList<string> ShortNames);

