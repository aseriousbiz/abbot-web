using System;
using System.Threading.Tasks;
using Serious.Abbot.Scripting;
using Serious.Slack.BlockKit;

namespace Serious.TestHelpers;

public class FakeSlackClient : ISlack
{
    public Task ReplyAsync(string fallbackText, params ILayoutBlock[] blocks)
    {
        throw new NotImplementedException();
    }

    public Task ReplyAsync(MessageOptions options, string fallbackText, params ILayoutBlock[] blocks)
    {
        throw new NotImplementedException();
    }

    public Task ReplyAsync(string fallbackText, object blocks)
    {
        throw new NotImplementedException();
    }

    public Task ReplyAsync(MessageOptions options, string fallbackText, object blocks)
    {
        throw new NotImplementedException();
    }

    public Task ReplyAsync(string fallbackText, string blocksJson)
    {
        throw new NotImplementedException();
    }

    public Task ReplyAsync(MessageOptions options, string fallbackText, string blocksJson)
    {
        throw new NotImplementedException();
    }

    public IMessageBlockActionsPayload? MessagePayload => throw new NotImplementedException();
}
