using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using Humanizer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Security;
using Serious.Abbot.Signals;
using Serious.Abbot.Skills;
using Serious.BlockKit.LayoutBlocks;
using Serious.Logging;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Payloads;
using SlackOption = Serious.Slack.BlockKit.Option;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Handles room membership events. In particular, when Abbot joins a room, we kick off the onboarding flow.
/// </summary>
/// <remarks>
/// When Abbot is added to a room, we kick off the onboarding flow. This starts the following sequence:
/// <see cref="HandleAbbotAddedToRoom"/> which sends an ephemeral message with a button to enable conversations.
/// <see cref="OnMessageInteractionAsync"/> which enables conversation and opens a modal view to set first responders.
/// <see cref="OnSubmissionAsync"/> which handles the submission of the modal view and saves the changes.
/// </remarks>
public class RoomMembershipPayloadHandler : IPayloadHandler<RoomMembershipEventPayload>, IHandler
{
    static readonly ILogger<RoomMembershipPayloadHandler> Log = ApplicationLoggerFactory.CreateLogger<RoomMembershipPayloadHandler>();

    readonly ISlackResolver _slackResolver;
    readonly IRoomRepository _roomRepository;
    readonly IUserRepository _userRepository;
    readonly ISystemSignaler _systemSignaler;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly IUrlGenerator _urlGenerator;
    readonly IAnalyticsClient _analyticsClient;

    public static class ActionIds
    {
        public const string DisableConversationTracking = "DisableConversationTracking";
        public const string EnableConversationTracking = "EnableConversationTracking";
        public const string EditConversationTrackingButton = "EditConversationsButton";

        /// <summary>
        /// The action Id for the choice to use custom response times.
        /// </summary>
        public const string CustomResponseTimeSelected = nameof(CustomResponseTimeSelected);

        /// <summary>
        /// The action Id for the choice to use the organization's default response times.
        /// </summary>
        public const string DefaultResponseTimeSelected = nameof(DefaultResponseTimeSelected);
    }

    /// <summary>
    /// The bound state when the view is submitted.
    /// </summary>
    /// <param name="TargetResponseTime">The target response time.</param>
    /// <param name="DeadlineResponseTime">The deadline response time.</param>
    /// <param name="ResponseTimeOption">Either <see cref="ActionIds.CustomResponseTimeSelected"/> or <see cref="ActionIds.DefaultResponseTimeSelected"/> depending on what the user selected.</param>
    /// <param name="IsCommunityRoom">Whether this room is a community room or not.</param>
    /// <param name="FirstResponders">The set of selected first responders.</param>
    public record SubmissionState(
        string? TargetResponseTime,
        string? DeadlineResponseTime,
        string? ResponseTimeOption,
        string? IsCommunityRoom,
        IReadOnlyList<string> FirstResponders);

    /// <summary>
    /// Encapsulates the state we need to render in the Conversation Tracking dialog.
    /// </summary>
    /// <param name="Organization">The current organization.</param>
    /// <param name="Room">The room to enable conversation tracking for.</param>
    /// <param name="InitialFirstResponderOptions">The existing first responder options for this room.</param>
    /// <param name="AgentOptions">The agent options that the user can choose from for the first responders.</param>
    /// <param name="SelectedWarningOption">The currently selected warning response time option.</param>
    /// <param name="SelectedDeadlineOption">The currently selected deadline response time option.</param>
    /// <param name="DefaultFirstResponders">The default first responders for the organization, if any.</param>
    /// <param name="ShowResponseTimeOptions">
    /// If <c>true</c>, then the radio buttons to choose between default and custom response time options is visible.
    /// This is true if default response times are configured for the organization.
    /// </param>
    /// <param name="CustomOptionSelected">
    /// If <c>true</c>, the custom response time options is selected. Otherwise the defaults are selected.
    /// </param>
    /// <param name="PrivateMetadata">Metadata we need to pass along with each state of the dialog.</param>
    record ViewState(
        Organization Organization,
        Room Room,
        IReadOnlyList<SlackOption> InitialFirstResponderOptions,
        IReadOnlyList<SlackOption> AgentOptions,
        SlackOption? SelectedWarningOption,
        SlackOption? SelectedDeadlineOption,
        IReadOnlyList<string> DefaultFirstResponders,
        bool ShowResponseTimeOptions,
        bool CustomOptionSelected,
        PrivateMetadata PrivateMetadata);

