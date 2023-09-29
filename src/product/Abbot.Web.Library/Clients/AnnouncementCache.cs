using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Clients;

/// <summary>
/// A cache of information about an Announcement that we retrieve just-in-time from the Slack API (rather than store
/// it ourselves).
/// </summary>
public interface IAnnouncementCache
{
    /// <summary>
    /// If the <see cref="Announcement.Text"/> property is <c>null</c>, this will call the Slack API to retrieve
    /// the announcement text and cache it if the text is not null.
    /// </summary>
    /// <param name="announcement">The Announcement</param>
    /// <returns>The text of the message, if it can be retrieved.</returns>
    Task<string?> GetAndCacheAnnouncementTextAsync(Announcement announcement);
}

public sealed class AnnouncementCache : IDisposable, IAnnouncementCache
{
    static readonly ILogger<AnnouncementCache> Log = ApplicationLoggerFactory.CreateLogger<AnnouncementCache>();

    readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions
    {
        // SizeLimit is a unit-less value. It's dependent on us to define what the "size" of each cache entry is.
        // We'll use the size of the announcement text as the unit of size for each cache entry.
        // We just need to pick some value so the cache doesn't grow indefinitely.
        SizeLimit = 10_000_000
    });
    readonly IConversationsApiClient _conversationsApiClient;

    public AnnouncementCache(IConversationsApiClient conversationsApiClient)
    {
        _conversationsApiClient = conversationsApiClient;
    }

    /// <summary>
    /// If the <see cref="Announcement.Text"/> property is <c>null</c>, this will call the Slack API to retrieve
    /// the announcement text and cache it if the text is not null.
    /// </summary>
    /// <param name="announcement">The Announcement</param>
    /// <returns>The text of the message, if it can be retrieved.</returns>
    public async Task<string?> GetAndCacheAnnouncementTextAsync(Announcement announcement)
    {
        if (announcement.Text is not null)
        {
            return announcement.Text;
        }

        var (key, channel, timestamp) = (
            announcement.Id,
            announcement.SourceRoom.PlatformRoomId,
            announcement.SourceMessageId);

        if (_memoryCache.TryGetValue(key, out string? messageText))
        {
            Log.RetrievedFromCache(key, timestamp, channel);
            return messageText;
        }

        var message = await _conversationsApiClient.GetMessageAsync(announcement);
        if (message is { Text.Length: > 0 })
        {
            using ICacheEntry entry = _memoryCache.CreateEntry(key);
            entry.SetSize(message.Text.Length);
            entry.SlidingExpiration = TimeSpan.FromDays(1);
            entry.Value = message.Text;

            Log.MessageCached(announcement.Id, timestamp, channel);

            return message.Text;
        }

        return null;
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }
}

public static partial class AnnouncementCacheLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Message Cached: {AnnouncementId} for Timestamp: {Timestamp}, Channel: {Channel}")]
    public static partial void MessageCached(
        this ILogger<AnnouncementCache> logger,
        int announcementId,
        string? timestamp,
        string? channel);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Retrieved from Cache: {Key} for Timestamp: {Timestamp}, Channel: {Channel}")]
    public static partial void RetrievedFromCache(
        this ILogger<AnnouncementCache> logger,
        int key,
        string? timestamp,
        string? channel);
}
