using Abbot.Common.TestHelpers.Fakes;
using Castle.Components.DictionaryAdapter;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.TestHelpers
{
    public class FakeSkillRunnerClient : ISkillRunnerClient
    {
        readonly List<FakeSkillRunnerClientInvocation> _invocations = new();
        readonly Stack<SkillRunResponse> _responses = new();
        readonly Stack<Exception> _exceptions = new();

        public Task<SkillRunResponse> SendAsync(Skill skill,
            IArguments arguments,
            string commandText,
            IEnumerable<Member> mentions,
            Member caller,
            BotChannelUser bot,
            PlatformRoom platformRoom,
            CustomerInfo? customerInfo,
            Uri skillUrl,
            bool isInteractive = false,
            IPattern? pattern = null,
            SignalMessage? signal = null,
            Uri? messageUrl = null,
            string? messageId = null,
            string? threadId = null,
            Member? triggeringMessageAuthor = null,
            ChatConversation? conversation = null,
            Room? room = null,
            MessageInteractionInfo? interactionInfo = null,
            bool passiveReplies = false,
            SkillRunProperties? properties = null)
        {
            _invocations.Add(new FakeSkillRunnerClientInvocation
            {
                Skill = skill,
                Arguments = arguments.Value,
                TokenizedArguments = arguments,
                CommandText = commandText,
                Mentions = mentions.ToReadOnlyList(),
                Caller = caller.User,
                Bot = bot,
                Signal = signal,
                Pattern = pattern,
                MessageId = messageId,
                Conversation = conversation,
                InteractionInfo = interactionInfo,
                MessageUrl = messageUrl,
                AuditProperties = properties,
                TriggeringMessageAuthor = triggeringMessageAuthor,
            });

            if (_exceptions.Any())
            {
                return Task.FromException<SkillRunResponse>(_exceptions.Pop());
            }

            return Task.FromResult(_responses.Any()
                ? _responses.Pop()
                : new()
                {
                    Success = false,
                    Replies = new List<string>(),
                    Errors = new EditableList<RuntimeError>(),
                    ContentType = null,
                    Content = null,
                });
        }

        public Task<SkillRunResponse> SendHttpTriggerAsync(
            SkillHttpTrigger trigger,
            HttpTriggerRequest triggerRequest,
            Uri skillUrl,
            Guid auditId)
        {
            _invocations.Add(new FakeSkillRunnerClientInvocation
            {
                SkillTrigger = trigger,
                HttpTriggerEvent = triggerRequest,
                Caller = trigger.Creator,
            });
            return Task.FromResult(_responses.Any()
                ? _responses.Pop()
                : new()
                {
                    Success = false,
                    Replies = new List<string>(),
                    Errors = new EditableList<RuntimeError>(),
                    ContentType = null,
                    Content = null,
                });
        }

        public Task<SkillRunResponse> SendPlaybookActionTriggerAsync(SkillPlaybookActionTrigger trigger, Uri skillUrl, Guid auditId)
        {
            _invocations.Add(new FakeSkillRunnerClientInvocation
            {
                SkillTrigger = trigger,
                Caller = trigger.Creator,
            });
            return Task.FromResult(_responses.Any()
                ? _responses.Pop()
                : new()
                {
                    Success = false,
                    Replies = new List<string>(),
                    Errors = new EditableList<RuntimeError>(),
                    ContentType = null,
                    Content = null,
                });
        }

        public Task<SkillRunResponse> SendScheduledTriggerAsync(SkillScheduledTrigger trigger, Uri skillUrl, Guid auditId)
        {
            _invocations.Add(new FakeSkillRunnerClientInvocation
            {
                SkillTrigger = trigger,
                Caller = trigger.Creator,
            });
            return Task.FromResult(_responses.Any()
                ? _responses.Pop()
                : new()
                {
                    Success = false,
                    Replies = new List<string>(),
                    Errors = new EditableList<RuntimeError>(),
                    ContentType = null,
                    Content = null,
                });
        }

        public IReadOnlyList<FakeSkillRunnerClientInvocation> Invocations => _invocations;

        public void PushResponse(SkillRunResponse response)
        {
            _responses.Push(response);
        }

        public void PushException(Exception exception)
        {
            _exceptions.Push(exception);
        }

        static SkillRunnerClient CreateRealSkillRunnerClient(
            ISkillAuditLog auditLog,
            IEnumerable<SkillRunResponse> responseBodies,
            string contentType = "application/json")
        {
            var httpMessageHandler = new FakeHttpMessageHandler();
            var httpClient = new HttpClient(httpMessageHandler);
            const string urlText = "http://localhost:7071/api/skillrunner";
            var url = new Uri(urlText);
            foreach (var responseBody in responseBodies)
            {
                httpMessageHandler.AddResponse(
                    url,
                    HttpMethod.Post,
                    responseBody,
                    contentType);
            }

            var endpoints = new FakeRunnerEndpointManager();
            var tokenFactory = new FakeApiTokenFactory();
            return new SkillRunnerClient(
                Substitute.For<IRoomRepository>(),
                httpClient,
                endpoints,
                tokenFactory,
                new FakePermissionRepository(Capability.Use),
                new FakeUrlGenerator(),
                new FakeSkillRunnerRetryPolicy(),
                auditLog);
        }

        public static SkillRunnerClient CreateRealSkillRunnerClient(
            ISkillAuditLog auditLog,
            SkillRunResponse responseBody,
            string contentType = "application/json")
        {
            return CreateRealSkillRunnerClient(auditLog, new List<SkillRunResponse> { responseBody }, contentType);
        }
    }

    public class FakeSkillRunnerClientInvocation
    {
        public Skill? Skill { get; init; }
        public string? Arguments { get; init; }
        public Uri? MessageUrl { get; init; }
        public IReadOnlyList<Member>? Mentions { get; init; }
        public required User Caller { get; init; }
        public BotChannelUser? Bot { get; init; }

        public HttpTriggerRequest? HttpTriggerEvent { get; init; }
        public SkillTrigger? SkillTrigger { get; init; }

        public SignalMessage? Signal { get; init; }
        public IPattern? Pattern { get; init; }
        public string? MessageId { get; init; }
        public ChatConversation? Conversation { get; init; }
        public MessageInteractionInfo? InteractionInfo { get; init; }

        public string CommandText { get; init; } = string.Empty;

        public SkillRunProperties? AuditProperties { get; init; }

        public IArguments? TokenizedArguments { get; init; }

        public Member? TriggeringMessageAuthor { get; init; }
    }
}
