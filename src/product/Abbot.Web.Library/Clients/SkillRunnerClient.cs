using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Clients;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Logging;
using Serious.Slack;
using Activity = System.Diagnostics.Activity;

namespace Serious.Abbot.Messages;

/// <summary>
/// Client to the Abbot skill runners which are Azure Functions that actually
/// execute the skill code.
/// </summary>
public class SkillRunnerClient : ISkillRunnerClient
{
    static readonly MediaTypeFormatter[] MediaTypeFormatters = { new AbbotJsonMediaTypeFormatter() };
    static readonly ILogger<SkillRunnerClient> Log = ApplicationLoggerFactory.CreateLogger<SkillRunnerClient>();

    readonly IRoomRepository _roomRepository;
    readonly HttpClient _httpClient;
    readonly IRunnerEndpointManager _runnerEndpointManager;
    readonly IApiTokenFactory _apiTokenFactory;
    readonly IPermissionRepository _permissionRepository;
    readonly IUrlGenerator _urlGenerator;
    readonly ISkillRunnerRetryPolicy _retryPolicy;
    readonly ISkillAuditLog _auditLog;

    /// <summary>
    /// Constructs a <see cref="SkillRunnerClient" /> used to call user-defined skills.
    /// </summary>
    /// <param name="roomRepository">The room repository.</param>
    /// <param name="httpClient">A <see cref="HttpClient"/> used to call the skill runner.</param>
    /// <param name="runnerEndpointManager">The endpoints for our configured skill runners.</param>
    /// <param name="apiTokenFactory">A factory to retrieve an Api token used to call the runner.</param>
    /// <param name="permissionRepository">A <see cref="IPermissionRepository"/> so we can check permissions.</param>
    /// <param name="urlGenerator">The url generator.</param>
    /// <param name="retryPolicy">The retry policy to use when calling a skill.</param>
    /// <param name="auditLog"><see cref="ISkillAuditLog"/> to audit this skill call.</param>
    public SkillRunnerClient(
        IRoomRepository roomRepository,
        HttpClient httpClient,
        IRunnerEndpointManager runnerEndpointManager,
        IApiTokenFactory apiTokenFactory,
        IPermissionRepository permissionRepository,
        IUrlGenerator urlGenerator,
        ISkillRunnerRetryPolicy retryPolicy,
        ISkillAuditLog auditLog)
    {
        _roomRepository = roomRepository;
        _httpClient = httpClient;
        _runnerEndpointManager = runnerEndpointManager;
        var requestAccepts = _httpClient.DefaultRequestHeaders.Accept;
        requestAccepts.Add(new MediaTypeWithQualityHeaderValue("application/vnd.abbot.v1+json", 0.9));
        _apiTokenFactory = apiTokenFactory;
        _permissionRepository = permissionRepository;
        _urlGenerator = urlGenerator;
        _retryPolicy = retryPolicy;
        _auditLog = auditLog;
    }

    public async Task<SkillRunResponse> SendAsync(
        Skill skill,
        IArguments arguments,
        string commandText,
        IEnumerable<Member> mentions,
        Member sender,
        BotChannelUser bot,
        PlatformRoom platformRoom,
        CustomerInfo? customer,
        Uri skillUrl,
        bool isInteraction = false,
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
        SkillRunProperties? auditProperties = null)
    {
        var auditId = Guid.NewGuid();
        var parentAuditId = signal?.Source.AuditIdentifier;

        if (!await _permissionRepository.CanRunAsync(sender, skill))
        {
            var response = new SkillRunResponse
            {
                Success = false,
                Errors = new[]
                {
                    new RuntimeError
                    {
                        Description = "The user does not have permission to run this skill."
                    }
                },
                Replies = new List<string>
                {
                    $"I’m afraid I can’t do that, {sender.User.FormatMention()}. "
                    + $"`<@{skill.Organization.PlatformBotUserId}> who can {skill.Name}` to find out who can change permissions "
                    + "for this skill."
                },
            };

            await _auditLog.LogSkillRunAsync(
                skill,
                arguments,
                pattern,
                signal?.Name,
                sender.User,
                platformRoom,
                response,
                auditId,
                parentAuditId,
                auditProperties);

            return response;
        }

        var message = CreateMessageInstance(
            auditId,
            skill,
            arguments,
            commandText,
            bot,
            sender,
            mentions,
            platformRoom,
            customer,
            skillUrl,
            conversation,
            pattern: pattern,
            signal: signal,
            messageUrl: messageUrl,
            messageId: messageId,
            threadId: threadId,
            triggeringMessageAuthor: triggeringMessageAuthor,
            isChat: signal is null,
            isInteraction: isInteraction,
            room: room,
            passiveReplies: passiveReplies);

        try
        {
            Activity.Current?.AddBaggage("abbot.activity.id", auditId.ToString());
            var response = await Send(skill, sender, message);
            await _auditLog.LogSkillRunAsync(
                skill,
                arguments,
                pattern,
                signal?.Name,
                sender.User,
                platformRoom,
                response,
                auditId,
                parentAuditId,
                auditProperties);
            return response;
        }
        catch (Exception e)
        {
#pragma warning disable CA1508
            var isCustomRunnerError = e is SkillRunException { CustomRunner: true };
#pragma warning restore CA1508
            await _auditLog.LogSkillRunAsync(
                skill,
                arguments,
                pattern,
                signal?.Name,
                sender.User,
                platformRoom,
                e,
                auditId,
                parentAuditId,
                isCustomRunnerError,
                auditProperties);

            // Only propagate the exception if we're not using a custom runner.
            // In a custom runner, the audit log event is sufficient logging.
            if (!isCustomRunnerError)
            {
                throw;
            }

            return new SkillRunResponse
            {
                Success = false,
                Errors = new[]
                {
                    new RuntimeError
                    {
                        Description = e.Message
                    }
                },
            };
        }
    }

