using Microsoft.Extensions.Caching.Memory;
using Serious.Abbot.Events;
using Serious.Slack.Events;

namespace Serious.Abbot.BotFramework;

public class SlackEventDeduplicator : IDisposable
{
    readonly IMemoryCache _cache;

    public SlackEventDeduplicator()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public void Dispose() => _cache.Dispose();

    public bool IsDuplicate(EventBody eventBody)
    {
        var key = eventBody switch
        {
            UserChangeEvent uce => UserEventPayload.FromSlackUserInfo(uce.User),
            _ => null,
        };

        if (key is null)
        {
            return false;
        }

        if (_cache.TryGetValue(key, out var _))
        {
            return true;
        }

        _cache.Set(key, key, TimeSpan.FromMinutes(31));
        return false;
    }
}
