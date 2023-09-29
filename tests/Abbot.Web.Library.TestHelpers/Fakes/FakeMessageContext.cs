using Microsoft.Bot.Schema;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;

namespace Serious.TestHelpers
{
    public record FakeMessageContext : MessageContext
    {
        public readonly IPlatformMessage PlatformMessage = null!;

        public IReadOnlyList<string> SentMessages => SentActivities.Select(a => a.Text).ToReadOnlyList();

        public IReadOnlyList<IMessageActivity> SentActivities => ((FakeResponder)Responder).SentMessages.ToReadOnlyList();

        public string LastReply() => SentMessages.Last();

        public string SingleReply() => SentMessages.Single();

        public IMessageActivity LastActivityReply() => SentActivities.Last();

        public IMessageActivity SingleActivityReply() => SentActivities.Single();

        public static FakeMessageContext Create() => Create("", "");

        public static FakeMessageContext Create(
            IPlatformMessage platformMessage,
            string? skillName = null,
            string? arguments = null,
            string? commandText = null,
            string? originalMessage = null,
            string? sigil = null,
            IReadOnlyList<SkillPattern>? patterns = null)
            => new(
                platformMessage,
                skillName ?? string.Empty,
                arguments ?? string.Empty,
                commandText ?? string.Empty,
                originalMessage ?? string.Empty,
                sigil ?? string.Empty,
                patterns ?? Array.Empty<SkillPattern>(),
                null);

        public static FakeMessageContext Create(
            string skillName,
            string arguments,
            string messageText = "some message text",
            Organization? organization = null,
            Member? sender = null,
            IReadOnlyList<Member>? mentions = null,
            DateTimeOffset? timestamp = null,
            string? commandText = null,
            Room? room = null,
            Conversation? conversation = null,
            IReadOnlyList<SkillPattern>? patterns = null,
            string? originalMessage = null,
            string? messageId = null,
            string? threadId = null,
            string? sigil = null,
            IPlatformMessage? platformMessage = null,
            bool directMessage = false,
            MessageInteractionInfo? messageInteractionInfo = null)
        {
            organization ??= room?.Organization ?? new Organization
            {
                Domain = "example.com",
                PlatformId = "T000000001",
                PlatformBotUserId = "U001",
            };
            room ??= new Room
            {
                Organization = organization,
                PlatformRoomId = "C000000001",
            };
            sender ??= new Member
            {
                User = new User
                {
                    PlatformUserId = "U0000000001"
                },
                Organization = organization
            };
            messageId ??= "1234567890.123456";
            commandText ??= (skillName is { Length: > 0 } ? $"{skillName} {arguments}" : string.Empty);

            var responder = new FakeResponder();

            platformMessage ??= new PlatformMessage(
                new MessageEventInfo(
                    messageText,
                    room.PlatformRoomId,
                    sender.User.PlatformUserId,
                    Array.Empty<string>(),
                    DirectMessage: directMessage,
                    Ignore: false,
                    MessageId: messageId,
                    ThreadId: threadId,
                    InteractionInfo: messageInteractionInfo,
                    Blocks: Array.Empty<ILayoutBlock>(),
                    Files: Array.Empty<FileUpload>()),
                SlackFormatter.MessageUrl(organization.Domain, room.PlatformRoomId, messageId, threadId),
                organization,
                timestamp.GetValueOrDefault(),
                responder,
                sender,
                BotChannelUser.GetBotUser(organization),
                mentions ?? Array.Empty<Member>(),
                room);

            return new FakeMessageContext(
                platformMessage,
                skillName,
                arguments,
                commandText,
                originalMessage ?? string.Empty,
                sigil ?? string.Empty,
                patterns ?? Array.Empty<SkillPattern>(),
                null)
            {
                ConversationMatch = new ConversationMatch(null, conversation)
            };
        }

        FakeMessageContext(
            IPlatformMessage platformMessage,
            string skillName,
            string arguments,
            string commandText,
            string originalMessage,
            string sigil,
            IReadOnlyList<SkillPattern> patterns,
            SkillDataScope? scope)
            : base(
                platformMessage,
                skillName,
                arguments,
                commandText,
                originalMessage,
                sigil,
                patterns,
                scope)
        {
            PlatformMessage = platformMessage;
        }

        public FakeMessageContext WithArguments(string arguments)
            => new(
                PlatformMessage,
                SkillName,
                arguments,
                CommandText,
                OriginalMessage,
                Sigil,
                Patterns,
                Scope);
    }
}