    public RoomMembershipPayloadHandler(
         ISlackResolver slackResolver,
         IRoomRepository roomRepository,
         IUserRepository userRepository,
         ISystemSignaler systemSignaler,
         PlaybookDispatcher playbookDispatcher,
         IUrlGenerator urlGenerator,
         IAnalyticsClient analyticsClient)
    {
        _slackResolver = slackResolver;
        _roomRepository = roomRepository;
        _userRepository = userRepository;
        _systemSignaler = systemSignaler;
        _playbookDispatcher = playbookDispatcher;
        _urlGenerator = urlGenerator;
        _analyticsClient = analyticsClient;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<RoomMembershipEventPayload> platformEvent)
    {
        Log.MethodEntered(typeof(RoomMembershipPayloadHandler), nameof(OnPlatformEventAsync), "Room Membership Event");
        var roomMembershipEventPayload = platformEvent.Payload;

        var organization = platformEvent.Organization;
        var orgSettings = RoomSettings.Merge(RoomSettings.Default, organization.DefaultRoomSettings);

        var room = await _slackResolver.ResolveRoomAsync(
            roomMembershipEventPayload.PlatformRoomId,
            organization,
            false);

        if (room is null)
        {
            return;
        }

        bool userIsBeingAdded = roomMembershipEventPayload.Type is MembershipChangeType.Added;

        // Are we changing Abbot's membership in the room?
        if (platformEvent.From.IsAbbot())
        {
            // Ignore membership change if not installed
            if (organization.PlatformBotUserId != platformEvent.From.User.PlatformUserId)
                return;

            var planSupportsConversationTracking = platformEvent
                .Organization
                .HasPlanFeature(PlanFeature.ConversationTracking);

            var actorUserId = platformEvent.Payload.InviterPlatformUserId;
            var inviter = actorUserId is not null
                ? await _slackResolver.ResolveMemberAsync(actorUserId, platformEvent.Organization)
                : null;

            if (inviter is not null)
            {
                _analyticsClient.Track(
                    $"Abbot {(userIsBeingAdded ? "invited to" : "removed from")} room",
                    AnalyticsFeature.Slack,
                    inviter,
                    platformEvent.Organization,
                    new()
                    {
                        { "plan_supports_conversation_tracking", planSupportsConversationTracking },
                        { "inviter_can_manage", inviter.CanManageConversations() },
                        { "room", $"{room.Id}" },
                        { "room_is_shared", room.Shared?.ToString() ?? "null" },
                    });
            }

            await _roomRepository.SetConversationManagementEnabledAsync(
                room,
                enabled: userIsBeingAdded,
                platformEvent.From);

            // Update the room's "BotIsMember" status
            room.BotIsMember = userIsBeingAdded;

            // If we just got invited to the room, it can't be deleted!
            room.Deleted &= !userIsBeingAdded;

            await _roomRepository.UpdateAsync(room);
            await HandleAbbotAddedToRoom(platformEvent, room, inviter);
            return;
        }

        if (userIsBeingAdded && room is { Persistent: true, ManagedConversationsEnabled: true })
        {
            if (ConversationTracker.IsSupportee(platformEvent.From, room))
            {
                // Should we send the user welcome message?
                // The event has to be:
                // - adding a new member from a foreign org
                // - to a Persistent room with ManagedConversationsEnabled
                var settings = RoomSettings.Merge(orgSettings, room.Settings);
                if (settings is { WelcomeNewUsers: true, UserWelcomeMessage: { Length: > 0 } welcome })
                {
                    await platformEvent.Responder.SendEphemeralActivityAsync(
                        roomMembershipEventPayload.PlatformUserId,
                        welcome);
                }
            }
        }
    }

