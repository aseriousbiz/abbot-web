using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Signals;
using Serious.TestHelpers;

public class SignalHandlerTests
{
    static void AssertSignalInvocation(
        FakeSkillRunnerClientInvocation? invocation,
        string expectedSignalName,
        string expectedSignalArguments,
        string expectedCommandText,
        string expectedSkillName,
        Id<Conversation> expectedConversationId)
    {
        Assert.NotNull(invocation?.Skill);
        Assert.NotNull(invocation.Signal);
        Assert.NotNull(invocation.Conversation);
        Assert.Equal(expectedSignalName, invocation.Signal.Name);
        Assert.Equal(expectedSignalArguments, invocation.Signal.Arguments);
        Assert.Equal(expectedCommandText, invocation.CommandText);
        Assert.Equal(expectedSkillName, invocation.Skill.Name);
        Assert.Equal(expectedConversationId, Id<Conversation>.Parse(invocation.Conversation.Id));
    }

    public class TheEnqueueSignalHandlingMethod
    {
        [Fact]
        public void ReturnsFalseWhenSignalRequestHasCycle()
        {
            var env = TestEnvironment.Create();
            var signal = new SignalRequest
            {
                Name = "foot",
                Source = new SignalSourceMessage
                {
                    SignalEvent = new SignalMessage
                    {
                        Name = "foot",
                        Source = new SignalSourceMessage()
                    }
                },
                Room = new PlatformRoom("C012341234", "whatever"),
            };
            var signalHandler = env.Activate<SignalHandler>();

            var result = signalHandler.EnqueueSignalHandling(new Id<Skill>(123), signal);

            Assert.False(result);
        }
    }

    public class TheHandleSignalAsyncMethod
    {
        [Fact]
        public async Task CallsAllSubscribersByName()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            const string signalName = "happy-happy-joy-joy";
            var skills = new[]
            {
                await env.CreateSkillAsync("signaler"),
                await env.CreateSkillAsync("signalee-1", subscriptions: new[] { signalName }),
                await env.CreateSkillAsync("signalee-2", subscriptions: new[] { signalName }),
                await env.CreateSkillAsync("signalee-3", subscriptions: new[] { "sad-face" })
            };
            env.SkillRunnerClient.PushResponse(new SkillRunResponse { Success = true });
            env.SkillRunnerClient.PushResponse(new SkillRunResponse { Success = true });
            var signal = new SignalRequest
            {
                Name = signalName,
                Arguments = "clap along!",
                SenderId = member.Id,
                ConversationId = convo.Id.ToString(),
                Room = room.ToPlatformRoom(),
            };
            var signalHandler = env.Activate<SignalHandler>();

            await signalHandler.HandleSignalAsync(skills[0], signal, "traceparent");

            Assert.Collection(env.SkillRunnerClient.Invocations,
                i => AssertSignalInvocation(i, signalName, "clap along!", "signalee-1 clap along!", "signalee-1", convo),
                i => AssertSignalInvocation(i, signalName, "clap along!", "signalee-2 clap along!", "signalee-2", convo));
        }

