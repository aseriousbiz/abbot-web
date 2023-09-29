#nullable enable

using System.Runtime.InteropServices;
using MarkdownLog;
using NodaTime;
using Serious.Abbot;
using Serious.Abbot.Execution;
using Serious.Abbot.Functions;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.Abbot.Scripting.Utilities;

namespace Serious.TestHelpers
{
    public class FakeBot : IExtendedBot
    {
        readonly ISignaler _signaler;

        public FakeBot(
            FakeBotBrain? brain = null,
            FakeSecrets? secrets = null,
            FakeTickets? tickets = null,
            FakeBotUtilities? utilities = null,
            FakeRoomsClient? rooms = null,
            FakeUsersClient? users = null,
            ISignaler? signaler = null)
        {
            _signaler = signaler ?? new FakeSignaler();
            ExtendedBrain = brain ?? new FakeBotBrain();
            Secrets = secrets ?? new FakeSecrets();
            Tickets = tickets ?? new FakeTickets();
            Utilities = utilities ?? new FakeBotUtilities();
            Rooms = rooms ?? new FakeRoomsClient();
            Users = users ?? new FakeUsersClient();
        }

        readonly List<string> _replies = new();
        public bool DidReply => Replies.Any();
        public IReadOnlyList<string> Replies => _replies;

        public string? MessageId { get; set; }

        public IMessage? Message { get; set; }

        public Uri? MessageUrl { get; set; }

        public string CommandText { get; } = string.Empty;

        public IExtendedBrain ExtendedBrain { get; }
        public IBrain Brain => ExtendedBrain;
        public ISecrets Secrets { get; }

        public ITicketsClient Tickets { get; }

        public IRoomsClient Rooms { get; }

        public IMetadataClient Metadata { get; } = null!;

        public ICustomersClient Customers { get; } = null!;

        public ITasksClient Tasks { get; } = null!;

        public IUsersClient Users { get; }

        public Task ReplyAsync(string text) =>
            DoReplyAsync(text);

        public Task ReplyAsync(string text, bool directMessage) =>
            DoReplyAsync(text);

        public Task ReplyAsync(string text, MessageOptions options) =>
            DoReplyAsync(text);

        Task<ProactiveBotMessageResponse> DoReplyAsync(string text)
        {
            _replies.Add(text);
            return Task.FromResult(new ProactiveBotMessageResponse(true));
        }

        public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, string? buttonsLabel, Uri? imageUrl,
            string? title)
            => ReplyWithButtonsAsync(text, buttons, buttonsLabel, imageUrl, title, null, null);


        public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, string? buttonsLabel, Uri? imageUrl, string? title,
            string color)
            => ReplyWithButtonsAsync(text, buttons, buttonsLabel, imageUrl, title, color, null);

        public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, string? buttonsLabel, Uri? imageUrl, string? title,
            MessageOptions options)
            => ReplyWithButtonsAsync(text, buttons, buttonsLabel, imageUrl, title, null, options);

        public Task ReplyWithButtonsAsync(
            string text,
            IEnumerable<Button> buttons,
            string? buttonsLabel,
            Uri? imageUrl,
            string? title,
            string? color,
            MessageOptions? options)
        {
            return DoReplyAsync(text);
        }

        public Task ReplyWithImageAsync(string image, string? text, string? title = null, Uri? titleUrl = null,
            string? color = null, MessageOptions? options = null)
        {
            return DoReplyAsync(text ?? string.Empty);
        }

        public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons)
            => ReplyWithButtonsAsync(text, buttons, null);

        public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, MessageOptions? options) =>
            DoReplyAsync(text);

        public Task ReplyTableAsync<T>(IEnumerable<T> items)
            => ReplyTableAsync(items, null);

        public Task ReplyTableAsync<T>(IEnumerable<T> items, MessageOptions? options) =>
            DoReplyAsync(items.ToMarkdownTable().ToMarkdown());

        public Task ReplyLaterAsync(string text, long delayInSeconds)
            => ReplyLaterAsync(text, delayInSeconds, null);

        public Task ReplyLaterAsync(string text, long delayInSeconds, MessageOptions? options) =>
            DoReplyAsync(text);

        public Task ReplyLaterAsync(string text, TimeSpan timeSpan)
            => ReplyLaterAsync(text, timeSpan, null);

        public Task ReplyLaterAsync(string text, TimeSpan timeSpan, MessageOptions? options) =>
            DoReplyAsync(text);

        public Task<IResult> SignalAsync(string name, string arguments)
        {
            return _signaler.SignalAsync(name, arguments);
        }

        Task<ProactiveBotMessageResponse> IExtendedBot.ReplyAsync(string text)
            => DoReplyAsync(text);

        Task<ProactiveBotMessageResponse> IExtendedBot.ReplyAsync(string text, MessageOptions options)
            => DoReplyAsync(text);
        Task<ProactiveBotMessageResponse> IExtendedBot.ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, MessageOptions? options)
            => DoReplyAsync(text);

        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;

        public string PlatformId { get; set; } = null!;
        public string SkillName { get; set; } = null!;
        public Uri SkillUrl => new($"https://localhost/skills/{SkillName}");
        public IRoom Room { get; } = new PlatformRoom("fake", "fake");

        public CustomerInfo? Customer => null;

        public IMessageTarget? Thread { get; set; }

        public string RuntimeDescription => RuntimeInformation.FrameworkDescription;

        public Task SendRoomNotificationAsync(RoomNotification roomNotification, IRoomMessageTarget? room = null)
        {
            throw new NotImplementedException();
        }

        public IArguments Arguments { get; set; } = null!;
        public IChatUser From { get; set; } = null!;
        public IReadOnlyList<IChatUser> Mentions { get; set; } = null!;
        public IBotHttpClient Http => new FakeBotHttpClient();
        public IHttpTriggerEvent Request => null!;

        public IHttpTriggerResponse Response => new HttpTriggerResponse();

        public bool IsRequest => false;
        public bool IsInteraction { get; set; }
        public bool IsChat { get; set; }

        public bool IsPlaybook { get; set; }

        public IDictionary<string, object?> Outputs { get; } = new Dictionary<string, object?>();

        public bool IsPatternMatch => Pattern is not null;
        public IPattern? Pattern { get; set; }
        public DateTimeZone TimeZone => DateTimeZoneProviders.Tzdb.GetSystemDefault();
        public IVersionInfo VersionInfo { get; } = new VersionInfo();

        public ISlack Slack => throw new NotImplementedException();

        public IUtilities Utilities { get; }
        public PlatformType PlatformType { get; set; }
        public ISignalEvent? SignalEvent { get; set; }
        public IConversation? Conversation { get; set; }
        public SkillDataScope Scope { get; set; }
        public string? ContextId { get; set; }
    }
}
