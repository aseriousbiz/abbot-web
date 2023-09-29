using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

public class TagConversationModal : IHandler
{
    static readonly ILogger<TagConversationModal> Log =
        ApplicationLoggerFactory.CreateLogger<TagConversationModal>();

    readonly ITagRepository _tagRepository;
    readonly IConversationRepository _conversationRepository;
    readonly IServiceProvider _serviceProvider;

    public TagConversationModal(
        ITagRepository tagRepository,
        IConversationRepository conversationRepository,
        IServiceProvider serviceProvider)
    {
        _tagRepository = tagRepository;
        _conversationRepository = conversationRepository;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Updates a <see cref="TagConversationModal"/> from a child modal.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to create it.</param>
    /// <param name="viewContext">The view context from the child modal.</param>
    /// <param name="conversationId">The Id of the conversation we're tagging.</param>
    public static async Task UpdateAsync<TViewPayload>(
        IServiceProvider serviceProvider,
        IViewContext<TViewPayload> viewContext,
        int conversationId) where TViewPayload : IViewPayload
    {
        var modal = serviceProvider.Activate<TagConversationModal>();
        await modal.UpdateFromChildModalAsync(viewContext, conversationId);
    }

    async Task UpdateFromChildModalAsync<TViewPayload>(IViewContext<TViewPayload> viewContext, int conversationId)
        where TViewPayload : IViewPayload
    {
        var viewState = await GatherViewState(conversationId);
        var modalUpdatePayload = Render(viewState);
        await viewContext.UpdateParentModalViewAsync(modalUpdatePayload);
    }

    /// <summary>
    /// Handles the interaction to display the modal. The interaction is either with the "Manage Tags" button in this
    /// modal or the "Tag Conversation" button in the Manage Conversations modal.
    /// </summary>
    /// <param name="viewContext"></param>
    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        var actions = viewContext.Payload.Actions;
        var action = actions.SingleOrDefault();
        var button = action as ButtonElement;

        if (button is { Value: { Length: > 0 } value })
        {
            var conversationId = int.Parse(value, CultureInfo.InvariantCulture);
            if (button.ActionId is ActionIds.ManageTags)
            {
                Log.OpeningManageTags(conversationId);

                // User is clicking the Manage Tags button so we need to push the Manage Tags modal on the view stack.
                var manageTagModal = await ManageTagsModal.CreateAsync(
                    _serviceProvider,
                    conversationId,
                    viewContext.Organization)
                    .Require();
                await viewContext.PushModalViewAsync(manageTagModal);
            }
            else
            {
                Log.OpeningTagConversationModal(conversationId);

                // The user clicked on the "Tag Conversation" button in the Manage Conversation Modal.
                var viewState = await GatherViewState(conversationId);
                var viewUpdatePayload = Render(viewState);
                await viewContext.PushModalViewAsync(viewUpdatePayload);
            }
        }
        else
        {
            var actionTypes = string.Join(',', actions.Select(a => a.GetType().Name));
            var buttonValue = button?.Value;

            Log.UnexpectedPayloadActions(actions.Count, actionTypes, buttonValue);
        }
    }

    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        var conversationId = int.Parse(viewContext.Payload.View.PrivateMetadata.Require(), CultureInfo.InvariantCulture);
        var conversation = await _conversationRepository.GetConversationAsync(conversationId).Require();
        var submission = viewContext.Payload.View.State?.BindRecord<SubmissionState>();
        var tagIds = submission?.Tags.Select(int.Parse).ToArray() ?? Array.Empty<int>();
        // We don't allow users to manually modify generated tags, so we need to add them back in.
        var generatedTagIds = conversation.Tags.Select(t => t.Tag).Where(t => t.Generated).Select(t => t.Id);
        await _tagRepository.TagConversationAsync(conversation, tagIds.Concat(generatedTagIds), viewContext.From);
    }

    async Task<ViewState> GatherViewState(int conversationId)
    {
        var conversation = await _conversationRepository.GetConversationAsync(conversationId).Require();
        var allTags = await _tagRepository.GetAllUserTagsAsync(conversation.Organization);

        var conversationTags = conversation.Tags.Select(t => t.Tag).Where(t => !t.Generated);
        return new ViewState(conversation, conversationTags, allTags);
    }

    static ViewUpdatePayload Render(ViewState viewState)
    {
        var (conversation, conversationTags, allTags) = viewState;
        conversationTags = conversationTags.OrderBy(t => t.Name);
        var allOptions = allTags.Select(ToOption).ToList();

        ILayoutBlock tagSelectionBlock = allOptions.Any()
            ? new Input
            {
                Label = new PlainText("Tags"),
                Element = new MultiStaticSelectMenu
                {
                    Placeholder = new PlainText("Add Tags"),
                    InitialOptions = conversationTags.Select(ToOption).ToList(),
                    Options = allOptions,
                    ActionId = ActionIds.AddTags,
                },
                BlockId = nameof(SubmissionState.Tags),
                Optional = true,
            }
            : new Section("This organization has no tags. Click \"Manage Tags\" to create some tags.");

        var blocks = new List<ILayoutBlock>
        {
            new Section
            {
                Text = new MrkdwnText(conversation.Title)
            },
            new Divider(),
            tagSelectionBlock,
            new Actions(new ButtonElement("Manage Tags")
            {
                ActionId = ActionIds.ManageTags,
                Value = $"{conversation.Id}"
            })
        };

        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<TagConversationModal>(),
            Title = "Tag Conversation",
            Submit = "Save Tags",
            Close = "Close",
            Blocks = blocks,
            PrivateMetadata = $"{conversation.Id}",
        };

        return payload;
    }

    static Option ToOption(Tag tag)
    {
        return new Option(tag.Name, $"{tag.Id}");
    }

    record ViewState(
        Conversation Conversation,
        IEnumerable<Tag> ConversationTags,
        IEnumerable<Tag> AllTags);

    public record SubmissionState(IReadOnlyList<string> Tags);

    static class ActionIds
    {
        public const string ManageTags = "manage-tags";
        public const string AddTags = "add-tags";
    }
}

static partial class TagConversationModalLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "User opening Tag Conversation modal for conversation {ConversationId}")]
    public static partial void OpeningTagConversationModal(
        this ILogger<TagConversationModal> logger,
        int conversationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Manage Tags while tagging {ConversationID}")]
    public static partial void OpeningManageTags(
        this ILogger<TagConversationModal> logger,
        int conversationId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Unexpected Payload Actions. {ActionsCount} actions of types {ActionsTypes} with button value {ButtonValue}")]
    public static partial void UnexpectedPayloadActions(
        this ILogger<TagConversationModal> logger,
        int actionsCount,
        string actionsTypes,
        string? buttonValue);
}