    /// <summary>
    /// Forwards an HttpTrigger event to a skill.
    /// </summary>
    /// <param name="trigger">The Http trigger to invoke.</param>
    /// <param name="triggerRequest">The HTTP request that caused the trigger.</param>
    /// <param name="skillUrl">The URL on ab.bot to edit the skill. This is passed to the skill.</param>
    /// <param name="auditId">The Id for the audit log entry for this skill run.</param>
    public Task<SkillRunResponse> SendHttpTriggerAsync(
        SkillHttpTrigger trigger,
        HttpTriggerRequest triggerRequest,
        Uri skillUrl,
        Guid auditId)
    {
        var arguments = triggerRequest.RawBody ?? string.Empty;
        // This is logged in TriggerController because there is more
        // useful context there we want to include in the log
        return SendTriggerAsync(trigger, arguments, skillUrl, auditId, triggerRequest);
    }

    /// <summary>
    /// Forwards a scheduled trigger event to a skill.
    /// </summary>
    /// <param name="trigger">The scheduled trigger to invoke.</param>
    /// <param name="skillUrl">The URL on ab.bot to edit the skill. This is passed to the skill.</param>
    /// <param name="auditId">The Id for the audit log entry for this skill run.</param>
    public async Task<SkillRunResponse> SendScheduledTriggerAsync(SkillScheduledTrigger trigger, Uri skillUrl,
        Guid auditId)
    {
        var response = await SendTriggerAsync(
            trigger,
            trigger.Arguments ?? string.Empty,
            skillUrl,
            auditId);

        await _auditLog.LogScheduledTriggerRunEventAsync(trigger, response, auditId);

        return response;
    }

    /// <summary>
    /// Forwards a playbook action event to a skill.
    /// </summary>
    /// <param name="trigger">The playbook action to invoke.</param>
    /// <param name="skillUrl">The URL on ab.bot to edit the skill. This is passed to the skill.</param>
    /// <param name="auditId">The Id for the audit log entry for this skill run.</param>
    public async Task<SkillRunResponse> SendPlaybookActionTriggerAsync(SkillPlaybookActionTrigger trigger, Uri skillUrl,
        Guid auditId)
    {
        var response = await SendTriggerAsync(
            trigger,
            trigger.Arguments ?? string.Empty,
            skillUrl,
            auditId,
            trigger.TriggerRequest,
            trigger.SignalMessage,
            isPlaybook: true);

        await _auditLog.LogPlaybookActionTriggerRunEventAsync(trigger, response, auditId);

        return response;
    }

    async Task<SkillRunResponse> SendTriggerAsync(
        SkillTrigger trigger,
        string arguments,
        Uri skillUrl,
        Guid auditId,
        HttpTriggerRequest? httpTriggerEvent = null,
        SignalMessage? signalMessage = null,
        bool isPlaybook = false)
    {
        var skill = trigger.Skill;

        var dbRoom = await _roomRepository.GetRoomByPlatformRoomIdAsync(trigger.RoomId, skill.Organization);
        var customer = dbRoom?.Customer?.ToCustomerInfo();

        var room = GetRoom(trigger);

        var bot = BotChannelUser.GetBotUser(skill.Organization);

        var sender = GetTriggerCreator(trigger, skill);

        var message = CreateMessageInstance(
            auditId,
            skill,
            new Arguments(arguments),
            arguments,
            bot,
            sender,
            Enumerable.Empty<Member>(),
            room,
            customer,
            skillUrl,
            httpTriggerEvent: httpTriggerEvent,
            signal: signalMessage,
            isChat: false,
            isPlaybook: isPlaybook);

        return await Send(skill, sender, message);
    }

