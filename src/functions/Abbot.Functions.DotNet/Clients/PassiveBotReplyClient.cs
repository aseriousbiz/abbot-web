using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Services;

/// <summary>
/// This is an <see cref="IBotReplyClient" /> that cannot proactively reply. Instead, it stores replies which
/// are examined and returned at a later date.
/// </summary>
public class PassiveBotReplyClient : IBotReplyClient
{
    readonly List<string> _replies = new();

    public Task<ProactiveBotMessageResponse> SendReplyAsync(string reply, TimeSpan delay, MessageOptions? options)
    {
        DidReply = true;
        _replies.Add(reply);
        return Task.FromResult(new ProactiveBotMessageResponse(true));
    }

    public Task<ProactiveBotMessageResponse> SendSlackReplyAsync(
        string fallbackText,
        string blocksJson,
        MessageOptions? options)
    {
        DidReply = true;
        _replies.Add(fallbackText);
        return Task.FromResult(new ProactiveBotMessageResponse(true));
    }

    public Task<ProactiveBotMessageResponse> SendReplyAsync(
        string reply,
        TimeSpan delay,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        string? image,
        string? title,
        Uri? titleUrl,
        string? color,
        MessageOptions? options)
    {
        DidReply = true;
        _replies.Add(reply);
        return Task.FromResult(new ProactiveBotMessageResponse(true));
    }

    /// <summary>
    /// Returns true if at least one reply was sent with a delay of <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public bool DidReply { get; set; }

    /// <summary>
    /// The set of replies that need to be sent with the response.
    /// </summary>
    public IEnumerable<string> Replies => _replies;
}