        [Fact]
        public async Task CallsAllMatchingSubscribers()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            const string signalName = "happy-happy-joy-joy";
            var skills = new[]
            {
                await env.CreateSkillAsync("signaler"),
                await env.CreateSkillAsync("signalee-1", new[] { new SignalSubscription { Name = signalName, ArgumentsPattern = "match-this", ArgumentsPatternType = PatternType.ExactMatch } }),
                await env.CreateSkillAsync("signalee-2", new[] { new SignalSubscription { Name = signalName, ArgumentsPattern = "MATCH-THAT", ArgumentsPatternType = PatternType.ExactMatch } }),
                await env.CreateSkillAsync("signalee-3", new[] { new SignalSubscription { Name = "sad-face", ArgumentsPattern = "match-that", ArgumentsPatternType = PatternType.ExactMatch } }),
                await env.CreateSkillAsync("signalee-4", new[] { new SignalSubscription { Name = signalName, ArgumentsPattern = "match-", ArgumentsPatternType = PatternType.StartsWith } }),
                await env.CreateSkillAsync("signalee-5", new[] { new SignalSubscription { Name = signalName, ArgumentsPattern = ".*atch-.*", ArgumentsPatternType = PatternType.RegularExpression } }),
                await env.CreateSkillAsync("signalee-6", new[] { new SignalSubscription { Name = signalName, ArgumentsPattern = ".*atch-.*", ArgumentsPatternType = PatternType.None } }), // Disabled
                await env.CreateSkillAsync("signalee-7", new[] { new SignalSubscription { Name = signalName, ArgumentsPattern = null, ArgumentsPatternType = PatternType.None } }), // No pattern, so matches everything.
                await env.CreateSkillAsync("signalee-2", new[] { new SignalSubscription { Name = signalName, ArgumentsPattern = "MATCH-THAT", ArgumentsPatternType = PatternType.ExactMatch, CaseSensitive = true } }),

            };
            env.SkillRunnerClient.PushResponse(new SkillRunResponse { Success = true });
            env.SkillRunnerClient.PushResponse(new SkillRunResponse { Success = true });
            var signal = new SignalRequest
            {
                Name = signalName,
                Arguments = "match-this",
                SenderId = member.Id,
                ConversationId = convo.Id.ToString(),
                Room = room.ToPlatformRoom(),
            };
            var signalHandler = env.Activate<SignalHandler>();

            await signalHandler.HandleSignalAsync(skills[0], signal, "traceparent");