    static SkillMessage CreateMessageInstance(
        Guid auditId,
        Skill skill,
        IArguments arguments,
        string messageText,
        BotChannelUser bot,
        Member sender,
        IEnumerable<Member> mentions,
        PlatformRoom platformRoom,
        CustomerInfo? customer,
        Uri skillUrl,
        ChatConversation? conversation = null,
        HttpTriggerRequest? httpTriggerEvent = null,
        IPattern? pattern = null,
        SignalMessage? signal = null,
        Uri? messageUrl = null,
        string? messageId = null,
        string? threadId = null,
        Member? triggeringMessageAuthor = null,
        bool isChat = true,
        bool isInteraction = false,
        Room? room = null,
        bool passiveReplies = false,
        bool isPlaybook = false)
    {
        var organization = skill.Organization;
        var mentionedUsers = mentions
            .Select(m => m.ToPlatformUser())
            .ToList();

        var code = skill.Language is CodeLanguage.CSharp or CodeLanguage.Ink
            ? skill.CacheKey
            : skill.Code;

        var from = sender.ToPlatformUser();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new SkillMessage
        {
            SkillInfo = new SkillInfo
            {
#pragma warning disable CS0618
                // We'll explicitly set these until we transition fully over.
                MessageId = messageId,
                MessageUrl = messageUrl,
                ThreadId = threadId,
#pragma warning restore CS0618
                // Yeah, this is ugly. These should not be separate parameters to this method, but an object
                // parameter since they go together. I'll clean it up later.
                Message = messageId is not null && messageUrl is not null && triggeringMessageAuthor is not null
                    ? new SourceMessageInfo
                    {
                        MessageId = messageId,
                        Text = messageText,
                        ThreadId = threadId,
                        MessageUrl = messageUrl,
                        Author = triggeringMessageAuthor.ToPlatformUser(),
                    }
                    : null,
                SkillName = skill.Name,
                SkillUrl = skillUrl,
                Room = platformRoom,
                Customer = customer,
                Arguments = arguments.Value,
                CommandText = messageText,
                TokenizedArguments = arguments.Cast<Argument>()
                    .ToList(),
                Pattern = pattern is null
                    ? null
                    : new PatternMessage(pattern),
                From = from,
                Bot = new PlatformUser(bot.UserId,
                    bot.DisplayName,
                    bot.DisplayName),
                PlatformId = organization.PlatformId,
                Mentions = mentionedUsers,
                Request = httpTriggerEvent,
                IsChat = isChat,
                IsRequest = httpTriggerEvent is not null,
                IsSignal = signal is not null,
                IsInteraction = isInteraction,
                IsPlaybook = isPlaybook,
            },
            ConversationInfo = conversation,
            SignalInfo = signal,
            RunnerInfo = new SkillRunnerInfo
            {
                UserId = sender.User.Id,
                MemberId = sender.Id,
                SkillId = skill.Id,
                RoomId = room?.Id,
                ConversationId = conversation?.Id,
                Scope = skill.Scope,
                Code = code,
                Language = skill.Language,
                Timestamp = timestamp,
                AuditIdentifier = auditId,
#pragma warning disable CS0618
                // We need to use this here for backwards compatibility
                ConversationReference = new ConversationReference
                {
                    Conversation = new ConversationAccount
                    {
                        Id = new SlackConversationId(platformRoom.Id, threadId).ToString()
                    }
                }
#pragma warning restore CS0618
            },
            PassiveReplies = passiveReplies,
        };
    }