    /// <summary>
    /// Called when the user interacts with an element in a message. In our case, the Enable Conversation Tracking
    /// button.
    /// </summary>
    /// <param name="platformMessage">The incoming interaction message.</param>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        var interaction = platformMessage.Payload.InteractionInfo?.ActionElement
            ?? throw new InvalidOperationException("There was no interaction payload.");

        if (interaction.ActionId is not ActionIds.EditConversationTrackingButton)
        {
            // Do nothing.
            return;
        }

        var responder = platformMessage.Responder;
        var from = platformMessage.From;
        var room = platformMessage.Room.Require();

        if (!platformMessage.Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            // We don't show the enable conversations button if the current plan doesn't allow conversation tracking.
            // However, there's the possible edge case where you invite Abbot, wait a bit, your trial expires, then
            // click the button. Yes, pretty unlikely, but we handle it.
            await responder.SendEphemeralActivityAsync(from.User.PlatformUserId, GetPleaseUpgradeMessage(room));
            return;
        }

        if (!from.CanManageConversations())
        {
            // We don't show the enable conversations button if the current user can't manage conversations.
            // This handles an edge case where the button is visible, but the user is removed from an allowed role.
            await responder.SendEphemeralActivityAsync(
                from.User.PlatformUserId,
                GetAskManagerMessage(room));
            return;
        }

        var responseUrl = (platformMessage.Payload.InteractionInfo?.ResponseUrl)
            .Require("Payload response url is null.");

        var triggerId = platformMessage.TriggerId.Require("No trigger id for interaction.");

