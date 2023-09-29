using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers
{
    public class FakeBotReplyClient : IBotReplyClient
    {
        public class ReplyInfo
        {
            public string? Message { get; init; }
            public TimeSpan Delay { get; init; }
            public MessageOptions? Options { get; init; }
            public IEnumerable<Button> Buttons { get; init; } = Array.Empty<Button>();
            public string? ButtonsLabel { get; init; }
            public string? Image { get; init; }
            public string? Title { get; init; }
            public Uri? TitleUrl { get; init; }
            public string? Color { get; init; }
            public string? Blocks { get; init; }
        }

        readonly List<ReplyInfo> _sent = new();

        public IReadOnlyList<ReplyInfo> SentReplies => _sent;

        public Task<ProactiveBotMessageResponse> SendReplyAsync(string reply, TimeSpan delay, MessageOptions? options)
        {
            DidReply = true;
            _sent.Add(new ReplyInfo
            {
                Message = reply,
                Delay = delay,
                Options = options,
            });
            return Task.FromResult(new ProactiveBotMessageResponse(true));
        }

        public Task<ProactiveBotMessageResponse> SendSlackReplyAsync(
            string fallbackText,
            string blocksJson,
            MessageOptions? options)
        {
            DidReply = true;
            _sent.Add(new ReplyInfo
            {
                Message = fallbackText,
                Options = options,
                Blocks = blocksJson
            });
            return Task.FromResult(new ProactiveBotMessageResponse(true));
        }

        public Task<ProactiveBotMessageResponse> SendReplyAsync(string reply, TimeSpan delay,
            IEnumerable<Button> buttons, string? buttonsLabel, string? image,
            string? title, Uri? titleUrl, string? color, MessageOptions? options)
        {
            DidReply = true;
            _sent.Add(new ReplyInfo
            {
                Message = reply,
                Delay = delay,
                Buttons = buttons,
                ButtonsLabel = buttonsLabel,
                Image = image,
                Title = title,
                TitleUrl = titleUrl,
                Color = color,
                Options = options,
            });
            return Task.FromResult(new ProactiveBotMessageResponse(true));
        }

        public bool DidReply { get; private set; }
        public IEnumerable<string> Replies => _sent.Select(r => r.Message ?? string.Empty);
    }
}
