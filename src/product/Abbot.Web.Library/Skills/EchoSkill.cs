using System.Threading;
using Microsoft.Bot.Builder;
using Serious.Abbot.Functions.Models;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;

namespace Serious.Abbot.Skills;

[Skill(Description = "Echoes back whatever you send it. Useful for testing.")]
public sealed class EchoSkill : ISkill
{
    public Task OnMessageActivityAsync(
        MessageContext messageContext,
        CancellationToken cancellationToken)
    {
        var text = messageContext.Arguments.Value;
        var (thread, echoText, echoFormat, user, room)
            = SkillPatterns.MatchEchoPattern(text);

        return echoText.Length == 0
            ? messageContext.SendHelpTextAsync(this)
            : ReplyWithEcho(thread, echoText, echoFormat, user, room, messageContext);
    }

    static Task ReplyWithEcho(
        bool thread,
        string echoText,
        string echoFormat,
        string user,
        string room,
        MessageContext messageContext)
    {
        var message = MessageFactory.Text(echoText);

        if (echoFormat.Length > 0)
        {
            message.TextFormat = echoFormat;
        }

        IMessageTarget? target =
            thread && messageContext.ReplyInThreadMessageTarget is { } replyThread
                ? replyThread
                : user.Length > 0
                    ? new UserMessageTarget(user)
                    : room.Length > 0
                        ? new RoomMessageTarget(room)
                        : null;

        return target is not null
            ? messageContext.SendActivityAsync(message, target)
            : messageContext.SendActivityAsync(message);
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{phrase}", "responds with {phrase}.");
        usage.Add("format:{format} {phrase}", "sets the text format (such as `plain`) when responding.");
        usage.Add("!thread phrase", "echos the message in a new thread");
        usage.Add("user:{user} phrase", "echos the message as a DM to the specified user.");
        usage.Add("room:{roomId} phrase", "echos the message in the specified room.");
    }
}