            Assert.Collection(env.SkillRunnerClient.Invocations,
                i => AssertSignalInvocation(i, signalName, "match-this", "signalee-1 match-this", "signalee-1", convo),
                i => AssertSignalInvocation(i, signalName, "match-this", "signalee-4 match-this", "signalee-4", convo),
                i => AssertSignalInvocation(i, signalName, "match-this", "signalee-5 match-this", "signalee-5", convo),
                i => AssertSignalInvocation(i, signalName, "match-this", "signalee-7 match-this", "signalee-7", convo));
        }
    }

    public class TheHandleSystemSignalAsyncMethod
    {
        [Fact]
        public async Task CallsAllSubscribersFromAbbotWhenMessageNull()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("signaler");
            await env.CreateSkillAsync("signalee-1", subscriptions: new[] { SystemSignal.StaffTestSignal.Name });
            await env.CreateSkillAsync("signalee-2", subscriptions: new[] { SystemSignal.StaffTestSignal.Name });
            await env.CreateSkillAsync("signalee-3", subscriptions: new[] { "sad-face" });
            env.SkillRunnerClient.PushResponse(new SkillRunResponse { Success = true });
            env.SkillRunnerClient.PushResponse(new SkillRunResponse { Success = true });
            var signalHandler = env.Activate<SignalHandler>();

            await signalHandler.HandleSystemSignalAsync(
                SystemSignal.StaffTestSignal,
                organization,
                "clap along!",
                new PlatformRoom("C001", "midgar"),
                env.TestData.Member,
                triggeringMessage: null);

            Assert.Equal(2, env.SkillRunnerClient.Invocations.Count);
            var firstInvocation = env.SkillRunnerClient.Invocations[0];
            var secondInvocation = env.SkillRunnerClient.Invocations[1];
            Assert.NotNull(firstInvocation.Skill);
            Assert.NotNull(firstInvocation.Signal);
            Assert.NotNull(secondInvocation.Skill);
            Assert.NotNull(secondInvocation.Signal);
            Assert.Equal("clap along!", firstInvocation.Arguments);
            Assert.Equal("clap along!", secondInvocation.Arguments);
            Assert.NotNull(firstInvocation.Skill);
            Assert.NotNull(secondInvocation.Skill);
            Assert.NotNull(firstInvocation.Signal);
            Assert.NotNull(secondInvocation.Signal);
            Assert.Null(firstInvocation.Conversation);
            Assert.Null(secondInvocation.Conversation);
            Assert.Equal("signalee-1", firstInvocation.Skill?.Name);
            Assert.Equal("signalee-2", secondInvocation.Skill?.Name);
            Assert.NotNull(firstInvocation.Signal);
            Assert.NotNull(secondInvocation.Signal);
            Assert.Equal(SystemSignal.StaffTestSignal.Name, firstInvocation.Signal.Name);
            Assert.Equal(SystemSignal.StaffTestSignal.Name, secondInvocation.Signal.Name);
            Assert.Equal("clap along!", firstInvocation.Signal.Arguments);
            Assert.Equal("clap along!", secondInvocation.Signal.Arguments);
            Assert.NotNull(firstInvocation.Caller);
            Assert.NotNull(secondInvocation.Caller);
            Assert.Equal(env.TestData.Member.User, firstInvocation.Caller);
            Assert.Equal(env.TestData.Member.User, secondInvocation.Caller);
        }

        [Fact]
        public async Task CallsAllSubscribersFromMember()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateSkillAsync("signaler");
            await env.CreateSkillAsync("signalee-1", subscriptions: new[] { SystemSignal.StaffTestSignal.Name });
            await env.CreateSkillAsync("signalee-2", subscriptions: new[] { SystemSignal.StaffTestSignal.Name });
            await env.CreateSkillAsync("signalee-3", subscriptions: new[] { "sad-face" });
            env.SkillRunnerClient.PushResponse(new SkillRunResponse { Success = true });
            env.SkillRunnerClient.PushResponse(new SkillRunResponse { Success = true });
            var signalHandler = env.Activate<SignalHandler>();

            await signalHandler.HandleSystemSignalAsync(
                SystemSignal.StaffTestSignal,
                organization,
                "clap along!",
                new PlatformRoom("C001", "midgar"),
                member,
                new MessageInfo(
                    "9999.6666",
                    "Some text",
                    null,
                    null,
                    convo,
                    member));

            Assert.Equal(2, env.SkillRunnerClient.Invocations.Count);
            var firstInvocation = env.SkillRunnerClient.Invocations[0];
            var secondInvocation = env.SkillRunnerClient.Invocations[1];
            Assert.NotNull(firstInvocation.Skill);
            Assert.NotNull(firstInvocation.Signal);
            Assert.NotNull(secondInvocation.Skill);
            Assert.NotNull(secondInvocation.Signal);
            Assert.Equal("clap along!", firstInvocation.Arguments);
            Assert.Equal("clap along!", secondInvocation.Arguments);
            Assert.Equal("Some text", firstInvocation.CommandText);
            Assert.Equal("Some text", secondInvocation.CommandText);
            Assert.NotNull(firstInvocation.Skill);
            Assert.NotNull(secondInvocation.Skill);
            Assert.NotNull(firstInvocation.Signal);
            Assert.NotNull(secondInvocation.Signal);
            Assert.NotNull(firstInvocation.Conversation);
            Assert.NotNull(secondInvocation.Conversation);
            Assert.Equal("signalee-1", firstInvocation.Skill?.Name);
            Assert.Equal("signalee-2", secondInvocation.Skill?.Name);
            Assert.NotNull(firstInvocation.Signal);
            Assert.NotNull(secondInvocation.Signal);
            Assert.Equal(SystemSignal.StaffTestSignal.Name, firstInvocation.Signal.Name);
            Assert.Equal(SystemSignal.StaffTestSignal.Name, secondInvocation.Signal.Name);
            Assert.Equal("clap along!", firstInvocation.Signal.Arguments);
            Assert.Equal("clap along!", secondInvocation.Signal.Arguments);
            Assert.NotNull(firstInvocation.Caller);
            Assert.NotNull(secondInvocation.Caller);
            Assert.Equal(member.Id, firstInvocation.Caller.Id);
            Assert.Equal(member.Id, secondInvocation.Caller.Id);
            Assert.Equal(convo.Id.ToString(), firstInvocation.Conversation.Id);
            Assert.Equal(convo.Id.ToString(), secondInvocation.Conversation.Id);
            Assert.Equal(convo.Title, firstInvocation.Conversation.Title);
            Assert.Equal(convo.Title, secondInvocation.Conversation.Title);
        }
    }
}
