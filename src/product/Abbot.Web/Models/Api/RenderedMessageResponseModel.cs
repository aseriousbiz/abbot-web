using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Models.Api;

public record RenderedMessageResponseModel(string Text, IReadOnlyList<RenderedMessageSpanResponseModel> Spans)
{
    public static RenderedMessageResponseModel Create(string text, RenderedMessage rendered, Member? viewer = null)
    {
        return new RenderedMessageResponseModel(
            text,
            rendered.Spans.Select(span => RenderedMessageSpanResponseModel.Create(span, viewer)).ToList());
    }
}

public abstract record RenderedMessageSpanResponseModel(string Text)
{
    public static RenderedMessageSpanResponseModel Create(RenderedMessageSpan span, Member? viewer = null) =>
        span switch
        {
            PlainTextSpan text => new TextSpanResponseModel(text.Text),
            UserMentionSpan mention => new UserMentionSpanResponseModel(
                mention.OriginalText,
                mention.Id,
                mention.Member is null
                    ? null
                    : MemberResponseModel.Create(mention.Member, viewer)),
            RoomMentionSpan room => new RoomMentionSpanResponseModel(
                room.OriginalText,
                room.Id,
                room.Room is null
                    ? null
                    : RoomResponseModel.Create(room.Room, viewer)),
            var x => throw new InvalidOperationException($"Unknown span type: {x.GetType()}"),
        };
}

public record UserMentionSpanResponseModel(string Text, string PlatformUserId, MemberResponseModel? Member) : RenderedMessageSpanResponseModel(Text);
public record RoomMentionSpanResponseModel(string Text, string PlatformRoomId, RoomResponseModel? Room) : RenderedMessageSpanResponseModel(Text);
public record TextSpanResponseModel(string Text) : RenderedMessageSpanResponseModel(Text);
