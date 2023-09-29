using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Signals;

public class SystemSignalerTests
{

    public class TheEnqueueSystemSignalMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("1111.3333")]
        public async Task ProperlyUnpacksArgumentsAndQueuesBackgroundJob(string? threadId)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var systemSignaler = env.Activate<SystemSignaler>();
            var platformRoom = room.ToPlatformRoom();

            systemSignaler.EnqueueSystemSignal(
                SystemSignal.StaffTestSignal,
                "a b c",
                organization,
                platformRoom,
                member,
                new MessageInfo(MessageId: "1111.2222", "Some text", null, ThreadId: threadId, convo, member));

            env.BackgroundJobClient.DidEnqueue<SignalHandler>(h => h.HandleSystemSignalAsync(
                SystemSignal.StaffTestSignal,
                organization,
                "a b c",
                platformRoom,
                member,
                new MessageInfo("1111.2222", "Some text", null, threadId, convo, member)));
        }

        [Fact]
        public async Task ProperlyUnpacksArgumentsAndQueuesBackgroundJobWhenTriggeringMessageNull()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var systemSignaler = env.Activate<SystemSignaler>();
            var platformRoom = new PlatformRoom("C123", "midgar");

            systemSignaler.EnqueueSystemSignal(
                SystemSignal.StaffTestSignal,
                "a b c",
                organization,
                platformRoom,
                env.TestData.Member,
                null);

            env.BackgroundJobClient.DidEnqueue<SignalHandler>(h => h.HandleSystemSignalAsync(
                SystemSignal.StaffTestSignal,
                organization,
                "a b c",
                platformRoom,
                env.TestData.Member,
                null));
        }
    }
}
