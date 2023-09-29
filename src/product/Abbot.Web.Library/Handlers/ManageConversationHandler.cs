using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Handles interactions with the "Kabob menu", aka the message context menu.
/// </summary>
public class ManageConversationHandler : IHandler
{
    readonly TicketModalService _ticketModalService;
    readonly IConversationRepository _conversationRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly IUrlGenerator _urlGenerator;
    readonly IClock _clock;

    public ManageConversationHandler(
        TicketModalService ticketModalService,
        IConversationRepository conversationRepository,
        IIntegrationRepository integrationRepository,
        IUrlGenerator urlGenerator,
        IClock clock)
    {
        _ticketModalService = ticketModalService;
        _conversationRepository = conversationRepository;
        _integrationRepository = integrationRepository;
        _urlGenerator = urlGenerator;
        _clock = clock;
    }

    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        var triggerId = platformMessage.TriggerId.Require();

        var state = await GatherViewStateAsync(platformMessage);

        var payload = Render(state);
        await platformMessage.Responder.OpenModalAsync(triggerId, payload);
    }

    async Task<ViewState> GatherViewStateAsync(IPlatformMessage platformMessage)
    {
        var hasConvoTracking = platformMessage.Organization.HasPlanFeature(PlanFeature.ConversationTracking);

        var room = platformMessage.Room;
        if (room is null)
        {
            return new ViewState(
                Room: null,
                Conversation: null,
                HasConversationTracking: hasConvoTracking,
                Array.Empty<TicketingIntegration>(),
                MessageId: string.Empty,
                Channel: string.Empty,
                ConfigureIntegrationUrl: null,
                CanCreateAnnouncement: false,
                CanTagConversation: false);
        }

        // Look up the conversation for this message
        var messageId = platformMessage.Payload.MessageId.Require();
        var rootMessageId = platformMessage.Payload.ThreadId ?? platformMessage.Payload.MessageId.Require();
        var convo = await _conversationRepository.GetConversationByThreadIdAsync(
            rootMessageId,
            room,
            followHubThread: true);

        var sourceMessagePlatformUserId = platformMessage.Payload.InteractionInfo?.SourceMessage?.User;

        return await GatherViewStateAsync(
            sourceMessagePlatformUserId,
            room,
            convo,
            hasConvoTracking,
            messageId,
            platformMessage.From);
    }

    async Task<ViewState> GatherViewStateAsync(
        string? sourceMessagePlatformUserId,
        Room room,
        Conversation? conversation,
        bool hasConvoTracking,
        string messageId,
        Member from)
    {
        var ticketingIntegrations = await _integrationRepository.GetTicketingIntegrationsAsync(room.Organization);

        var configureIntegrationUrl = from.IsAdministrator()
            ? _urlGenerator.IntegrationSettingsPage()
            : null;

        // Look at the source message to see if it was created by the current user.
        var canCreateAnnouncement = conversation is null
            && sourceMessagePlatformUserId == from.User.PlatformUserId
            && room.BotIsMember is true
            && from.IsAgent();

        var canTagConversation = from.IsAgent();

        return new ViewState(
            room,
            conversation,
            hasConvoTracking,
            ticketingIntegrations,
            messageId,
            room.PlatformRoomId,
            configureIntegrationUrl,
            canCreateAnnouncement,
            canTagConversation);
    }

    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        var payloadAction = viewContext.Payload.Actions.Single();

        if (payloadAction is ButtonElement buttonElement)
        {
            if (payloadAction.BlockId is BlockIds.TrackConversation && buttonElement.Value is { } buttonValue)
            {
                var (channel, messageId, _) = ConversationIdentifier.Parse(buttonValue);
                var conversation = await _ticketModalService.TrackConversationAsync(
                    viewContext.FromMember,
                    channel.Require(),
                    messageId.Require(),
                    viewContext.Organization);
                await ShowConversationModalAsync(viewContext, conversation);
                return;
            }

            if (Enum.TryParse<ConversationState>(buttonElement.ActionId, out var newState)
                && int.TryParse(buttonElement.Value, out var conversationId))
            {
                var conversation = await HandleConversationStateChangeAsync(viewContext.FromMember, conversationId, newState);
                await ShowConversationModalAsync(viewContext, conversation);
            }
        }
    }

    async Task<Conversation> HandleConversationStateChangeAsync(
        Member actor,
        int conversationId,
        ConversationState newState)
    {
        var conversation = await _conversationRepository.GetConversationAsync(conversationId);
        if (conversation is null)
        {
            throw new InvalidOperationException($"Conversation {conversationId} not found for Organization {actor.Organization.Id}.");
        }

        await _conversationRepository.ChangeConversationStateAsync(conversation,
            newState,
            actor,
            _clock.UtcNow,
            "modal");
        return conversation;
    }

    async Task ShowConversationModalAsync(IViewContext<IViewBlockActionsPayload> viewContext, Conversation conversation)
    {
        var state = await GatherViewStateAsync(
            null,
            conversation.Room,
            conversation,
            viewContext.Organization.HasPlanFeature(PlanFeature.ConversationTracking),
            conversation.FirstMessageId,
            viewContext.FromMember);

        var viewUpdate = Render(state);
        await viewContext.UpdateModalViewAsync(viewUpdate);
    }

    ViewUpdatePayload Render(ViewState state)
    {
        var blocks = new List<ILayoutBlock>();
        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<ManageConversationHandler>(),
            Title = "Manage Conversation",
            Close = "Close",
            Blocks = blocks,
            PrivateMetadata = new AnnouncementHandler.PrivateMetadata(state.Channel, state.MessageId),
        };

        if (state.Conversation is { State: not ConversationState.Hidden } conversation)
        {
            var webUrl = _urlGenerator.ConversationDetailPage(conversation.Id);
            blocks.Add(new Section("🌐 View this conversation on ab.bot")
            {
                BlockId = "view_conversation",
                Accessory = new ButtonElement("View", Url: webUrl),
            });

            blocks.Add(new Section("✋ Assign this conversation")
            {
                // This gets routed to AssignConversationModal.OnInteractionAsync()
                BlockId = InteractionCallbackInfo.For<AssignConversationModal>(),
                Accessory = new ButtonElement("Assign", $"{conversation.Id}"),
            });

            if (state.CanTagConversation)
            {
                blocks.Add(new Section("🏷️ Tag this conversation")
                {
                    // This gets routed to TagConversationModal.OnInteractionAsync()
                    BlockId = InteractionCallbackInfo.For<TagConversationModal>(),
                    Accessory = new ButtonElement("Tag", $"{conversation.Id}"),
                });
            }

            if (conversation.State != ConversationState.Archived)
            {
                blocks.Add(GetOpenCloseSection(conversation));
            }

            blocks.Add(GetArchiveUnarchiveButton(conversation));
        }
        else if (state.HasConversationTracking is false)
        {
            // Don't render this if the room is null.
            blocks.Add(new Section("ℹ️ Conversation Tracking"));

            var explanation = "You must upgrade your plan to use this feature.";
            blocks.Add(new Context(new MrkdwnText(explanation)));
        }
        else if (state.Room is { BotIsMember: not true })
        {
            // Don't render this if the room is null.
            blocks.Add(new Section("ℹ️ Conversation Tracking"));

            var botName = state.Room.Organization.BotName;
            var explanation = $"Invite `@{botName}` to track conversations.";
            blocks.Add(new Context(new MrkdwnText(explanation)));
        }
        else if (state.Room?.ManagedConversationsEnabled == false)
        {
            // Don't render this if the room is null.
            blocks.Add(new Section("ℹ️ Conversation Tracking"));

            var explanation = "Conversation tracking is not enabled for this room.";
            blocks.Add(new Context(new MrkdwnText(explanation)));
        }
        else if (state.Room is not null)
        {
            var buttonValue = new ConversationIdentifier(Channel: state.Channel, state.MessageId);
            blocks.Add(new Section("ℹ️ Conversation Tracking")
            {
                BlockId = BlockIds.TrackConversation,
                Accessory = new ButtonElement("Track Conversation", Value: buttonValue)
                {
                    ActionId = ActionIds.TrackConversation,
                },
            });

            blocks.Add(new Context(new MrkdwnText(
                "This message is not associated with a Conversation. " +
                // TODO: Can we guess which of these it was without much work?
                "That may be because it was posted by a bot, by someone from your organization, or during a time when Conversation Tracking was not enabled.")));
        }

        foreach (var (ticketing, settings) in state.TicketingIntegrations)
        {
            blocks.AddRange(GetTicketButtonBlocks(state, ticketing, settings));
        }

        if (state.CanCreateAnnouncement)
        {
            blocks.Add(new Section("📣 Create an announcement from this message.")
            {
                BlockId = "create_announcement",
                Accessory = new ButtonElement("Create Announcement")
                {
                    ActionId = new InteractionCallbackInfo(nameof(AnnouncementHandler))
                },
            });
        }

        if (blocks.Count is 0)
        {
            // Are there any actions that will always be available?
            blocks.Add(new Section("Sorry, there are no actions you can take on this message."));
        }

        return payload;
    }

    static IEnumerable<ILayoutBlock> GetTicketButtonBlocks(
        ViewState state,
        Integration integration,
        ITicketingSettings settings)
    {
        var ticketType = settings.IntegrationName;
        var ticketTypeLower = settings.IntegrationSlug;
        var conversationLink = settings.FindLink(state.Conversation, integration);
        var existingLink = settings.GetTicketLink(conversationLink)?.WebUrl;
        if (existingLink is not null)
        {
            yield return new Section($"🎫 View the {ticketType} ticket linked to this conversation")
            {
                BlockId = $"{ticketTypeLower}_link",
                Accessory = new ButtonElement("View")
                {
                    Url = existingLink,
                }
            };
        }
        else if (state.HasConversationTracking && integration.Enabled)
        {
            if (!settings.HasApiCredentials)
            {
                yield return new Section(new MrkdwnText($"_{ticketType} credentials are not configured._"))
                {
                    BlockId = $"{ticketTypeLower}_link",
                    Accessory = state.ConfigureIntegrationUrl is not null
                        ? new ButtonElement("Configure")
                        {
                            Url = state.ConfigureIntegrationUrl,
                        }
                        : null
                };
            }
            else if (state.Room is { BotIsMember: not true })
            {
                var botName = state.Room.Organization.BotName;
                yield return new Section(new MrkdwnText($"_Invite `@{botName}` to create {ticketType} tickets._"))
                {
                    BlockId = $"{ticketTypeLower}_link",
                    Accessory = state.ConfigureIntegrationUrl is not null
                        ? new ButtonElement("Configure")
                        {
                            Url = state.ConfigureIntegrationUrl,
                        }
                        : null
                };
            }
            else
            {
                yield return new Section($"🎫 Create a {ticketType} ticket for this conversation")
                {
                    BlockId = CreateTicketFormModal.For(integration),
                    Accessory = new ButtonElement("Create Ticket")
                    {
                        ActionId = CreateTicketFormModal.For(integration),
                        Value = new ConversationIdentifier(state.Channel, state.MessageId, state.Conversation?.Id),
                    }
                };
            }
        }
    }

    static Section GetOpenCloseSection(Conversation conversation)
    {
        var (openCloseText, openCloseButtonText, newState) = conversation.State.IsOpen()
            ? ("✅ Close this conversation", "Close", ConversationState.Closed)
            : ("✅ Reopen this conversation", "Reopen", ConversationState.Waiting);

        var openCloseSection = new Section(openCloseText)
        {
            BlockId = "open_close_conversation",
            Accessory = new ButtonElement(openCloseButtonText, Value: $"{conversation.Id}")
            {
                ActionId = newState.ToString()
            },
        };

        return openCloseSection;
    }

    static Section GetArchiveUnarchiveButton(Conversation conversation)
    {
        var (text, buttonText, newState) = conversation.State != ConversationState.Archived
            ? ("🚫 Stop tracking conversation", "Stop", ConversationState.Archived)
            : ("🚫 Unarchive conversation", "Unarchive", ConversationState.Closed);

        return new Section(text)
        {
            BlockId = "archive_unarchive",
            Accessory = new ButtonElement(buttonText, Value: $"{conversation.Id}")
            {
                ActionId = $"{newState}",
                Style = ButtonStyle.Danger
            },
        };
    }

    record ViewState(
        Room? Room,
        Conversation? Conversation,
        bool HasConversationTracking,
        IReadOnlyList<TicketingIntegration> TicketingIntegrations,
        string MessageId,
        string Channel,
        Uri? ConfigureIntegrationUrl,
        bool CanCreateAnnouncement,
        bool CanTagConversation);

    public static class ActionIds
    {
        public const string TrackConversation = nameof(TrackConversation);
    }

    public static class BlockIds
    {
        public const string TrackConversation = nameof(TrackConversation);
    }
}
