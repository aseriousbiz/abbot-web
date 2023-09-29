using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MarkdownLog;
using NodaTime;
using Serious.Abbot.Execution;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Models;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Abbot.Scripting.Utilities;

namespace Serious.Abbot.Functions;

/// <summary>
/// It's Abbot! Provides context and a set of services and information for your bot skill.
/// </summary>
public class AbbotBot : IExtendedBot, ISignaler
{
    readonly ISkillContextAccessor _skillContextAccessor;
    readonly IBotReplyClient _botReplyClient;
    readonly ISignaler _signaler;
    IHttpTriggerEvent? _httpTriggerEvent;
    IHttpTriggerResponse? _httpTriggerResponse;
    IArguments? _arguments;
    ISignalEvent? _signalEvent;

    /// <summary>
    /// Constructs a <see cref="AbbotBot" /> with a set of services and information for skill authors to use.
    /// </summary>
    /// <param name="skillContextAccessor">Used to access the context for the current skill.</param>
    /// <param name="brain">Used by skill authors to store and retrieve information.</param>
    /// <param name="secrets">Access to the secrets key vault</param>
    /// <param name="tickets">Access to tickets.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to make a request to the Abbot skill runner APIs.</param>
    /// <param name="botReplyClient">Used to send replies from a skill back to caller of the skill.</param>
    /// <param name="utilities">A useful grab bag of utility methods for C# skill authors.</param>
    /// <param name="roomsClient">Used to manage Slack conversations.</param>
    /// <param name="usersClient">Used to retrieve information about users.</param>
    /// <param name="signaler">Used to raise a signal that other skills can subscribe and respond to.</param>
    /// <param name="slackClient">Used to make block kit requests to Slack and access the UI elements the user interacted with.</param>
    /// <param name="customersClient">Client used to manage customers.</param>
    /// <param name="metadataClient">Client used to manage available metadata fields for an organization.</param>
    /// <param name="tasksClient">Client used to manage tasks.</param>
    public AbbotBot(
        ISkillContextAccessor skillContextAccessor,
        IExtendedBrain brain,
        ISecrets secrets,
        ITicketsClient tickets,
        IBotHttpClient httpClient,
        IBotReplyClient botReplyClient,
        IUtilities utilities,
        IRoomsClient roomsClient,
        IUsersClient usersClient,
        ISignaler signaler,
        ISlack slackClient,
        ICustomersClient customersClient,
        IMetadataClient metadataClient,
        ITasksClient tasksClient)
    {
        _skillContextAccessor = skillContextAccessor;
        _botReplyClient = botReplyClient;
        _signaler = signaler;

        Http = httpClient;
        ExtendedBrain = brain;
        Secrets = secrets;
        Tickets = tickets;
        Utilities = utilities;
        Rooms = roomsClient;
        Users = usersClient;
        TimeZone = DateTimeZone.Utc;
        Slack = slackClient;
        Customers = customersClient;
        Metadata = metadataClient;
        Tasks = tasksClient;
    }

    SkillContext SkillContext => _skillContextAccessor.SkillContext
                                 ?? throw new InvalidOperationException(
                                     $"{nameof(SkillContextAccessor)}.{nameof(SkillContextAccessor.SkillContext)} must be set before accessing it.");

    public SkillInfo SkillInfo => SkillContext.SkillInfo;
    public SkillRunnerInfo SkillRunnerInfo => SkillContext.SkillRunnerInfo;

    public string? MessageId => Message?.Id
#pragma warning disable CS0618
                                ?? SkillInfo.MessageId;
#pragma warning restore CS0618

    IMessage? _message;
    public IMessage? Message
    {
        get {
            // This funky late evaluation is to avoid changing a crap load of tests.
            // Yeah, it's ugly. We can fix it later.
            return _message ??= SkillInfo.Message is { } msg
                ? new SourceMessage
                {
                    Text = msg.Text,
                    Id = msg.MessageId,
                    ThreadId = msg.ThreadId,
                    Url = msg.MessageUrl,
                    Author = msg.Author,
                }
                : null;
        }
    }