    async Task<SkillRunResponse> Send(Skill skill, Member sender, SkillMessage message)
    {
        if (!skill.Organization.UserSkillsEnabled)
        {
            var developerSettingsLink = new Hyperlink(_urlGenerator.AdvancedSettingsPage(), "Advanced Settings Page");
            return new SkillRunResponse
            {
                Success = false,
                Errors = new[]
                {
                    new RuntimeError
                    {
                        Description = "Running custom skills is disabled for this organization."
                    }
                },
                Replies = new List<string>
                {
                    $"Running custom skills is disabled for this organization. An administrator can enable it in the {developerSettingsLink}."
                },
            };
        }

        if (!skill.Enabled)
        {
            var errorMessage = $"Failed to run the `{skill.Name}` skill because it is disabled.";
            return new SkillRunResponse
            {
                Success = false,
                Errors = new[]
                {
                    new RuntimeError
                    {
                        Description = errorMessage
                    },
                },
                Replies = new List<string>
                {
                    errorMessage
                },
            };
        }

        // Get the endpoint configuration
        var endpoint = await _runnerEndpointManager.GetEndpointAsync(skill.Organization, skill.Language);

        var timestamp = message.RunnerInfo.Timestamp;
        var skillApiToken = _apiTokenFactory.CreateSkillApiToken(
            skill,
            sender,
            sender.User,
            timestamp);

        async Task<SkillRunResponse> MakeRequest()
        {
            using var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = endpoint.Url,
            };
            request.Headers.Accept.Add(new("application/json"));
            request.Headers.Add(CommonConstants.SkillApiTokenHeaderName, skillApiToken);
            if (endpoint.ApiToken is not null)
            {
                // Set both X-Functions-Key (for Azure Functions, including our legacy custom runner image)
                // and Authorization (for the new Functions-less runner image).
                request.Headers.Add("X-Functions-Key", endpoint.ApiToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiToken);
            }

            request.AddJsonContent(message);
            Log.MakingHttpRequestToSkillRunner(request.RequestUri, skill.Id, skill.Name);
            var response = await _httpClient.SendAsync(request, CancellationToken.None);
            return await HandleResponseAsync(response, endpoint.Url);
        }

        try
        {
            return await _retryPolicy.ExecuteAsync(MakeRequest);
        }
        catch (Exception e)
        {
            var error = endpoint.IsHosted
                ? $"Internal error calling skill runner endpoint. Contact 'support@ab.bot' for help and give them the request ID: {Activity.Current?.Id}"
                : "An error occurred calling your custom skill runner";

            throw new SkillRunException(error,
                skill,
                message.SkillInfo.PlatformId,
                endpoint.Url,
                e,
                !endpoint.IsHosted);
        }
    }

    static async Task<SkillRunResponse> HandleResponseAsync(HttpResponseMessage response, Uri endpoint)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var abbotMediaType = AbbotMediaTypes.ApplicationJsonV1.MediaType
                             ?? throw new InvalidOperationException(
                                 "AbbotMediaTypes.ApplicationJsonV1.MediaType is null!");

        if (abbotMediaType.Equals(mediaType, StringComparison.Ordinal))
        {
            var runnerResponse = await response.Content.ReadAsAsync<SkillRunResponse>(MediaTypeFormatters);
            return runnerResponse;
        }

        if ("text/html".Equals(mediaType, StringComparison.Ordinal))
        {
            var html = await response.Content.ReadAsStringAsync();
            if ("The service is unavailable.".Equals(html, StringComparison.OrdinalIgnoreCase))
            {
                // This often happens during deployment. We need the caller to retry.
                throw new ServerUnavailableException($"The skill runner service `{endpoint}` is temporarily down.");
            }
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Unexpected response from skill runner endpoint: {content}");
    }

    // When calling a skill from a trigger, we want to provide room information. Here we do our best to
    // retrieve it. The Trigger RoomId could be null if the trigger was created before we started storing
    // the room id. Moving forward, this should never happen.
    static PlatformRoom GetRoom(SkillTrigger trigger)
    {
        var roomId = trigger.RoomId;
        var roomName = trigger.Name;
        return new PlatformRoom(roomId, roomName);
    }

    // Gets the Member on whose behalf the skill will be called.
    static Member GetTriggerCreator(ITrackedEntity trigger, IOrganizationEntity skill)
    {
        // HACK: We need to pass a user and we don't have one loaded necessarily.
        return trigger.Creator.Members.SingleOrDefault(m => m.OrganizationId == skill.OrganizationId)
               ?? throw new InvalidOperationException($"Trigger creator {trigger.Creator.Id} is not a " +
                                                      $"member of the organization {skill.OrganizationId}!");
    }

    /// <summary>
    /// Adds support for our custom Content-Type: application/vnd.abbot.v1+json
    /// </summary>
    class AbbotJsonMediaTypeFormatter : JsonMediaTypeFormatter
    {
        public AbbotJsonMediaTypeFormatter()
        {
            SupportedMediaTypes.Add(AbbotMediaTypes.ApplicationJsonV1);
        }
    }
}

static partial class SkillRunnerClientLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message =
            "Making HTTP call to skill runner (Endpoint: {EndPoint}, Skill (Id: {SkillId} Name: {SkillName}))")]
    public static partial void MakingHttpRequestToSkillRunner(
        this ILogger logger,
        Uri endpoint,
        int skillId,
        string skillName);
}
