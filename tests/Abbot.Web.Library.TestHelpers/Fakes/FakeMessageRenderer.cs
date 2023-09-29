using System;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;

namespace Serious.TestHelpers;

public class FakeMessageRenderer : IMessageRenderer
{
    public Task<RenderedMessage> RenderMessageAsync(string? text, Organization organization)
    {
        var spans = text is not { Length: > 0 }
            ? Array.Empty<RenderedMessageSpan>()
            : new RenderedMessageSpan[] { new PlainTextSpan(text) };

        return Task.FromResult(new RenderedMessage(spans));
    }
}
