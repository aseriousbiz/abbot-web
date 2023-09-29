using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.TestHelpers;

namespace Serious.Abbot.Messaging;

public class UnitTestPlatformMessage : PlatformMessage
{
    public delegate ChannelUser ChannelUserFactory(string platformId, ChannelAccount account);

    public delegate BotChannelUser BotUserFactory(string platformId, ChannelAccount account);

    public UnitTestPlatformMessage(Organization organization,
        ITurnContext turnContext,
        BotUserFactory botUserFactory)
        : base(
            new MessageEventInfo(
                turnContext.Activity.Text ?? string.Empty,
                "UnitTest",
                "U001",
                Array.Empty<string>(),
                false,
                false,
                null,
                null,
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>()),
            null,
            organization,
            turnContext.Activity.Timestamp ?? DateTimeOffset.UtcNow,
            new Responder(new FakeSimpleSlackApiClient(), turnContext),
            from: new Member { User = new User { PlatformUserId = "U001", DisplayName = "Unit Test User" } },
            botUser: botUserFactory(organization.PlatformId, turnContext.Activity.Recipient),
            mentions: Enumerable.Empty<Member>(),
            room: new Room { PlatformRoomId = "UnitTest", Name = "UnitTest", Persistent = false })
    {
    }
}