    public Uri? MessageUrl => Message?.Url
#pragma warning disable CS0618
                              ?? SkillInfo.MessageUrl;
#pragma warning restore CS0618

    public string CommandText => SkillInfo.CommandText;

    public IMessageTarget? Thread => (Message?.ThreadId
#pragma warning disable CS0618
                                      ?? SkillInfo.ThreadId
#pragma warning restore CS0618
                                      ?? MessageId) is { Length: > 0 } threadId
        ? Room.GetThread(threadId)
        : null;

    /// <summary>
    /// The extra brain bits that aren't ready for mainstream consumption.
    /// </summary>
    public IExtendedBrain ExtendedBrain { get; }

    public IBrain Brain => ExtendedBrain;

    public ISecrets Secrets { get; }

    public ITicketsClient Tickets { get; }

    public IRoomsClient Rooms { get; }

    public IMetadataClient Metadata { get; }

    public ICustomersClient Customers { get; }

    public ITasksClient Tasks { get; }

    public IUsersClient Users { get; }

    public Task ReplyAsync(string text) =>
        ReplyLaterAsync(text, TimeSpan.Zero);

    Task<ProactiveBotMessageResponse> IExtendedBot.ReplyAsync(string text) =>
        _botReplyClient.SendReplyAsync(text, TimeSpan.Zero, null);

    public Task ReplyAsync(string text, bool directMessage)
    {
        return ReplyLaterAsync(text,
            TimeSpan.Zero,
            new()
            {
                To = From
            });
    }

    public Task ReplyAsync(string text, MessageOptions options) =>
        _botReplyClient.SendReplyAsync(text, TimeSpan.Zero, options);

    Task<ProactiveBotMessageResponse> IExtendedBot.ReplyAsync(string text, MessageOptions options) =>
        _botReplyClient.SendReplyAsync(text, TimeSpan.Zero, options);

