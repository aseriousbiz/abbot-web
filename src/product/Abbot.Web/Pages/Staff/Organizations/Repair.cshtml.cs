using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class RepairPage : OrganizationDetailPage
{
    // Example: C012ZJGPYTF
    static readonly Regex ChannelIdValidator = new(@"^C[0-9A-Z]+$");

    // Example: 1657563178.893239, but we allow the leading 'p' and missing '.' to make it easier to paste from the clipboard.
    static readonly Regex MessageIdValidator = new(@"^p?(?<ts>[0-9]{10})\.?(?<id>[0-9]{6})$");

    public static readonly DomId ImportButtonId = new("import-button");
    public static readonly DomId PreviewResultsId = new("preview-results");

    readonly ISlackResolver _slackResolver;
    readonly IConversationTracker _conversationTracker;
    readonly IConversationThreadResolver _conversationThreadResolver;
    readonly IClock _clock;
    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly ISettingsManager _settingsManager;

    public RepairPage(AbbotContext db,
        ISlackResolver slackResolver,
        IConversationTracker conversationTracker,
        IConversationThreadResolver conversationThreadResolver,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        ISettingsManager settingsManager,
        IAuditLog auditLog,
        IClock clock) : base(db, auditLog)
    {
        _slackResolver = slackResolver;
        _conversationTracker = conversationTracker;
        _conversationThreadResolver = conversationThreadResolver;
        _clock = clock;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _settingsManager = settingsManager;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        await InitializeDataAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostPreviewImportConversationAsync(
        string id,
        string? messageUrl,
        string? channelId,
        string? messageId)
    {
        var model = await GeneratePreviewAsync(id, messageUrl, channelId, messageId);

        var buttonContent = model.ErrorMessage is null
            ? $"""<button id="{ImportButtonId}" data-confirm="Are you sure you want to import this conversation? This will be visible to the customer!" class="btn btn-danger">Import</button>"""
            : $"""<button id="{ImportButtonId}" disabled data-tooltip="Can't import conversation due to an error" class="btn btn-danger">Import</button>""";

        return TurboStream(
            TurboUpdate(
                PreviewResultsId,
                Partial("_ImportResponse", model)),
            TurboReplace(ImportButtonId, buttonContent));
    }

    public async Task<IActionResult> OnPostImportConversationAsync(
        string id,
        string? messageUrl,
        string? channelId,
        string? messageId)
    {
        async Task<PartialViewResult> DoImportAsync()
        {
            var model = await GeneratePreviewAsync(id, messageUrl, channelId, messageId);
            if (model.ErrorMessage is { Length: > 0 })
            {
                return Partial("_ImportResponse", model);
            }

            if (model.Messages is not { Count: > 0 })
            {
                return Partial("_ImportResponse", ImportResponseModel.Error("No messages found."));
            }

            if (model.MessageDeleted)
            {
                return Partial("_ImportResponse", ImportResponseModel.Error("The root message has been deleted."));
            }

            var abbot = await _userRepository.EnsureAbbotMemberAsync(Organization);

            // Now we create the conversation
            try
            {
                // Perform the import as Abbot. We'll also write an audit log entry.
                var convo = await _conversationTracker.CreateConversationAsync(model.Messages, abbot, _clock.UtcNow);
                if (convo is null)
                {
                    return Partial("_ImportResponse", ImportResponseModel.Error("Failed to create conversation"));
                }

                model.CreatedConversation = convo;
                return Partial("_ImportResponse", model);
            }
            catch (Exception ex)
            {
                var errorModel = ImportResponseModel.Error(
                    $"Error importing conversation: {ex.Message} - Activity ID: {System.Diagnostics.Activity.Current?.Id}");

                return Partial("_ImportResponse", errorModel);
            }
        }

        var partial = await DoImportAsync();
        var buttonContent = $"""<button id="{ImportButtonId}" disabled data-tooltip="This conversation has been imported" class="btn btn-danger">Import</button>""";
        return TurboStream(
            TurboUpdate(PreviewResultsId, partial),
            TurboReplace(ImportButtonId, buttonContent));
    }

    async Task<ImportResponseModel> GeneratePreviewAsync(
        string orgId,
        string? messageUrl,
        string? channelId,
        string? messageId)
    {
        await InitializeDataAsync(orgId);

        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return ImportResponseModel.Error($"Organization plan {Organization.PlanType} does not allow conversation tracking");
        }

        if (messageUrl is { Length: > 0 })
        {
            // Parse values out of the URL.
            if (SlackUrl.TryParse(messageUrl, out var slackUrl) && slackUrl is SlackMessageUrl slackMessageUrl)
            {
                channelId = slackMessageUrl.ConversationId;
                messageId = slackMessageUrl.ThreadTimestamp ?? slackMessageUrl.Timestamp;
            }
            else
            {
                return ImportResponseModel.Error("Invalid message URL");
            }
        }

        if (channelId is not { Length: > 0 })
        {
            return ImportResponseModel.Error("Channel ID is required");
        }

        if (!ChannelIdValidator.IsMatch(channelId))
        {
            return ImportResponseModel.Error($"{channelId} is not a valid Channel ID");
        }

        var room = await _slackResolver.ResolveRoomAsync(channelId, Organization, forceRefresh: false);
        if (room is null)
        {
            return ImportResponseModel.Error($"No room found with Channel ID '{channelId}'.");
        }

        var lastVerifiedMessageId = await _settingsManager.GetLastVerifiedMessageIdAsync(room);

        if (room.BotIsMember != true)
        {
            return ImportResponseModel.Error("Abbot is not a member of that room", lastVerifiedMessageId);
        }

        if (!room.ManagedConversationsEnabled)
        {
            return ImportResponseModel.Error("Channel does not have managed conversations enabled.", lastVerifiedMessageId);
        }

        if (messageId is not { Length: > 0 })
        {
            return ImportResponseModel.Error("Message ID is required", lastVerifiedMessageId);
        }

        if (MessageIdValidator.Match(messageId) is not { Success: true } match)
        {
            return ImportResponseModel.Error($"{messageId} is not a valid Message ID", lastVerifiedMessageId);
        }

        messageId = match.Groups["ts"] + "." + match.Groups["id"];

        try
        {
            Conversation? existingConversation = null;
            var messages = await _conversationThreadResolver.ResolveConversationMessagesAsync(room, messageId);
#pragma warning disable CA1826
            if (messages.FirstOrDefault() is { } firstMessage)
#pragma warning restore CA1826
            {
                var threadId = firstMessage.ThreadId ?? firstMessage.MessageId;
                existingConversation = await _conversationRepository.GetConversationByThreadIdAsync(threadId, room);
            }

            return ImportResponseModel.Preview(messages, lastVerifiedMessageId, existingConversation, Organization);
        }
        catch (Exception ex)
        {
            return ImportResponseModel.Error(
                $"Error importing conversation: {ex.Message} - Activity ID: {System.Diagnostics.Activity.Current?.Id}");
        }
    }

    protected override Task InitializeDataAsync(Organization organization)
    {
        return Task.CompletedTask;
    }

    public class ImportResponseModel
    {
        public string? ErrorMessage { get; set; }

        public Organization? SubjectOrganization { get; set; }

        public IReadOnlyList<ConversationMessage>? Messages { get; set; }

        public Conversation? ExistingConversation { get; set; }

        public Conversation? CreatedConversation { get; set; }

        public bool MessageDeleted { get; init; }

        public string? LastVerifiedMessageId { get; init; }

        public static ImportResponseModel Error(string error, string? lastVerifiedMessageId = null)
        {
            return new ImportResponseModel
            {
                ErrorMessage = error,
                LastVerifiedMessageId = lastVerifiedMessageId
            };
        }

        public static ImportResponseModel Preview(
            IReadOnlyList<ConversationMessage> messages,
            string? lastVerifiedMessageId,
            Conversation? existingConversation,
            Organization subjectOrganization)
        {
            return new ImportResponseModel
            {
                Messages = messages,
                LastVerifiedMessageId = lastVerifiedMessageId,
                ExistingConversation = existingConversation,
                SubjectOrganization = subjectOrganization,
#pragma warning disable CA1826
                MessageDeleted = messages.FirstOrDefault()?.Deleted ?? false
#pragma warning restore CA1826
            };
        }
    }
}
