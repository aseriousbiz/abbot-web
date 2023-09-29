using System;
using System.Collections.Generic;
using Serious.Abbot.Scripting;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Functions.Models;

public record SourceMessage : IMessage
{
    public required string Text { get; init; }

    public required string Id { get; init; }

    public required string? ThreadId { get; init; }

    public required Uri Url { get; init; }

    public required IChatUser Author { get; init; }
}