        var viewState = await LoadViewState(room, responseUrl);
        var modalView = Render(viewState);
        await responder.OpenModalAsync(triggerId, modalView);
    }

    async Task<ViewState> LoadViewState(Room room, Uri responseUrl)
    {
        var organization = room.Organization;

        // If we want to get real fancy, we use an external data source
        // https://api.slack.com/reference/block-kit/block-elements#external_multi_select
        var agents = await _userRepository.FindMembersAsync(
            organization,
            "",
            100,
            Roles.Agent);

        var agentOptions = agents
            .Select(m => new SlackOption(m.User.DisplayName, m.User.PlatformUserId))
            .ToList();
        var existingResponders = room.GetFirstResponders().Select(m => m.User.PlatformUserId).ToHashSet();
        var initialFirstResponderOptions = agentOptions.Where(o => existingResponders.Contains(o.Value)).ToList();

        var defaultFirstResponders = (await _userRepository.GetDefaultFirstRespondersAsync(organization))
            .Select(m => m.User.PlatformUserId)
            .ToList();

        var selectedWarningOption = CreateOptionFromTimeSpan(room.TimeToRespond.Warning);
        var selectedDeadlineOption = CreateOptionFromTimeSpan(room.TimeToRespond.Deadline);

        var viewState = new ViewState(
            organization,
            room,
            initialFirstResponderOptions,
            agentOptions,
            selectedWarningOption,
            selectedDeadlineOption,
            defaultFirstResponders,
            ShowResponseTimeOptions: organization.HasDefaultResponseTimes(),
            CustomOptionSelected: room.HasCustomResponseTimes(),
            new PrivateMetadata(room.PlatformRoomId, responseUrl));
        return viewState;
    }

    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        var action = viewContext.Payload.Actions.SingleOrDefault();
        if (action is not IValueElement valueElement)
        {
            return;
        }

        // We have to rebuild the ViewState from the existing view and the changes the user made.
        var privateMetadata = PrivateMetadata.Parse(viewContext.Payload.View.PrivateMetadata).Require();
        var (room, _) = await GetRoomAndResponseUrlAsync(viewContext.Payload, viewContext.Organization);

        if (action.ActionId is ActionIds.DisableConversationTracking)
        {
            await _roomRepository.SetConversationManagementEnabledAsync(
                room, false, viewContext.FromMember);

            var disabledViewUpdate = RenderDisabled(room, privateMetadata);
            await viewContext.UpdateModalViewAsync(disabledViewUpdate);
            return;
        }

        if (valueElement.ActionId is ActionIds.EnableConversationTracking)
        {
            await _roomRepository.SetConversationManagementEnabledAsync(
                room, true, viewContext.FromMember);

            var enabledViewState = await LoadViewState(room, privateMetadata.ResponseUrl);
            var enabledViewUpdate = Render(enabledViewState);
            await viewContext.UpdateModalViewAsync(enabledViewUpdate);
            return;
        }

        var blocks = viewContext.Payload.View.Blocks;

        var firstRespondersBlock = blocks.FindInputElementByBlockId<MultiStaticSelectMenu>(nameof(SubmissionState.FirstResponders)).Require();

        var targetSelectedOption = blocks.FindInputElementByBlockId<StaticSelectMenu>(nameof(SubmissionState.TargetResponseTime))
            ?.SelectedOption;
        var deadlineSelectedOption = blocks.FindInputElementByBlockId<StaticSelectMenu>(nameof(SubmissionState.DeadlineResponseTime))
            ?.SelectedOption;

        if (targetSelectedOption is null || deadlineSelectedOption is null)
        {
            if (room.HasCustomResponseTimes())
            {
                targetSelectedOption ??= CreateOptionFromTimeSpan(room.TimeToRespond.Warning);
                deadlineSelectedOption ??= CreateOptionFromTimeSpan(room.TimeToRespond.Deadline);
            }
        }

        var defaultFirstResponders = (await _userRepository.GetDefaultFirstRespondersAsync(viewContext.Organization))
            .Select(m => m.User.PlatformUserId)
            .ToList();

        // Need to create the ViewState from the existing view, but with the changes specified by the user interaction.
        var viewState = new ViewState(
            viewContext.Organization,
            room,
            firstRespondersBlock.SelectedOptions,
            firstRespondersBlock.Options.Require(),
            targetSelectedOption,
            deadlineSelectedOption,
            defaultFirstResponders,
            ShowResponseTimeOptions: viewContext.Organization.HasDefaultResponseTimes(),
            CustomOptionSelected: valueElement.Value is ActionIds.CustomResponseTimeSelected,
            privateMetadata);

        var viewUpdate = Render(viewState);
        await viewContext.UpdateModalViewAsync(viewUpdate);
    }

    ViewUpdatePayload Render(ViewState viewState)
    {
        if (!viewState.Room.ManagedConversationsEnabled)
        {
            return RenderDisabled(viewState.Room, viewState.PrivateMetadata);
        }

        var supportees = viewState.Room.SupporteeType();

        SelectMenu userSelectMenu = new MultiStaticSelectMenu(viewState.AgentOptions.ToArray())
        {
            Placeholder = "Select first responders",
            // It's possible we have this room in our db already if abbot was removed and added again.
            InitialOptions = viewState.InitialFirstResponderOptions,
            ActionId = nameof(SubmissionState.FirstResponders)
        };

        var botAppName = viewState.Organization.BotAppName ?? "Abbot";
        var blocks = new List<ILayoutBlock>
        {
            new Section(
                new MrkdwnText( $":white_check_mark: {botAppName} tracks conversations started by *{supportees}* in this room." ),
                new ButtonElement("Stop tracking", "Disable")
                {
                    ActionId = ActionIds.DisableConversationTracking,
                    Style = ButtonStyle.Default,
                    Confirm = new(
                        "Stop Conversation Tracking",
                        new PlainText("Are you sure?"),
                        "Stop tracking",
                        "Continue tracking")
                    {
                        Style = ButtonStyle.Danger,
                    },
                }),
        };

        if (viewState.Room.Shared is not true)
        {
            var customerOption = new CheckOption(new MrkdwnText("*Customer Support*"), value: "false", description: new MrkdwnText("Only guest users start tracked conversations."));
            var communityOption = new CheckOption(new MrkdwnText("*Community Support*"), value: "true", description: new MrkdwnText("All non-Agent users start tracked conversations."));
            blocks.Add(new Actions(new RadioButtonGroup(customerOption, communityOption)
            {
                InitialOption = viewState.Room.Settings?.IsCommunityRoom is true
                    ? communityOption
                    : customerOption,
                ActionId = nameof(SubmissionState.IsCommunityRoom),
            })
            {
                BlockId = nameof(SubmissionState.IsCommunityRoom),
            });
        }

        blocks.Add(new Input("First Responders", userSelectMenu)
        {
            // Needed so we can identify this in the state dictionary.
            BlockId = nameof(SubmissionState.FirstResponders),
            Optional = viewState.DefaultFirstResponders.Any()
        });

        if (viewState.DefaultFirstResponders.Any())
        {
            var roomsSettingPage = _urlGenerator.RoomsSettingsPage(new FragmentString("#default-first-responders"));
            var hyperlink = new Hyperlink(roomsSettingPage, "default first responders");
            var defaultResponders = viewState.DefaultFirstResponders
                .Select(SlackFormatter.UserMentionSyntax)
                .Humanize();
            blocks.Add(new Context(new MrkdwnText($"Leave blank to use {hyperlink}: {defaultResponders}")));
        }

        blocks.AddRange(new ILayoutBlock[]
        {
            new Divider(),
            new Section($":alarm_clock: {botAppName} notifies first responders of conversations that do not receive "
                + "a response by the target and deadline response times.")
        });

        var organization = viewState.Organization;

        if (viewState.ShowResponseTimeOptions)
        {
            var timeToRespond = organization.DefaultTimeToRespond;
            var defaultWarning = timeToRespond
                .Warning
                .GetValueOrDefault()
                .Humanize();
            var defaultDeadline = timeToRespond
                .Deadline
                .GetValueOrDefault()
                .Humanize();

            var defaultOption = new CheckOption(
                "Use your organization’s defaults",
                ActionIds.DefaultResponseTimeSelected,
                new MrkdwnText($"*Target:* {defaultWarning}, *Deadline:* {defaultDeadline}"));
            var customOption = new CheckOption(
                "Use custom response times",
                ActionIds.CustomResponseTimeSelected);

            var options = new RadioButtonGroup(defaultOption, customOption)
            {
                InitialOption = viewState.CustomOptionSelected
                    ? customOption
                    : defaultOption
            };
            blocks.Add(new Actions(options) { BlockId = nameof(SubmissionState.ResponseTimeOption) });
        }

        if (viewState.CustomOptionSelected || !viewState.ShowResponseTimeOptions)
        {
            AddResponseTimeInputBlocks(blocks, viewState);
        }

        return new ViewUpdatePayload
        {
            Title = "Conversation Tracking",
            Submit = "Save settings",
            Close = "Cancel",
            CallbackId = InteractionCallbackInfo.For<RoomMembershipPayloadHandler>(), // TODO: Make this automatic.
            PrivateMetadata = viewState.PrivateMetadata,
            NotifyOnClose = false,
            Blocks = blocks
        };
    }

    static ViewUpdatePayload RenderDisabled(Room room, PrivateMetadata privateMetadata)
    {
        var botAppName = room.Organization.BotAppName ?? "Abbot";
        return new ViewUpdatePayload
        {
            Title = "Conversation Tracking",
            Close = "Close",
            CallbackId = InteractionCallbackInfo.For<RoomMembershipPayloadHandler>(),
            PrivateMetadata = privateMetadata,
            NotifyOnClose = false,
            Blocks = new List<ILayoutBlock> {
                new Section(
                    new MrkdwnText(
                        $":information_source: {botAppName} is not tracking conversations in this room."),
                    new ButtonElement("Track conversations")
                    {
                        ActionId = ActionIds.EnableConversationTracking,
                        Style = ButtonStyle.Primary,
                    }),
            },
        };
    }

    static void AddResponseTimeInputBlocks(
        ICollection<ILayoutBlock> blocks,
        ViewState viewState)
    {
        var responseTimeOptions = new[]
        {
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(4),
            TimeSpan.FromHours(8),
            TimeSpan.FromDays(1)
        }
        .Select(ts => CreateOptionFromTimeSpan(ts))
        .ToArray();

        var targetResponseTimeOptions = responseTimeOptions;
        var deadlineResponseTimeOptions = responseTimeOptions;

        var organization = viewState.Organization;
        var selectedTargetOption = viewState.SelectedWarningOption
            ?? CreateOptionFromTimeSpan(organization.DefaultTimeToRespond.Warning);
        var selectedDeadlineOption = viewState.SelectedDeadlineOption
            ?? CreateOptionFromTimeSpan(organization.DefaultTimeToRespond.Deadline);

        if (selectedTargetOption is not null && !targetResponseTimeOptions.Contains(selectedTargetOption))
        {
            targetResponseTimeOptions = targetResponseTimeOptions
                .Append(selectedTargetOption)
                .OrderBy(o => o.Value)
                .ToArray();
        }

        if (selectedDeadlineOption is not null && !deadlineResponseTimeOptions.Contains(selectedDeadlineOption))
        {
            deadlineResponseTimeOptions = deadlineResponseTimeOptions
                .Append(selectedDeadlineOption)
                .OrderBy(o => o.Value)
                .ToArray();
        }

        blocks.AddRange(new ILayoutBlock[]
        {
            new Input(
                "Target Response Time",
                new StaticSelectMenu(
                    nameof(SubmissionState.TargetResponseTime),
                    targetResponseTimeOptions)
                {
                    InitialOption = selectedTargetOption
                })
            {
                BlockId = nameof(SubmissionState.TargetResponseTime),
                Optional = true
            },
            new Input(
                "Deadline Response Time",
                new StaticSelectMenu(
                    nameof(SubmissionState.DeadlineResponseTime),
                    deadlineResponseTimeOptions)
                {
                    InitialOption = selectedDeadlineOption
                })
            {
                BlockId = nameof(SubmissionState.DeadlineResponseTime),
                Optional = true
            }
        });
    }

    /// <summary>
    /// Handles the onboarding modal submission. In this case, it saves the first responders.
    /// </summary>
    /// <param name="viewContext"></param>
    /// <exception cref="UnreachableException"></exception>
    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        var state = viewContext.Payload.View.State.Require();

        var submissionState = state.BindRecord<SubmissionState>();

        var (room, responseUrl) = await GetRoomAndResponseUrlAsync(viewContext.Payload, viewContext.Organization);

        if (!viewContext.Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            // We don't show the dialog if the current plan doesn't allow conversation tracking.
            // However, there's the possible edge case where you invite Abbot, wait a bit, your trial expires, then
            // click the button. Yes, pretty unlikely, but we handle it.
            var upgradeMessage = new RichActivity(GetPleaseUpgradeMessage(room));
            await viewContext.UpdateActivityAsync(upgradeMessage, responseUrl);
            return;
        }

        if (!viewContext.FromMember.CanManageConversations())
        {
            // We don't show the dialog if the current user can't manage conversations.
            // This handles an edge case where the dialog is visible, but the user is removed from an allowed role.
            var askMessage = new RichActivity(GetAskManagerMessage(room));
            await viewContext.UpdateActivityAsync(askMessage, responseUrl);
            return;
        }

        var isCommunityRoom = submissionState.IsCommunityRoom is "true";
        if (room.Settings?.IsCommunityRoom != isCommunityRoom)
        {
            room.Settings = (room.Settings ?? new RoomSettings()) with
            {
                IsCommunityRoom = isCommunityRoom
            };
            await _roomRepository.UpdateAsync(room);
        }

        if (submissionState.ResponseTimeOption is ActionIds.CustomResponseTimeSelected or null)
        {
            if (submissionState is { TargetResponseTime: { } targetResponseValue, DeadlineResponseTime: { } deadlineResponseTime })
            {
                // Grab response times
                var targetResponseTimeSpan = TimeSpan.Parse(targetResponseValue, CultureInfo.InvariantCulture);
                var deadlineResponseTimeSpan = TimeSpan.Parse(deadlineResponseTime, CultureInfo.InvariantCulture);

                if (deadlineResponseTimeSpan < targetResponseTimeSpan)
                {
                    viewContext.ReportValidationErrors(
                        nameof(SubmissionState.DeadlineResponseTime),
                        "Deadline response time must be greater than target response time.");
                    return;
                }

                await _roomRepository.UpdateResponseTimesAsync(
                    room,
                    targetResponseTimeSpan,
                    deadlineResponseTimeSpan,
                    viewContext.FromMember);
            }
        }
        else if (submissionState.ResponseTimeOption is ActionIds.DefaultResponseTimeSelected)
        {
            await _roomRepository.UpdateResponseTimesAsync(
                room,
                null,
                null,
                viewContext.FromMember);
        }

        await _roomRepository.SetRoomAssignmentsAsync(
            room,
            submissionState.FirstResponders,
            RoomRole.FirstResponder,
            viewContext.FromMember);

        string successMessage;

        if (submissionState.FirstResponders.Count > 0)
        {
            var selectedResponders = submissionState
                .FirstResponders
                .Select(SlackFormatter.UserMentionSyntax)
                .Humanize();

            successMessage = $"Great! {selectedResponders} will be notified of conversations in this room.";
        }
        else
        {
            var defaultFirstResponders = (await _userRepository.GetDefaultFirstRespondersAsync(viewContext.Organization))
                .Select(m => m.User.PlatformUserId)
                .Select(SlackFormatter.UserMentionSyntax)
                .Humanize();
            successMessage = $"The default first responders ({defaultFirstResponders}) will be notified of conversations in this room.";
        }

        var blocks = GetInitialStepBlocks(room, viewContext.FromMember)
            .Append(new Section(new MrkdwnText(successMessage)));

        var activity = new RichActivity(successMessage, blocks.ToArray())
        {
            ResponseUrl = responseUrl
        };

        // Remove the buttons from the existing message and add a line that they enabled conversation tracking.
        await viewContext.UpdateActivityAsync(activity, responseUrl);
    }

    async Task<(Room, Uri)> GetRoomAndResponseUrlAsync(IViewPayload viewPayload, Organization organization)
    {
        var privateMetaData = PrivateMetadata.Parse(viewPayload.View.PrivateMetadata).Require();

        var (channel, responseUrl) = privateMetaData;
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(channel, organization)
                   ?? throw new InvalidOperationException($"The room {channel} does not exist.");

        return (room, responseUrl);
    }

    async Task HandleAbbotAddedToRoom(
        IPlatformEvent platformEvent,
        Room room,
        Member? inviter)
    {
        if (room.BotIsMember is true && inviter is not null)
        {
            var inviterUserId = inviter.User.PlatformUserId;
            var responder = platformEvent.Responder;

            // Let's send a nice ephemeral message to the room (see #2279)
            await responder.SendEphemeralActivityAsync(
                inviterUserId,
                GetHeader(room, inviter, plainText: true),
                GetInitialStepBlocks(room, inviter));

            _systemSignaler.EnqueueSystemSignal(
                SystemSignal.AbbotAddedToRoom,
                room.Name ?? room.PlatformRoomId, // It's more likely users will want to filter a signal on a room name
                platformEvent.Organization,
                room.ToPlatformRoom(),
                inviter,
                null);

            var outputs = new OutputsBuilder()
                .SetRoom(room)
                .Outputs;
            await _playbookDispatcher.DispatchAsync(
                AbbotAddedTrigger.Id,
                outputs,
                platformEvent.Organization,
                PlaybookRunRelatedEntities.From(room));
        }
    }

    string GetHeader(Room room, Member inviter, bool plainText = false)
    {
        var planSupportsConversationTracking = room
            .Organization
            .HasPlanFeature(PlanFeature.ConversationTracking);

        var header = new StringBuilder("Thanks for the invitation! ");

        if (planSupportsConversationTracking)
        {
            var supportees = room.SupporteeType();
            header.Append(CultureInfo.InvariantCulture, $"I will {Link("https://docs.ab.bot/convos/", "track conversations")} from {supportees} in this room");
            if (inviter.CanManageConversations())
            {
                header.Append(CultureInfo.InvariantCulture, $", and you can track other conversations with the {Bold("Manage Conversation")} message shortcut");
            }
            header.Append('.');
        }
        else
        {
            header.Append("If you'd like me to keep track of customer conversations you'll need to upgrade to a Business plan.");
            if (inviter.IsAdministrator())
            {
                header.Append(CultureInfo.InvariantCulture, $" You can do that here: {Link(_urlGenerator.OrganizationBillingPage().ToString())}.");
            }
        }

        return header.ToString();

        string Link(string url, string? text = null) =>
            plainText ? text ?? url : text is null ? $"<{url}>" : $"<{url}|{text}>";

        string Bold(string text) =>
            plainText ? text : $"*{text}*";
    }

    ILayoutBlock[] GetInitialStepBlocks(Room room, Member inviter)
    {
        var headerText = GetHeader(room, inviter);
        var planSupportsConversationTracking = room
            .Organization
            .HasPlanFeature(PlanFeature.ConversationTracking);

        var actions = new List<IActionElement>();
        if (inviter.CanManageConversations() && planSupportsConversationTracking)
        {
            actions.Add(new ButtonElement("Edit conversation tracking")
            {
                Style = ButtonStyle.Primary,
                ActionId = ActionIds.EditConversationTrackingButton,
            });
        }
        actions.Add(new ButtonElement("Take me to Ab.bot", Url: _urlGenerator.HomePage()));

        return new ILayoutBlock[]
        {
            new Section(new MrkdwnText(headerText)),
            new Actions(actions.ToArray())
            {
                // I'd like the callback info to be part of the context, but for now we need to do this.
                BlockId = new InteractionCallbackInfo(GetType().Name)
            }
        };
    }

    public async Task<BlockSuggestionsResponse> OnBlockSuggestionRequestAsync(
        IPlatformEvent<BlockSuggestionPayload> platformEvent)
    {
        var query = platformEvent.Payload.Value;
        query = query is { Length: > 0 } && query[0] == '@'
            ? query[1..]
            : query;

        var suggestions = await _userRepository.FindMembersAsync(
            platformEvent.Organization,
            query,
            100,
            Roles.Agent);

        var options = suggestions
            .Select(m => new SlackOption(m.User.DisplayName, m.User.PlatformUserId));

        return new OptionsBlockSuggestionsResponse(options);
    }

    record PrivateMetadata(string Channel, Uri ResponseUrl) : PrivateMetadataBase
    {
        public static PrivateMetadata? Parse(string? privateMetadata)
        {
            return TrySplitParts(privateMetadata, 2, out var parts)
                && Uri.TryCreate(parts[1], UriKind.Absolute, out var responseUrl)
                ? new PrivateMetadata(parts[0], responseUrl)
                : null;
        }

        protected override IEnumerable<string> GetValues()
        {
            yield return Channel;
            yield return ResponseUrl.ToString();
        }

        public override string ToString() => base.ToString();
    }

    string GetAskManagerMessage(Room room)
    {
        return $"To enable conversation tracking, please ask an https://ab.bot/ Agent or Administrator to visit {new Hyperlink(_urlGenerator.RoomSettingsPage(room), "Room Settings")} to enable conversation tracking.";
    }

    string GetPleaseUpgradeMessage(Room room)
    {
        return $"Please {new Hyperlink(_urlGenerator.OrganizationBillingPage(), "upgrade")} your plan and then go to {new Hyperlink(_urlGenerator.RoomSettingsPage(room), "Room Settings")} to enable conversation tracking.";
    }

    [return: NotNullIfNotNull("timeSpan")]
    static SlackOption? CreateOptionFromTimeSpan(TimeSpan? timeSpan)
    {
        return timeSpan is { } ts
            ? new SlackOption(ts.Humanize(culture: CultureInfo.InvariantCulture), ts.ToString())
            : null;
    }
}