    public Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title) => ReplyWithButtonsAsync(text, buttons, buttonsLabel, imageUrl, title, null, null);

    public Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title,
        string? color) => ReplyWithButtonsAsync(text, buttons, buttonsLabel, imageUrl, title, color, null);

    public Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title,
        MessageOptions? options) => ReplyWithButtonsAsync(text, buttons, buttonsLabel, imageUrl, title, null, options);

    public Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title,
        string? color,
        MessageOptions? options)
    {
        return _botReplyClient.SendReplyAsync(
            text,
            TimeSpan.Zero,
            buttons,
            buttonsLabel,
            imageUrl?.ToString(),
            title,
            null,
            color,
            options);
    }

    public Task ReplyWithImageAsync(
        string image,
        string? text = null,
        string? title = null,
        Uri? titleUrl = null,
        string? color = null,
        MessageOptions? options = null)
    {
        return _botReplyClient.SendReplyAsync(
            text ?? string.Empty,
            TimeSpan.Zero,
            Enumerable.Empty<Button>(),
            null,
            image,
            title,
            titleUrl,
            color,
            options
        );
    }

    public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons) =>
        _botReplyClient.SendReplyAsync(text, TimeSpan.Zero, buttons, null, null, null, null, null, null);

    public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, MessageOptions? options) =>
        _botReplyClient.SendReplyAsync(text, TimeSpan.Zero, buttons, null, null, null, null, null, options);

    Task<ProactiveBotMessageResponse> IExtendedBot.ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, MessageOptions? options) =>
        _botReplyClient.SendReplyAsync(text, TimeSpan.Zero, buttons, null, null, null, null, null, options);

    public Task ReplyTableAsync<T>(IEnumerable<T> items) => ReplyTableAsync(items, null);

    public Task ReplyTableAsync<T>(IEnumerable<T> items, MessageOptions? options)
    {
        var table = items.ToMarkdownTable();
        return ReplyLaterAsync($"```\n{table.ToMarkdown()}```", TimeSpan.Zero, options);
    }

    public Task ReplyLaterAsync(string text, long delayInSeconds) => ReplyLaterAsync(text, delayInSeconds, null);

    public Task ReplyLaterAsync(string text, long delayInSeconds, MessageOptions? options)
    {
        return ReplyLaterAsync(text, TimeSpan.FromSeconds(delayInSeconds), options);
    }

    public Task ReplyLaterAsync(string text, TimeSpan timeSpan) => ReplyLaterAsync(text, timeSpan, null);

    public Task ReplyLaterAsync(string text, TimeSpan timeSpan, MessageOptions? options)
    {
        return _botReplyClient.SendReplyAsync(text, timeSpan, options);
    }

    public async Task<IResult> SignalAsync(string name, string arguments)
    {
        return await _signaler.SignalAsync(name, arguments);
    }

    public string Id => SkillInfo.Bot.Id;

    public string Name => SkillInfo.Bot.UserName;

    public string PlatformId => SkillInfo.PlatformId;

    public string SkillName => SkillInfo.SkillName;

    public Uri SkillUrl => SkillInfo.SkillUrl;

    public IRoom Room => SkillInfo.Room;

    public CustomerInfo? Customer => SkillInfo.Customer;

    public IArguments Arguments =>
        _arguments ??= new Arguments(SkillInfo.TokenizedArguments, SkillInfo.Arguments);

    public IChatUser From => SkillInfo.From;
    public IReadOnlyList<IChatUser> Mentions => SkillInfo.Mentions;

    public IConversation? Conversation => SkillContext.ConversationInfo;

    public IReadOnlyList<string> Replies => _botReplyClient.Replies.ToReadOnlyList();

    public bool IsPlaybook => SkillInfo.IsPlaybook;

    public IDictionary<string, object?> Outputs { get; } = new Dictionary<string, object?>();

    public SkillDataScope Scope => SkillRunnerInfo.Scope;

    public string? ContextId => SkillRunnerInfo.ContextId;

    public IBotHttpClient Http { get; }

    public IHttpTriggerEvent Request =>
        _httpTriggerEvent ??= SkillInfo.Request is { } httpTriggerRequest
            ? new HttpTriggerEvent(httpTriggerRequest)
            : HttpTriggerEvent.Empty;

    public IHttpTriggerResponse Response =>
        _httpTriggerResponse ??= Request != HttpTriggerEvent.Empty
            ? new HttpTriggerResponse()
            : HttpTriggerResponse.Invalid;

    public bool IsRequest => SkillInfo.IsRequest;

    public bool IsInteraction => SkillInfo.IsInteraction;

    public bool IsChat => SkillInfo.IsChat;

    /// <summary>
    /// If true, the skill is responding to a chat message because it matched a pattern, not because it was
    /// directly called.
    /// </summary>
    public bool IsPatternMatch => Pattern is not null;

    /// <summary>
    /// If the skill is responding to a pattern match, then this contains information about the pattern that
    /// matched the incoming message and caused this skill to be called. Otherwise this is null.
    /// </summary>
    public IPattern? Pattern => SkillInfo.Pattern;

    public DateTimeZone TimeZone { get; }

    public IVersionInfo VersionInfo { get; } = new VersionInfo();

    public ISlack Slack { get; }

    public IUtilities Utilities { get; }

    public bool DidReply => _botReplyClient.DidReply;

    public override string ToString() => $"<@{Id}>";

    public ISignalEvent? SignalEvent =>
        _signalEvent ??= SkillContext.SignalInfo is not null
            ? new SignalEvent(SkillContext.SignalInfo)
            : null;

    /// <inheritdoc />
    public string RuntimeDescription => RuntimeInformation.FrameworkDescription;
}
