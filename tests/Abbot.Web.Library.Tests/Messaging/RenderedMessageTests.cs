using Abbot.Common.TestHelpers;
using Serious.Abbot.Messaging;
using Serious.Slack;

public class RenderedMessageTests
{
    public class TheToTextMethod
    {
        [Fact]
        public async Task RendersTextualMessage()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var member = env.TestData.Member;
            var userId = member.User.PlatformUserId;
            var message = new RenderedMessage(new List<RenderedMessageSpan>
            {
                new PlainTextSpan("Hello, "),
                new LinkSpan("world", "http://example.com", new [] { new PlainTextSpan("world") }),
                new PlainTextSpan(" "),
                new EmojiSpan(":smile:", new Emoji("smile")),
                new PlainTextSpan(" "),
                new UserMentionSpan($"<@{userId}|user>", userId, member),
                new PlainTextSpan(" and "),
                new UserMentionSpan("<@U00000099>", "U00000099", null),
                new PlainTextSpan(" in "),
                new RoomMentionSpan($"<#{room.PlatformRoomId}|{room.Name}>", room.PlatformRoomId, room),
                new PlainTextSpan(" and "),
                new RoomMentionSpan("<#C00000002>", "C00000002", null),
                new PlainTextSpan("."),
            });

            var result = message.ToText();

            Assert.Equal($"Hello, world :smile: {member.DisplayName} and (unknown user U00000099) in #{room.Name} and (unknown channel C00000002).", result);
        }
    }

    public class TheToHtmlMethod
    {
        [Fact]
        public async Task RendersTextualMessage()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.Domain = "aseriousbiz.slack.com";
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();
            var member = env.TestData.Member;
            var userId = member.User.PlatformUserId;
            var message = new RenderedMessage(new List<RenderedMessageSpan>
            {
                new PlainTextSpan("Hello, "),
                new LinkSpan("world", "http://example.com", new [] { new PlainTextSpan("world") }),
                new PlainTextSpan(" "),
                new EmojiSpan(":smile:", new Emoji("smile")),
                new PlainTextSpan(" "),
                new UserMentionSpan($"<@{userId}|user>", userId, member),
                new PlainTextSpan(" and "),
                new UserMentionSpan("<@U00000099>", "U00000099", null),
                new PlainTextSpan(" in "),
                new RoomMentionSpan($"<#{room.PlatformRoomId}|{room.Name}>", room.PlatformRoomId, room),
                new PlainTextSpan(" and "),
                new RoomMentionSpan("<#C00000002>", "C00000002", null),
                new PlainTextSpan("."),

            });

            var result = message.ToHtml();

            Assert.Equal(
                $@"Hello, <a href=""http://example.com"">world</a> :smile: <a href=""https://aseriousbiz.slack.com/team/{userId}"">{env.TestData.User.DisplayName}</a> and (unknown user U00000099) in <a href=""https://aseriousbiz.slack.com/archives/{room.PlatformRoomId}"">#{room.Name}</a> and (unknown channel C00000002).",
                result);
        }
    }
}
