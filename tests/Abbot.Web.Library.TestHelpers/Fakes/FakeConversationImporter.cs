using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeConversationThreadResolver : IConversationThreadResolver
{
    public FakeConversationThreadResolver(IClock clock, CommonTestData testData)
    {
        Clock = clock;
        TestData = testData;
    }

    public IClock Clock { get; }
    public CommonTestData TestData { get; }

    public async Task<IReadOnlyList<ConversationMessage>> ResolveConversationMessagesAsync(Room room, string messageId)
    {
        return new[]
        {
            new ConversationMessage(
                "Import me!",
                room.Organization,
                TestData.Member,
                room,
                Clock.UtcNow,
                messageId,
                ThreadId: null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                MessageContext: null),
        };
    }
}
