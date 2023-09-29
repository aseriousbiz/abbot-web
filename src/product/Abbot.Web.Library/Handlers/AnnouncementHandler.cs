using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using NodaTime;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;
using Serious.Filters;
using Serious.Logging;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

public class AnnouncementHandler : IHandler
{
    static readonly ILogger<AnnouncementHandler> Log = ApplicationLoggerFactory.CreateLogger<AnnouncementHandler>();

    readonly IAnnouncementsRepository _repository;
    readonly IAnnouncementScheduler _announcementScheduler;
    readonly AnnouncementDispatcher _announcementDispatcher;
    readonly ISlackResolver _resolver;
    readonly IReactionsApiClient _reactionsApiClient;
    readonly IRoomRepository _roomRepository;
    readonly CustomerRepository _customerRepository;
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;

    public static class Blocks
    {
        public const string SendAsBot = "send-as-bot-input";
        public const string ChannelOptionsInput = "channel-radio-input";
        public const string Channels = "channels-input";
        public const string Segments = "segments-input";
        public const string WhenInput = "when-radio-input";
        public const string DatePicker = "date-picker-input";
        public const string TimePicker = "time-picker-input";
        public const string ActionsBlock = "actions-block";
    }

    public static class ActionIds
    {
        public const string SendAsBot = "send-as-bot";
        public const string ChannelOptions = "channel-radio";
        public const string Channels = "channels";
        public const string Segments = "segments";
        public const string WhenOptions = "when-radio";
        public const string DatePicker = "date-picker";
        public const string TimePicker = "time-picker";
    }

    static class OptionValues
    {
        // Channel Options Input
        public const string PickChannels = nameof(SelectedRoomsAnnouncementTarget);

        // Customer Segment Options Input
        public const string PickSegments = nameof(CustomerSegmentsAnnouncementTarget);

        public const string AllExternalChannels = nameof(AllRoomsAnnouncementTarget);

        // When Input
        public const string Later = "later";
    }

    static bool HasSendLaterSelected(BlockActionsState? state) =>
        state?.TryGetAs<RadioButtonGroup>(Blocks.WhenInput, ActionIds.WhenOptions, out var rbg) == true
            && rbg.SelectedOption is { Value: OptionValues.Later };

    static bool? HasPickChannelsSelected(IAnnouncementTarget? target) => target is SelectedRoomsAnnouncementTarget;
    static bool? HasPickSegmentsSelected(IAnnouncementTarget? target) => target is CustomerSegmentsAnnouncementTarget;

    record ViewState(
        Announcement? ExistingAnnouncement,
        bool BotIsMember,
        bool PickChannels,
        bool PickSegments,
        IReadOnlyList<CustomerTag> CustomerSegments,
        bool ScheduleLater,
        string BotName,
        string PrivateMetadata,
        Member Member);

    public AnnouncementHandler(
        IAnnouncementsRepository repository,
        IAnnouncementScheduler announcementScheduler,
        AnnouncementDispatcher announcementDispatcher,
        ISlackResolver resolver,
        IReactionsApiClient reactionsApiClient,
        IRoomRepository roomRepository,
        CustomerRepository customerRepository,
        IAnalyticsClient analyticsClient,
        IClock clock)
    {
        _repository = repository;
        _announcementScheduler = announcementScheduler;
        _announcementDispatcher = announcementDispatcher;
        _resolver = resolver;
        _reactionsApiClient = reactionsApiClient;
        _roomRepository = roomRepository;
        _customerRepository = customerRepository;
        _analyticsClient = analyticsClient;
        _clock = clock;
    }

    /// <summary>
    /// Handles the create announcement button click from a DM with Abbot
    /// </summary>
    /// <param name="platformMessage"></param>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        if (!platformMessage.From.IsAgent())
        {
            return;
        }

        if (platformMessage.TriggerId is not { Length: > 0 } triggerId)
        {
            throw new UnreachableException($"Trigger Id was not provided. {platformMessage.MessageId}.");
        }

