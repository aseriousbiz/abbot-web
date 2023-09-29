using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.PayloadHandlers;

public class AssignConversationModal : IHandler
{
    static readonly ILogger<AssignConversationModal> Log =
        ApplicationLoggerFactory.CreateLogger<AssignConversationModal>();

    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly ISlackApiClient _slackApiClient;

    public AssignConversationModal(
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        IRoleManager roleManager,
        ISlackApiClient slackApiClient)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _roleManager = roleManager;
        _slackApiClient = slackApiClient;
    }

    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        if (viewContext.Payload.Actions.SingleOrDefault() is ButtonElement { Value: { Length: > 0 } value }
            && int.TryParse(value, out var conversationId))
        {
            var viewState = await GatherViewState(conversationId, viewContext.FromMember);
            var viewUpdatePayload = Render(viewState);
            await viewContext.PushModalViewAsync(viewUpdatePayload);
        }
    }

    async Task<ViewState> GatherViewState(int conversationId, Member actor)
    {
        var conversation = await _conversationRepository.GetConversationAsync(conversationId).Require();

        // We only allow assigning a conversation to a single person for now.
        var assignee = conversation.Assignees.SingleOrDefault();
        var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, conversation.Organization);

        return new ViewState(conversation, agents, assignee, actor);
    }

    static ViewUpdatePayload Render(ViewState viewState)
    {
        var (conversation, agents, assignee, actor) = viewState;
        // We want to list the current user first, then by alphabetical.
        var agentOptions = agents
            .OrderByDescending(a => a.Id == actor.Id)
            .ThenBy(a => a.DisplayName)
            .Select(ToOption)
            .ToList();
        var blocks = new List<ILayoutBlock>
        {
            new Section
            {
                Text = new MrkdwnText($"*{conversation.Title}*")
            },
            new Divider(),
            new Input
            {
                Label = new PlainText("Assign to an Agent"),
                BlockId = BlockIds.AgentsInput,
                Element = new StaticSelectMenu
                {
                    ActionId = ActionIds.AgentsSelectMenu,
                    Placeholder = new PlainText("Select an Agent"),
                    Options = agentOptions,
                    InitialOption = assignee is null ? null : ToOption(assignee)
                },
                Optional = true,
            }
        };

        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<AssignConversationModal>(),
            Title = "Assign Conversation",
            Submit = "Assign",
            Close = "Close",
            Blocks = blocks,
            PrivateMetadata = $"{conversation.Id}",
        };

        return payload;
    }

    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        var state = viewContext.Payload.View.State.Require("No state in payload");

        // Load the conversation
        var conversationId = int.Parse(viewContext.Payload.View.PrivateMetadata.Require(), CultureInfo.InvariantCulture);
        var conversation = await _conversationRepository.GetConversationAsync(conversationId);

        if (conversation is null)
        {
            // We can't load if the conversation is gone.
            Log.ConversationNotFound(
                conversationId,
                viewContext.FromMember.Id,
                viewContext.FromMember.OrganizationId);

            // Update our view to a message
            viewContext.SetResponseAction(new UpdateResponseAction(AlertModal.Render(
                $":warning: Conversation could not be found, contact '{WebConstants.SupportEmail}' for assistance.",
                "Internal Error")));
            return;
        }

        var actor = viewContext.FromMember;
        var existingAssignee = conversation.Assignees.SingleOrDefault();
        var staticSelectMenu = state.GetAs<StaticSelectMenu>(BlockIds.AgentsInput, ActionIds.AgentsSelectMenu);
        var selectedOption = staticSelectMenu.SelectedOption;
        if (selectedOption is null)
        {
            await _conversationRepository.AssignConversationAsync(conversation, Array.Empty<Member>(), actor);

            if (existingAssignee is not null && existingAssignee.Id != actor.Id)
            {
                await SendDirectMessageAsync(
                    text: $"You have been unassigned from a conversation by {actor.DisplayName}.",
                    sectionText: $"You have been unassigned from a conversation by {actor.ToMention()}.",
                    conversation,
                    existingAssignee);
            }

            return;
        }

        var assigneeId = Id<Member>.Parse(selectedOption.Value, CultureInfo.InvariantCulture);
        var newAssignee = await _userRepository.GetMemberByIdAsync(assigneeId, conversation.Organization);
        if (newAssignee is null || !newAssignee.IsAgent())
        {
            // Update our view to a message
            viewContext.SetResponseAction(new UpdateResponseAction(AlertModal.Render(
                $":warning: User could not be found or is not an agent, contact '{WebConstants.SupportEmail}' for assistance.",
                "Internal Error")));
            return;
        }

        await _conversationRepository.AssignConversationAsync(conversation, new[] { newAssignee }, actor);

        if (newAssignee.Id == actor.Id || existingAssignee?.Id == newAssignee.Id)
        {
            // We don't send the DM to the actor if they assign themselves. We also don't send a DM if the
            // assignee was already assigned.
            return;
        }

        await SendDirectMessageAsync(
            text: $"You have been assigned to a conversation by {actor.DisplayName}.",
            sectionText: $"You have been assigned to a conversation by {actor.ToMention()}.",
            conversation,
            newAssignee);
    }

    async Task SendDirectMessageAsync(string text, string sectionText, Conversation conversation, Member newAssignee)
    {
        await _slackApiClient.SendDirectMessageAsync(
            conversation.Organization,
            newAssignee.User,
            text: text,
            new Section(new MrkdwnText(sectionText)),
            new Actions
            {
                Elements = new[]
                {
                    new ButtonElement
                    {
                        Text = new PlainText("Go to thread"),
                        Url = conversation.GetFirstMessageUrl()
                    }
                }
            });
    }

    record ViewState(Conversation Conversation, IEnumerable<Member> Agents, Member? Assignee, Member Actor);

    static class BlockIds
    {
        public const string AgentsInput = nameof(AgentsInput);
    }

    static class ActionIds
    {
        public const string AgentsSelectMenu = nameof(AgentsSelectMenu);
    }

    static Option ToOption(Member member)
    {
        return new Option(member.DisplayName, $"{member.Id}");
    }
}

static partial class AssignConversationModalLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Could not find conversation '{ConversationId}' in response to link request from member '{MemberId}' in organization '{OrganizationId}'.")]
    public static partial void ConversationNotFound(this ILogger<AssignConversationModal> logger,
        int conversationId, int memberId, int organizationId);
}
