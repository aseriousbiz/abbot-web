using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;

public class MessageRendererTests
{
    [Fact]
    public async Task FullRenderTest()
    {
        var env = TestEnvironmentBuilder.Create()
            .Substitute<ISlackResolver>(out var slackResolver)
            .Build();
        var room = await env.CreateRoomAsync();

        slackResolver
            .ResolveMemberAsync(env.TestData.Member.User.PlatformUserId, env.TestData.Organization)
            .Returns(Task.FromResult<Member?>(env.TestData.Member));
        slackResolver
            .ResolveMemberAsync("unknown", env.TestData.Organization)
            .Returns(Task.FromResult<Member?>(null));
        slackResolver
            .ResolveRoomAsync(room.PlatformRoomId, env.TestData.Organization, false)
            .Returns(Task.FromResult<Room?>(room));
        slackResolver
            .ResolveRoomAsync("unknown", env.TestData.Organization, false)
            .Returns(Task.FromResult<Room?>(null));

        var message = $@"<@{env.TestData.Member.User.PlatformUserId}> can you add <@U0000|steve> to <#{room.PlatformRoomId}|my channel> and <#C0000>? Also add them to <!subteam^1234>. I guess anyone <!here> could do that though. Just go to <https://www.google.com|Google> or <https://www.bing.com> and search it up.";

        var renderer = env.Activate<MessageRenderer>();
        var parsed = await renderer.RenderMessageAsync(message, env.TestData.Organization);

        // We don't support "|title" syntax in user/room mentions (probably not something Slack actually does)
        // Nor do we support "<!subteam^1234>" yet since we don't track user groups.
        // Also, we don't actually render links and @here/@channel/@everyone specially, so they're just plain text
        Assert.Equal(new RenderedMessageSpan[]
        {
            new UserMentionSpan($"<@{env.TestData.Member.User.PlatformUserId}>", env.TestData.Member.User.PlatformUserId, env.TestData.Member),
            new PlainTextSpan(" can you add "),
            new UserMentionSpan("<@U0000|steve>", "U0000", null),
            new PlainTextSpan(" to "),
            new RoomMentionSpan($"<#{room.PlatformRoomId}|my channel>", room.PlatformRoomId, room),
            new PlainTextSpan(" and "),
            new RoomMentionSpan($"<#C0000>", "C0000", null),
            new PlainTextSpan("? Also add them to "),
            new PlainTextSpan("<!subteam^1234>"),
            new PlainTextSpan(". I guess anyone "),
            new PlainTextSpan("@here"),
            new PlainTextSpan(" could do that though. Just go to "),
            new LinkSpan("<https://www.google.com|Google>", "https://www.google.com", new [] { new PlainTextSpan("Google") }),
            new PlainTextSpan(" or "),
            new LinkSpan("<https://www.bing.com>", "https://www.bing.com", new[] { new PlainTextSpan("https://www.bing.com") }),
            new PlainTextSpan(" and search it up."),
        }, parsed.Spans.ToArray());
    }
}
