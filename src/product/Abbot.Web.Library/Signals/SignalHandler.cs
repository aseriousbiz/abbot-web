using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Logging;

namespace Serious.Abbot.Signals;

/// <summary>
/// Handles incoming signals raised from a user skill.
/// </summary>
public class SignalHandler : ISignalHandler
{
    static readonly ILogger<SignalHandler> Log = ApplicationLoggerFactory.CreateLogger<SignalHandler>();

    readonly ISkillRunnerClient _skillRunnerClient;
    readonly ISkillRepository _skillRepository;
    readonly IRoomRepository _roomRepository;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly IOrganizationRepository _organizationRepository;
    readonly IUserRepository _userRepository;
    readonly IConversationRepository _conversationRepository;
    readonly IUrlGenerator _urlGenerator;
    readonly IBackgroundJobClient _backgroundJobClient;

    /// <summary>
    /// Constructs a <see cref="SignalHandler"/>.
    /// </summary>
    /// <param name="skillRunnerClient">Client to the Abbot skill runners used to call a skill.</param>
    /// <param name="skillRepository">Repository used to manage skills in the database.</param>
    /// <param name="roomRepository">Repository used to manage rooms in the database.</param>
    /// <param name="playbookDispatcher"></param>
    /// <param name="organizationRepository">Repository used to retrieve organizations.</param>
    /// <param name="userRepository">Repository used to manage members of an organization.</param>
    /// <param name="conversationRepository">Repository used to manage conversations in an organization.</param>
    /// <param name="urlGenerator"><see cref="IUrlGenerator"/> so we can generate the URL to the skill on the Abbot website to pass to the skill.</param>
    /// <param name="backgroundJobClient">Runs tasks in a background thread or process.</param>
    public SignalHandler(
        ISkillRunnerClient skillRunnerClient,
        ISkillRepository skillRepository,
        IRoomRepository roomRepository,
        PlaybookDispatcher playbookDispatcher,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IConversationRepository conversationRepository,
        IUrlGenerator urlGenerator,
        IBackgroundJobClient backgroundJobClient)
    {
        _skillRunnerClient = skillRunnerClient;
        _skillRepository = skillRepository;
        _roomRepository = roomRepository;
        _playbookDispatcher = playbookDispatcher;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _conversationRepository = conversationRepository;
        _urlGenerator = urlGenerator;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// Receives a <see cref="SignalRequest"/> and enqueues a call to <see cref="HandleSignalAsync"/>
    /// which will handle the signal by calling all subscribers to that signal in the organization.
    /// </summary>
    /// <param name="skillId">The ID of the skill raising the signal.</param>
    /// <param name="signalRequest">The <see cref="SignalRequest"/> containing information about the signal to create.</param>
    public bool EnqueueSignalHandling(Id<Skill> skillId, SignalRequest signalRequest)
    {
        if (signalRequest.ContainsCycle())
        {
            return false;
        }

        var traceParent = Activity.Current?.Id ?? string.Empty;

        _backgroundJobClient.Enqueue(() => HandleSignalAsync(skillId, signalRequest, traceParent));
        return true;
    }

    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task HandleSystemSignalAsync(
        SystemSignal signal,
        Id<Organization> organizationId,
        string arguments,
        PlatformRoom room,
        Id<Member> senderId,
        MessageInfo? triggeringMessage)
    {
        var organization = await _organizationRepository.GetAsync(organizationId);
        if (organization is null)
        {
            Log.EntityNotFound(organizationId, typeof(Organization));
            return;
        }
        using var orgScope = Log.BeginOrganizationScope(organization);

        Uri? triggeringMessageUrl = triggeringMessage?.MessageUrl;
        string? triggeringMessageId = triggeringMessage?.MessageId;
        string? triggeringThreadId = triggeringMessage?.ThreadId;
        string? triggeringMessageText = triggeringMessage?.Text;
        var conversationId = triggeringMessage?.ConversationId;

        var source = new SignalSourceMessage
        {
            AuditIdentifier = Guid.NewGuid(),
            SkillName = Skill.SystemSkillName,
            Mentions = Array.Empty<PlatformUser>(),
            Arguments = arguments,
            IsChat = false,
            IsInteraction = false,
            IsRequest = false,
        };
        var sender = senderId == default
            ? null
            : await _userRepository.GetMemberByIdAsync(senderId);
        // There is *always* a sender.
        sender ??= await _userRepository.EnsureAbbotMemberAsync(organization);

        var triggeringMessageAuthor = triggeringMessage is not null
            ? await _userRepository.GetMemberByIdAsync(triggeringMessage.SenderId)
            : null;

        using var memberScope = Log.BeginMemberScope(sender);
        await CallSignalSubscribersAsync(
            organization,
            signal.Name,
            arguments,
            sourceSkillId: Skill.SystemSkillId,
            sourceSkillName: Skill.SystemSkillName,
            room,
            source,
            sender,
            triggeringMessageUrl,
            triggeringMessageId,
            triggeringThreadId,
            triggeringMessageText,
            triggeringMessageAuthor,
            conversationId);
    }

    /// <summary>
    /// Receives a <see cref="SignalRequest"/> and calls all skills in the organization subscribed to that signal.
    /// </summary>
    /// <remarks>
    /// This method must be public and every argument of this method must be serializable because
    /// we run this in a background job.
    /// </remarks>
    /// <param name="skillId">The ID of the skill raising the signal.</param>
    /// <param name="signalRequest">The <see cref="SignalRequest"/> containing information about the signal to create.</param>
    /// <param name="traceParent">The trace parent to use for this enqueued job.</param>
    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task HandleSignalAsync(Id<Skill> skillId, SignalRequest signalRequest, string traceParent)
    {
        using var activity = ActivityHelper.CreateAndStart<SignalHandler>(traceParent);
        Log.ProcessingSignalStart(signalRequest.Name, signalRequest.Arguments, skillId);

        var skill = await _skillRepository.GetByIdAsync(skillId);
        if (skill is null)
        {
            Log.EntityNotFound(skillId, typeof(Skill));
            return;
        }

        var senderId = (Id<Member>)signalRequest.SenderId;
        var sender = await _userRepository.GetMemberByIdAsync(senderId, skill.Organization)
                     ?? throw new InvalidOperationException($"Member {signalRequest.SenderId} not found.");

        var conversationId = signalRequest.ConversationId is { Length: > 0 }
            ? Id<Conversation>.Parse(signalRequest.ConversationId, CultureInfo.InvariantCulture)
            : Id.Null<Conversation>();

        await CallSignalSubscribersAsync(
            skill.Organization,
            signalRequest.Name,
            signalRequest.Arguments,
            skill,
            sourceSkillName: skill.Name,
            signalRequest.Room,
            signalRequest.Source,
            sender,
            triggeringMessageUrl: null,
            triggeringMessageId: null,
            triggeringThreadId: null,
            triggeringMessageText: null,
            triggeringMessageAuthor: null,
            conversationId);
    }

    // Retrieves all the subscribers (skills and playbooks) and calls them!
    async Task CallSignalSubscribersAsync(
        Organization organization,
        string signalName,
        string arguments,
        Id<Skill> sourceSkillId,
        string sourceSkillName,
        PlatformRoom room,
        SignalSourceMessage source,
        Member sender,
        Uri? triggeringMessageUrl,
        string? triggeringMessageId,
        string? triggeringThreadId,
        string? triggeringMessageText,
        Member? triggeringMessageAuthor,
        Id<Conversation>? conversationId)
    {
        var conversation = conversationId is not null
            ? await _conversationRepository.GetConversationAsync(conversationId.Value)
            : null;
        using var convoScopes =
            Log.BeginConversationRoomAndHubScopes(conversation);

        // Validate the conversation is in the organization.
        // The organization ID is trusted information since it either came from:
        // * A System Signal invocation - which happened in process and already validated the correct organization.
        // * A Signal request from a Skill Runner, which derived it from the Skill ID, which was validated against the API token we issued when sending the request to the runner.
        if (conversation is not null && conversation.OrganizationId != organization.Id)
        {
            Log.SignalReferencedUnauthorizedEntity(
                "Conversation",
                conversation.Id,
                conversation.OrganizationId,
                organization.Id);

            // Drop the signal, it's Bad(TM).
            return;
        }

        var dbRoom = await _roomRepository.GetRoomByPlatformRoomIdAsync(room.Id, organization);
        var customer = dbRoom?.Customer?.ToCustomerInfo();
        var outputs = new OutputsBuilder()
            .SetRoom(dbRoom)
            .SetConversation(conversation)
            .SetMessage(dbRoom, triggeringMessageId, triggeringThreadId, triggeringMessageText, triggeringMessageUrl)
            .Outputs;

        var signal = new SignalMessage
        {
            Name = signalName,
            Arguments = arguments,
            Source = source,
        };

        outputs["signal"] = signal.Name;
        outputs["arguments"] = signal.Arguments;
        await _playbookDispatcher.DispatchSignalAsync(signal, outputs, organization);

        await CallSubscribedSkillsAsync(
            organization,
            signalName,
            arguments,
            sourceSkillId,
            sourceSkillName,
            room,
            customer,
            source,
            sender,
            triggeringMessageUrl,
            triggeringMessageId,
            triggeringThreadId,
            triggeringMessageText,
            triggeringMessageAuthor,
            conversation);
    }

    async Task CallSubscribedSkillsAsync(
        Organization organization,
        string signalName,
        string arguments,
        Id<Skill> sourceSkillId,
        string sourceSkillName,
        PlatformRoom room,
        CustomerInfo? customer,
        SignalSourceMessage source,
        Member sender,
        Uri? triggeringMessageUrl,
        string? triggeringMessageId,
        string? triggeringThreadId,
        string? triggeringMessageText,
        Member? triggeringMessageAuthor,
        Conversation? conversation)
    {
        // Get all the candidate skills that subscribe to this signal by name.
        var candidates = await _skillRepository
            .GetSkillListQueryable(organization)
            .Include(s => s.SignalSubscriptions)
            .Where(s => s.SignalSubscriptions.Any(sig => sig.Name == signalName))
            .ToListAsync();

        // Further filter skills that subscribe to this signal by arguments. This logic can't be run in
        // the database hence this extra step.
        var subscribers = candidates
            .Where(s => s.SignalSubscriptions.Any(sub => sub.Match(arguments)))
            .ToList();

        Log.SignalSubscriberCount(
            subscribers.Count,
            signalName,
            sourceSkillId,
            sourceSkillName,
            organization.PlatformId,
            organization.PlatformType);

        if (!subscribers.Any())
        {
            return;
        }

        var signalMessage = new SignalMessage
        {
            Name = signalName,
            Arguments = arguments,
            Source = source
        };

        var mentions = await _userRepository.ParseMentions(arguments, organization);

        // It's tempting to parallelize this, but we can't do that as-is.
        // Each call to CallSubscribedSkillAsync will invoke ISkillRunnerClient to call the skill.
        // Each call to ISkillRunnerClient will query ISettingsManager to check for custom endpoints.
        // EF won't allow concurrent queries to the same DbContext, so we can't do this in parallel.
        // In the future, we can use Mass Transit to do something different here: https://github.com/aseriousbiz/abbot/issues/3824
        foreach (var subscriber in subscribers)
        {
            await CallSubscribedSkillAsync(
                signalName,
                arguments,
                triggeringMessageText,
                subscriber,
                mentions,
                sender,
                organization,
                room,
                customer,
                signalMessage,
                triggeringMessageUrl,
                triggeringMessageId,
                triggeringThreadId,
                triggeringMessageAuthor,
                conversation);
        }
    }

    async Task CallSubscribedSkillAsync(
        string signalName,
        string arguments,
        string? messageText,
        Skill subscribedSkill,
        IReadOnlyList<Member> mentions,
        Member sender,
        Organization organization,
        PlatformRoom room,
        CustomerInfo? customer,
        SignalMessage signalMessage,
        Uri? triggeringMessageUrl,
        string? triggeringMessageId,
        string? triggeringThreadId,
        Member? triggeringMessageAuthor,
        Conversation? conversation)
    {
        var skillUrl = _urlGenerator.SkillPage(subscribedSkill.Name);

        var chatConversation = conversation?.ToChatConversation(_urlGenerator.ConversationDetailPage(conversation.Id));

        try
        {
            await _skillRunnerClient.SendAsync(
                subscribedSkill,
                new Arguments(arguments, mentions.Select(m => m.ToPlatformUser())),
                messageText ?? $"{subscribedSkill.Name} {arguments}",
                mentions,
                sender,
                BotChannelUser.GetBotUser(organization),
                room,
                customer,
                skillUrl,
                signal: signalMessage,
                messageUrl: triggeringMessageUrl,
                messageId: triggeringMessageId,
                threadId: triggeringThreadId,
                triggeringMessageAuthor: triggeringMessageAuthor,
                conversation: chatConversation,
                auditProperties: new()
                {
                    CommandText = $"{subscribedSkill.Name} {arguments}",
                });
        }
        catch (Exception e)
        {
            Log.ExceptionProcessingSignal(e, signalName, subscribedSkill.Name, e.ToString());
        }
    }
}

public static partial class SignalHandlerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Signal referenced a {EntityType} entity {EntityId} that belongs to organization {EntityOwnerOrganizationId}, but the request came from organization {InvocationOrganizationId}")]
    public static partial void SignalReferencedUnauthorizedEntity(this ILogger logger, string entityType, int entityId,
        int entityOwnerOrganizationId, int invocationOrganizationId);

    [LoggerMessage(
        EventId = 4025,
        Level = LogLevel.Error,
        Message =
            "Signal request does not have a conversation reference. (Name: {SignalName}, Source Skill: {SkillId})")]
    public static partial void SignalMissingConversationReference(this ILogger logger, string signalName, int skillId);

    [LoggerMessage(
        EventId = 4026,
        Level = LogLevel.Error,
        Message =
            "Failed to process signal (Name: {SignalName}, Subscribed Skill: {SkillName}, Error: {ErrorMessage})")]
    public static partial void ExceptionProcessingSignal(
        this ILogger<SignalHandler> logger,
        Exception? exception,
        string signalName,
        string skillName,
        string errorMessage);
}
