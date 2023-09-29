using System.Threading;

namespace Abbot.Common.TestHelpers;

public class IdGenerator
{
    int _nextId;

    public int GetId() => Interlocked.Increment(ref _nextId);
    public string GetSlackAppId(int? id = null) => $"A{id ?? GetId():0000}";
    public string GetSlackEventId(int? id = null) => $"EV{id ?? GetId():0000}";
    public string GetSlackTeamId(int? id = null) => $"T{id ?? GetId():0000}";
    public string GetSlackUserId(int? id = null) => $"U{id ?? GetId():0000}";
    public string GetSlackBotId(int? id = null) => $"B{id ?? GetId():0000}";
    public string GetSlackChannelId(int? id = null) => $"C{id ?? GetId():0000}";
    public string GetSlackMessageId(int? id = null) => $"1111.{id ?? GetId():0000}";
}