        if (platformMessage.Payload.InteractionInfo is { Arguments: "update-sent-messages", ActionElement.ActionId: { Length: > 0 } actionId })
        {
            await UpdateSentAnnouncementMessagesAsync(platformMessage, actionId);
        }
        else
        {
            await ShowAnnouncementModalAsync(platformMessage, triggerId);
        }
    }

    async Task ShowAnnouncementModalAsync(IPlatformMessage platformMessage, string triggerId)
    {
        var privateMetadataText = AppendResponseUrlToPrivateMetadata(platformMessage.Payload.InteractionInfo);

        var view = await GetAnnouncementModalAsync(
            privateMetadataText,
            platformMessage.From,
            platformMessage.Organization);
        _analyticsClient.Screen(
            "Announcement Modal",
            AnalyticsFeature.Announcements,
            platformMessage.From,
            platformMessage.Organization);
        await platformMessage.Responder.OpenModalAsync(triggerId, view);
    }

    async Task UpdateSentAnnouncementMessagesAsync(IPlatformEvent platformEvent, string actionId)
    {
        if (int.TryParse(actionId, out var announcementId)
            && await _repository.GetByIdAsync(announcementId, platformEvent.Organization) is { } announcement)
        {
            if (announcement.DateCompletedUtc is not null)
            {
                await _announcementScheduler.ScheduleAnnouncementUpdateAsync(announcement, platformEvent.From.User);
                await platformEvent.Responder.SendActivityAsync($"Announcement Updates are now being sent.");
            }
            else
            {
                await platformEvent.Responder.SendActivityAsync("Please wait till the announcement is done being sent.");
            }

            _analyticsClient.Track(
                "Announcement Messages Updated",
                AnalyticsFeature.Announcements,
                platformEvent.From,
                platformEvent.Organization,
                new()
                {
                    ["try_later"] = announcement.DateCompletedUtc is null
                });
        }
    }

    /// <summary>
    /// Handles the create announcement button click on the <see cref="ManageConversationHandler"/> which gets
    /// routed here.
    /// </summary>
    /// <param name="viewContext">Information about the view that was interacted with.</param>
    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        if (!viewContext.FromMember.IsAgent())
        {
            return;
        }

        var selectedTargetName = GetSelectedTargetName(viewContext.Payload.View.State);
        var announcementTarget = _announcementDispatcher.GetAnnouncementTarget(selectedTargetName);


        var pickChannels = HasPickChannelsSelected(announcementTarget);
        var pickSegments = HasPickSegmentsSelected(announcementTarget);
        var scheduleLater = HasSendLaterSelected(viewContext.Payload.View.State);

        foreach (var action in viewContext.Payload.Actions)
        {
            switch (action)
            {
                case RadioButtonGroup { ActionId: ActionIds.ChannelOptions } channelOptions:
                    pickChannels = channelOptions is { SelectedOption.Value: OptionValues.PickChannels };
                    break;

                case RadioButtonGroup { ActionId: ActionIds.WhenOptions } whenOptions:
                    scheduleLater = whenOptions is { SelectedOption.Value: OptionValues.Later };
                    break;
            }
        }

        var privateMetadataText = viewContext.Payload.View.PrivateMetadata;

        var view = await GetAnnouncementModalAsync(
            privateMetadataText,
            viewContext.FromMember,
            viewContext.Organization,
            pickChannelsChecked: pickChannels,
            pickSegmentsChecked: pickSegments,
            scheduleLaterChecked: scheduleLater);
        await viewContext.UpdateModalViewAsync(view);
    }

    async Task<ModalView> GetAnnouncementModalAsync(
        string? privateMetadataText,
        Member member,
        Organization organization,
        bool? pickChannelsChecked = null,
        bool? pickSegmentsChecked = null,
        bool? scheduleLaterChecked = null)
    {
        Log.PrivateMetadata(privateMetadataText);
        var privateMetadata = PrivateMetadata.Parse(privateMetadataText).Require();

        var announcement = await _repository.GetAnnouncementFromMessageAsync(
            privateMetadata.Channel,
            privateMetadata.MessageId,
            organization);

        var sourceRoom = announcement?.SourceRoom ?? await _resolver.ResolveRoomAsync(
            privateMetadata.Channel,
            organization,
            forceRefresh: true).Require();

        var pickChannels = pickChannelsChecked
            ?? announcement?.Messages.Any();

        var allCustomerSegments = await _customerRepository.GetAllCustomerSegmentsAsync(organization, 1, int.MaxValue);
        var pickSegments = pickSegmentsChecked
            ?? announcement?.CustomerSegments.Any(); // No customer tags, no pick tags

        var scheduledLater = scheduleLaterChecked is true
            || announcement?.ScheduledDateUtc is not null;

        Log.SourceRoom(sourceRoom.Id, sourceRoom.BotIsMember.GetValueOrDefault());

        var viewState = new ViewState(
            announcement,
            BotIsMember: sourceRoom is { BotIsMember: true },
            PickChannels: pickChannels is true,
            PickSegments: pickSegments is true,
            CustomerSegments: allCustomerSegments,
            ScheduleLater: scheduledLater,
            organization.BotName ?? "Abbot",
            privateMetadata,
            member);

        return Render(viewState);
    }

    ModalView Render(ViewState viewState)
    {
        // Handle the situation where we cannot create or edit an announcement
        if (viewState.ExistingAnnouncement is { } existingAnnouncement
            && IsTooLateToEditAnnouncement(existingAnnouncement, out var errorModal))
        {
            return errorModal;
        }

        var blocks = new List<ILayoutBlock>();

        if (viewState.BotIsMember)
        {
            var sendAsBotOption = new CheckOption($"Send as @{viewState.BotName}", "true",
                "Announcements are sent from your user by default.");
            blocks.Add(
                new Actions(Blocks.SendAsBot,
                    new CheckboxGroup(sendAsBotOption)
                    {
                        ActionId = ActionIds.SendAsBot,
                        InitialOptions =
                            viewState.ExistingAnnouncement?.SendAsBot == true
                                ? new[] { sendAsBotOption }
                                : Array.Empty<CheckOption>(),
                    })
                );

            var channelOptions = new List<CheckOption>()
            {
                new("All Shared Channels",
                    OptionValues.AllExternalChannels,
                    "All tracked shared channels (at time of announcement) will receive announcement."),
                new("Pick Specific Channels",
                    OptionValues.PickChannels,
                    "Selected tracked channels will receive announcement."),
            };

            if (viewState.CustomerSegments.Any() || viewState.ExistingAnnouncement?.CustomerSegments.Any() is true)
            {
                channelOptions.Add(new CheckOption("Customer Segment",
                    OptionValues.PickSegments,
                    "Customers in the selected segments will receive announcement."));
            }

            var initialOption = viewState.ExistingAnnouncement switch
            {
                { CustomerSegments.Count: > 0 } => channelOptions[2],
                { Messages.Count: 0 } => channelOptions[0],
                { Messages.Count: > 0 } => channelOptions[1],
                _ => null,
            };
            blocks.Add(
                new Input("Where should this announcement be broadcast?",
                    new RadioButtonGroup(channelOptions)
                    {
                        ActionId = ActionIds.ChannelOptions,
                        InitialOption = initialOption,
                    },
                    Blocks.ChannelOptionsInput,
                    dispatchAction: true));

            if (viewState.PickChannels)
            {
                blocks.Add(
                    new Input("Select tracked channels",
                        RoomsMultiSelectMenu(viewState))
                    {
                        BlockId = Blocks.Channels,
                    });
            }

            if (viewState.PickSegments)
            {
                blocks.Add(
                    new Input("Select customer segments",
                        CustomerSegmentsMultiSelectMenu(viewState))
                    {
                        BlockId = Blocks.Segments,
                    });
            }

            if (viewState.ExistingAnnouncement is null)
            {
                blocks.Add(new Input("When should this be sent?",
                    new RadioButtonGroup(
                        new CheckOption("Send Immediately", "immediately", "Announcement will be posted immediately."),
                        new CheckOption("Schedule for Later", OptionValues.Later, "Send announcement at a later time."))
                    {
                        ActionId = ActionIds.WhenOptions
                    },
                    Blocks.WhenInput,
                    dispatchAction: true));
            }
            else
            {
                blocks.Add(new Section("When should this be sent?"));
            }

            if (viewState.ScheduleLater)
            {
                var datePicker = new DatePicker(ActionIds.DatePicker);
                var timePicker = new TimePicker(ActionIds.TimePicker);

                if (viewState.ExistingAnnouncement is { ScheduledDateUtc: { } scheduledDateUtc })
                {
                    // Convert date to user's timezone.
                    var localScheduledDate = viewState.Member.ToTimeZone(scheduledDateUtc);
                    datePicker = datePicker with
                    {
                        InitialDate = localScheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    };
                    timePicker = timePicker with
                    {
                        InitialTime = localScheduledDate.ToString("hh:mm", CultureInfo.InvariantCulture)
                    };
                }

                // Update the view with a Date and time picker.
                blocks.Add(new Input("Choose a date to broadcast announcement", datePicker)
                {
                    BlockId = Blocks.DatePicker
                });
                blocks.Add(new Input("Choose a time to broadcast announcement", timePicker)
                {
                    BlockId = Blocks.TimePicker
                });
            }
        }
        else
        {
            blocks.Add(new Section($"In order to create an announcement from this message, {viewState.BotName} must be a member of this channel."));
        }

        return new ModalView
        {
            Title = viewState.ExistingAnnouncement is null ? "Create Announcement" : "Edit Announcement",
            Close = "Back",
            Submit = !viewState.BotIsMember
                ? "Close"
                : viewState.ScheduleLater
                    ? "Schedule"
                    : "Send",
            Blocks = blocks,
            CallbackId = InteractionCallbackInfo.For<AnnouncementHandler>(),
            PrivateMetadata = viewState.PrivateMetadata, // Just passing it along for the ride.
        };
    }

    bool IsTooLateToEditAnnouncement(Announcement existingAnnouncement, [NotNullWhen(true)] out ModalView? errorModal)
    {
        errorModal = null;
        var message = existingAnnouncement switch
        {
            { ScheduledDateUtc: null } => "An announcement for this message was already created to be sent immediately",
            { DateCompletedUtc: not null } => "An announcement for this message was already sent.",
            { DateStartedUtc: not null } => "An announcement for this message was already created and is currently sending.",
            { ScheduledDateUtc: { } scheduledDateUtc } when scheduledDateUtc < _clock.UtcNow.AddMinutes(5) => "It’s too late to edit this announcement. The announcement is scheduled to be sent within 5 minutes.",
            _ => null
        };

        if (message is not null)
        {
            errorModal = new ModalView
            {
                Title = "Cannot Edit Announcement",
                Close = "Close",
                Blocks = new List<ILayoutBlock> { new Section(message) },
            };

            return true;
        }

        return false;
    }

    static SelectMenu RoomsMultiSelectMenu(ViewState viewState)
    {
        return new MultiExternalSelectMenu
        {
            ActionId = ActionIds.Channels,
            MinQueryLength = 1,
            Placeholder = "Select tracked channels",
            InitialOptions = viewState.ExistingAnnouncement?.Messages
                                 .Select(ToOption)
                                 .ToArray()
                             ?? Array.Empty<Option>(),
        };
    }

    static SelectMenu CustomerSegmentsMultiSelectMenu(ViewState viewState)
    {
        return new MultiStaticSelectMenu
        {
            ActionId = ActionIds.Segments,
            Options = viewState.CustomerSegments.Select(ToOption).ToArray(),
            Placeholder = "Select customer segments",
            InitialOptions = viewState
                .ExistingAnnouncement?
                .CustomerSegments
                .Select(s => s.CustomerTag)
                .Select(ToOption)
                .ToArray() ?? Array.Empty<Option>(),
        };
    }

    static Option ToOption(AnnouncementMessage message) => ToOption(message.Room);

    static Option ToOption(Room room) => new(room.Name ?? room.PlatformRoomId, room.PlatformRoomId);

    static Option ToOption(CustomerTag segment) => new(segment.Name, $"{segment.Id}");

    public async Task<BlockSuggestionsResponse> OnBlockSuggestionRequestAsync(
        IPlatformEvent<BlockSuggestionPayload> platformEvent)
    {
        var query = platformEvent.Payload.Value.TrimStart('#');

        var suggestions = await _roomRepository.GetPersistentRoomsAsync(
            platformEvent.Organization,
            filter: new FilterList { Filters.Filter.Create("room", query) }, // Filter on the room name.
            TrackStateFilter.Tracked,
            page: 1,
            pageSize: int.MaxValue);

        return new OptionsBlockSuggestionsResponse(suggestions.Select(ToOption));
    }

    /// <summary>
    /// Handles submission of the Announcement handler dialog.
    /// </summary>
    /// <param name="viewContext">Information about the view that was interacted with.</param>
    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        if (!viewContext.FromMember.IsAgent())
        {
            return;
        }

        var modal = (viewContext.Payload.View as ModalView).Require();
        if (modal.Submit?.Text is "Close")
        {
            // The user clicked the Close button on a warning dialog. Close all modals.
            viewContext.RespondByClosingAllViews();
            return;
        }

        var state = modal.State.Require();

        Log.PrivateMetadata(modal.PrivateMetadata);
        var privateMetadata = PrivateMetadata.Parse(modal.PrivateMetadata).Require();

        var existingAnnouncement = await _repository.GetAnnouncementFromMessageAsync(
            privateMetadata.Channel,
            privateMetadata.MessageId,
            viewContext.Organization);

        if (existingAnnouncement is not null && IsTooLateToEditAnnouncement(existingAnnouncement, out var errorModal))
        {
            // TODO: This is a bit ugly, but bear with me. - @haacked
            var errorMessage = ((Section)errorModal.Blocks.First()).Text?.Text;

            viewContext.ReportValidationErrors(Blocks.ChannelOptionsInput, errorMessage ?? "It is too late.");
            return;
        }

        bool scheduleLater = existingAnnouncement is not null || HasSendLaterSelected(state);

        // On a submission, we need to look at the State to understand the state of the modal.
        if (!TryGetScheduledDateTimeUtc(viewContext, state, scheduleLater, out var scheduledDateTimeUtc))
        {
            return;
        }

        // Even though we validated the source room in the dialog, we need to do it again in the unlikely case Abbot was
        // removed from the channel in between showing the dialog and the user clicking submit. Yes, it's unlikely which
        // is why we throw an exception rather than worry about it. @haacked
        var sourceRoom = await _resolver.ResolveRoomAsync(privateMetadata.Channel, viewContext.Organization, true);
        if (sourceRoom is not { BotIsMember: true })
        {
            viewContext.ReportValidationErrors(
                Blocks.ChannelOptionsInput,
                "In order to create an announcement from this message, the bot must be a member of the channel.");
            return;
        }

        var announcement = existingAnnouncement ?? new Announcement
        {
            SourceMessageId = privateMetadata.MessageId,
            SourceRoom = sourceRoom,
            Organization = viewContext.Organization,
            Messages = new List<AnnouncementMessage>(),
            CustomerSegments = new List<AnnouncementCustomerSegment>(),
        };

        var selectedTargetName = GetSelectedTargetName(state);
        var announcementTarget = _announcementDispatcher.GetAnnouncementTarget(selectedTargetName).Require();

        if (!await announcementTarget.HandleTargetSelectedAsync(viewContext, state, announcement))
        {
            return;
        }

        announcement.SendAsBot = state.GetAs<CheckboxGroup>(Blocks.SendAsBot, ActionIds.SendAsBot)
                .SelectedOptions
                .Any();
        announcement.ScheduledDateUtc = scheduledDateTimeUtc;

        if (existingAnnouncement is null)
        {
            await _repository.CreateAsync(announcement, viewContext.From);
        }
        else
        {
            await _repository.UpdateAsync(announcement, viewContext.From);
        }

        await _announcementScheduler.ScheduleAnnouncementBroadcastAsync(
            announcement,
            viewContext.From,
            scheduleReminder: scheduledDateTimeUtc is not null);
        _analyticsClient.Track(
            "Announcement Scheduled",
            AnalyticsFeature.Announcements,
            viewContext.FromMember,
            viewContext.Organization,
            new()
            {
                ["reminder"] = scheduledDateTimeUtc is not null
            });

        if (viewContext.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            // Add the announcement emoji to the source message.
            await _reactionsApiClient.AddReactionAsync(apiToken,
                "mega",
                sourceRoom.PlatformRoomId,
                privateMetadata.MessageId);
        }

        if (scheduledDateTimeUtc.HasValue)
        {
            await SendScheduledSuccessfulMessageAsync(
                viewContext,
                scheduledDateTimeUtc.Value,
                announcement,
                existingAnnouncement is not null);
        }

        if (privateMetadata.ResponseUrl is { } responseUrl)
        {
            var message = MetaBot.CreateAbbotDmResponseMessage(
                    Enumerable.Empty<IActionElement>(),
                    "create an announcement");
            await viewContext.UpdateActivityAsync(message, responseUrl);
        }
    }

    // This sends a message to the user after they schedule an announcement for later with some helpful information.
    async Task SendScheduledSuccessfulMessageAsync(
        IViewContext<IViewSubmissionPayload> viewContext,
        DateTime scheduledDateTimeUtc,
        Announcement announcement,
        bool edited = false)
    {
        var localScheduledDate = viewContext.FromMember.ToTimeZone(scheduledDateTimeUtc);
        var announcementTarget = _announcementDispatcher.GetAnnouncementTarget(announcement);
        var targetLabel = announcementTarget.GetSuccessMessageLabel(announcement);

        var messageUrl = announcement.GetMessageUrl().Require();
        var originalMessageLink = new Hyperlink(messageUrl, "the original message");

        var rescheduleButton = CreateAnnouncementButton(
            "Reschedule",
            announcement.SourceRoom.PlatformRoomId,
            announcement.SourceMessageId);
        var action = edited ? "rescheduled" : "scheduled";

        var message = new RichActivity(
            $":mega: Alright. I’ve {action} this announcement to be sent *{localScheduledDate:MMMM d, yyyy} at {localScheduledDate:h:mm tt}*.",
            new Section(new MrkdwnText(
                $":mega: Alright. I’ve {action} this announcement to be posted on *{localScheduledDate:MMMM d, yyyy} at {localScheduledDate:h:mm tt}*"
                + $" {targetLabel}. I’ll remind you about this announcement *an hour before* it is sent.")),
            new Section(new MrkdwnText($":writing_hand: *Need to make a change before then?* Edit {originalMessageLink} with your changes.")),
            new Section(new MrkdwnText(":x: *Change your mind about this announcement?* "
                + "Delete the original message and it will no longer be scheduled for announcement.")),
            new Section(new MrkdwnText(":alarm_clock: *Need to reschedule or edit where the announcement will "
                + "be broadcasted?* Click the reschedule button below.")),
            new Actions(Blocks.ActionsBlock, rescheduleButton));

        await viewContext.SendDirectMessageAsync(message);
    }

    bool TryGetScheduledDateTimeUtc(
        IViewContext<IViewSubmissionPayload> viewContext, BlockActionsState state,
        bool scheduledLater,
        out DateTime? scheduledDateTimeUtc)
    {
        if (!scheduledLater)
        {
            // Send immediately!
            scheduledDateTimeUtc = null;
            return true;
        }

        scheduledDateTimeUtc = DateTime.MaxValue;
        var datePicker = state.GetAs<DatePicker>(Blocks.DatePicker, ActionIds.DatePicker);
        var timePicker = state.GetAs<TimePicker>(Blocks.TimePicker, ActionIds.TimePicker);

        var date = datePicker.SelectedDate;
        var time = timePicker.SelectedTime;

        if (date is null)
        {
            viewContext.ReportValidationErrors(Blocks.DatePicker, "Please specify a date in the future.");
        }

        if (time is null)
        {
            viewContext.ReportValidationErrors(Blocks.TimePicker, "Please specify a time in the future.");
        }

        if (date is null || time is null)
        {
            return false;
        }

        var timezoneId = viewContext.FromMember.TimeZoneId ?? "America/Los_Angeles";
        var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timezoneId) ?? DateTimeZone.Utc;
        scheduledDateTimeUtc = date.Value.ToUtcDateTime(time.Value, tz);

        if (_clock.UtcNow > scheduledDateTimeUtc)
        {
            viewContext.ReportValidationErrors(Blocks.DatePicker, "Please specify a date and time in the future.");
            return false;
        }
        return true;
    }

    public record PrivateMetadata(string Channel, string MessageId, Uri? ResponseUrl = null) : PrivateMetadataBase
    {
        static Uri? ToUri(string? responseUrl) =>
            responseUrl is null
                ? null
                : Uri.TryCreate(responseUrl, UriKind.Absolute, out var url)
                    ? url
                    : null;

        public static PrivateMetadata? Parse(string? privateMetadata)
        {
            return TrySplitParts(privateMetadata, 3, out var parts)
                ? new PrivateMetadata(parts[0], parts[1], ToUri(parts[2]))
                : null;
        }

        protected override IEnumerable<string> GetValues()
        {
            yield return Channel;
            yield return MessageId;
            yield return ResponseUrl?.ToString() ?? string.Empty;
        }

        public override string ToString() => base.ToString();
    }

    static ButtonElement CreateAnnouncementButton(string buttonText, string platformRoomId, string messageId)
    {
        return new ButtonElement(buttonText)
        {
            ActionId = InteractionCallbackInfo.For<AnnouncementHandler>(),
            Value = new PrivateMetadata(platformRoomId, messageId)
        };
    }

    static string? AppendResponseUrlToPrivateMetadata(MessageInteractionInfo? messageInteractionInfo)
    {
        if (messageInteractionInfo is not { Arguments: { Length: > 0 } privateMetadataText })
        {
            return null;
        }

        var responseUrl = messageInteractionInfo.ResponseUrl;
        if (responseUrl is null)
        {
            return privateMetadataText;
        }
        var privateMetadata = PrivateMetadata.Parse(privateMetadataText)
            ?? throw new InvalidOperationException($"Invalid Private Metadata {privateMetadataText}");
        privateMetadata = privateMetadata with
        {
            ResponseUrl = responseUrl
        };

        return privateMetadata.ToString();
    }

    static string? GetSelectedTargetName(BlockActionsState? state) =>
        state?.TryGetAs<RadioButtonGroup>(Blocks.ChannelOptionsInput, ActionIds.ChannelOptions, out var rbg) is true
            ? rbg.SelectedOption?.Value
            : null;

}

// A little overkill right now, but I have plans to clean up this code.

public static partial class AnnouncementHandlerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "PrivateMetadata: \"{PrivateMetadata}\"")]
    public static partial void PrivateMetadata(this ILogger<AnnouncementHandler> logger, string? privateMetadata);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Source Room: RoomId: {RoomId}, Bot.IsMember: {BotIsMember}")]
    public static partial void SourceRoom(this ILogger<AnnouncementHandler> logger, int roomId, bool botIsMember);


}
