using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Humanizer;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.PayloadHandlers;

public class AppHomePageHandler : IHandler
{
    const int PageSize = 5;
    internal const string SetConversationStatePrefix = "set_conversation_state:";
    static readonly ILogger<AppHomePageHandler> Log = ApplicationLoggerFactory.CreateLogger<AppHomePageHandler>();

    internal class BlockIds
    {
        public const string ConversationPrefix = "conversation:";
    }

    internal class ActionIds
    {
        public const string GoToThread = "go_to_thread";
        public const string ViewConversation = "view_conversation";
        public const string ConversationAction = "conversation_action";
        public const string OpenHomePage = "open_home_page";
    }

    readonly ISlackApiClient _slackApiClient;
    readonly IAnalyticsClient _analyticsClient;
    readonly IConversationRepository _conversationRepository;
    readonly IClock _clock;
    readonly IUrlGenerator _urlGenerator;

    public AppHomePageHandler(ISlackApiClient slackApiClient, IAnalyticsClient analyticsClient,
        IConversationRepository conversationRepository, IClock clock, IUrlGenerator urlGenerator)
    {
        _slackApiClient = slackApiClient;
        _analyticsClient = analyticsClient;
        _conversationRepository = conversationRepository;
        _clock = clock;
        _urlGenerator = urlGenerator;
    }

    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        var action = viewContext.Payload.Actions.SingleOrDefault();
        if (action is not IValueElement valueElement)
        {
            return;
        }

        _analyticsClient.Track(
            "App Home Action",
            AnalyticsFeature.AppHome,
            viewContext.FromMember,
            viewContext.Organization,
            new()
            {
                // We know that the action_ids we use here are stable.
                ["action_id"] = valueElement.ActionId,
                ["value"] = valueElement.Value
            });

        if (valueElement.BlockId is not null && valueElement.BlockId.StartsWith(BlockIds.ConversationPrefix, StringComparison.Ordinal)
                                             && valueElement.ActionId is ActionIds.ConversationAction
                                             && valueElement.Value is not null
                                             && valueElement.Value.StartsWith(SetConversationStatePrefix, StringComparison.Ordinal))
        {
            var conversationId = int.Parse(valueElement.BlockId[BlockIds.ConversationPrefix.Length..], CultureInfo.InvariantCulture);
            var targetState =
                Enum.Parse<ConversationState>(valueElement.Value[SetConversationStatePrefix.Length..], true);

            await SetConversationStateAsync(conversationId, targetState, viewContext);
        }

        // Re-publish the app home view to catch any updates
        await PublishAppHomePageAsync(viewContext.Bot, viewContext.Organization, viewContext.FromMember);
    }

    async Task SetConversationStateAsync(int conversationId, ConversationState targetState,
        IViewContext<IViewBlockActionsPayload> viewContext)
    {
        var conversation = await _conversationRepository.GetConversationAsync(conversationId);
        if (conversation is null)
        {
            return;
        }

        await _conversationRepository.ChangeConversationStateAsync(conversation,
            targetState,
            viewContext.FromMember,
            _clock.UtcNow,
            "app home");
    }

    public async Task PublishAppHomePageAsync(
        BotChannelUser bot,
        Organization organization,
        Member from)
    {
        // Go fetch the priority queue
        var query = new ConversationQuery(organization.Id)
            .InRoomsWhereResponder(from)
            .WithState(ConversationStateFilter.Open);

        var list = await _conversationRepository.QueryConversationsWithStatsAsync(query, _clock.UtcNow, 1, PageSize);

        var blocks = new List<ILayoutBlock>();
        blocks.AddRange(RenderConversationRows(list.Conversations));
        blocks.Add(new Actions(
            new ButtonElement(":globe_with_meridians: View more on ab.bot", Url: _urlGenerator.HomePage())
            {
                ActionId = ActionIds.OpenHomePage,
            }));

        var appHomeView = new AppHomeView
        {
            Blocks = blocks,
            CallbackId = new InteractionCallbackInfo(nameof(AppHomePageHandler))
        };

        var request = new PublishAppHomeRequest(from.User.PlatformUserId, appHomeView);

        if (bot.TryGetUnprotectedApiToken(out var apiToken))
        {
            var response = await _slackApiClient.PublishViewAsync(apiToken, request);
            if (!response.Ok)
            {
                Log.ErrorCallingSlackApi(response.ToString());
            }

            _analyticsClient.Screen(
                "App Home Screen",
                AnalyticsFeature.AppHome,
                from,
                organization,
                new()
                {
                    ["success"] = response.Ok,
                    ["version"] = 2,
                });
        }
    }

    IEnumerable<ILayoutBlock> RenderConversationRows(IReadOnlyList<Conversation> conversations)
    {
        yield return new Header(":speech_balloon: Your Conversations");

        foreach (var conversation in conversations)
        {
            yield return new Divider();

            var menuOptions = new List<OverflowOption>();

            if (conversation.State != ConversationState.Unknown)
            {
                if (conversation.State != ConversationState.Archived)
                {
                    menuOptions.Add(GetOpenCloseOption(conversation));
                }

                menuOptions.Add(GetArchiveUnarchiveOption(conversation));
            }

            yield return new Section(
                new MrkdwnText(
                    $"""
                    {conversation.StartedBy.User.ToMention()} in {conversation.Room.ToMention()} {conversation.Created.Humanize()}
                    {conversation.Title.TruncateAtWordBoundary(120, appendEllipses: true)}
                    """),
                new OverflowMenu(menuOptions)
                {
                    ActionId = ActionIds.ConversationAction
                })
            {
                BlockId = $"conversation:{conversation.Id}",
            };

            yield return new Context(
                $"<{conversation.GetFirstMessageUrl()}|Go to thread> • " +
                $"<{_urlGenerator.ConversationDetailPage(conversation.Id)}|View on ab.bot> • " +
                $"Last message posted {conversation.LastMessagePostedOn.Humanize()}");
        }
    }

    static OverflowOption GetOpenCloseOption(Conversation conversation)
    {
        var (openCloseButtonText, newState) = conversation.State.IsOpen()
            ? ("✅ Close Conversation", ConversationState.Closed)
            : ("✅ Reopen Conversation", ConversationState.Waiting);

        return new(openCloseButtonText, $"{SetConversationStatePrefix}{newState}");
    }

    static OverflowOption GetArchiveUnarchiveOption(Conversation conversation)
    {
        var (buttonText, newState) = conversation.State != ConversationState.Archived
            ? ("🚫 Stop Tracking Conversation", ConversationState.Archived)
            : ("🚫 Unarchive Conversation", ConversationState.Closed);

        return new OverflowOption(buttonText, $"{SetConversationStatePrefix}{newState}");
    }
}
