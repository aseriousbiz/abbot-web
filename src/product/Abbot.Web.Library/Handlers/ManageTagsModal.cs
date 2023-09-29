using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

public class ManageTagsModal : IHandler
{
    readonly ITagRepository _tagRepository;
    readonly IServiceProvider _serviceProvider;

    public ManageTagsModal(ITagRepository tagRepository, IServiceProvider serviceProvider)
    {
        _tagRepository = tagRepository;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a <see cref="ManageTagsModal"/>.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to create it.</param>
    /// <param name="conversationId">If created from the <see cref="TagConversationModal"/>, this is the Id of the current <see cref="Conversation"/> being tagged.</param>
    /// <param name="organization">The organization.</param>
    public static async Task<ViewUpdatePayload?> CreateAsync(
        IServiceProvider serviceProvider,
        int conversationId,
        Organization organization)
    {
        var modal = serviceProvider.Activate<ManageTagsModal>();
        return await modal.CreateAsync(conversationId, organization);
    }

    async Task<ViewUpdatePayload?> CreateAsync(int conversationId, Organization organization)
    {
        var viewState = await GatherViewState(organization, conversationId);
        return Render(viewState);
    }

    async Task<ViewState> GatherViewState(Organization organization, int conversationId)
    {
        return await GatherViewState(organization, new PrivateMetadata(conversationId).ToString());
    }

    async Task<ViewState> GatherViewState(Organization organization, string? privateMetadata)
    {
        var allTags = await _tagRepository.GetAllUserTagsAsync(organization);
        return new ViewState(allTags, privateMetadata);
    }

    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        bool push = true;
        if (viewContext.Payload.Actions is [ButtonElement { ActionId: "Delete", Value: { Length: > 0 } } deleteButton])
        {
            var tagId = int.Parse(deleteButton.Value, CultureInfo.InvariantCulture);
            var tag = await _tagRepository.GetByIdAsync(tagId, viewContext.Organization);
            if (tag is not null)
            {
                await _tagRepository.RemoveAsync(tag, viewContext.From);
                if (viewContext.Payload.View.PreviousViewId is not null)
                {
                    // Update parent modal with the updated set of tags.
                    await UpdateParentModalAsync(viewContext);
                }
            }

            push = false;
        }

        var conversationId = PrivateMetadata.FromViewContext(viewContext.Payload).Require().ConversationId;
        var viewState = await GatherViewState(viewContext.Organization, conversationId);
        var viewUpdatePayload = Render(viewState);

        Func<ViewUpdatePayload, Task<ViewResponse>> modalAction = push
            ? viewContext.PushModalViewAsync
            : viewContext.UpdateModalViewAsync;

        await modalAction(viewUpdatePayload);
    }

    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        var state = viewContext.Payload.View.State.Require();
        var submissionState = state.BindRecord<SubmissionState>();

        if (submissionState.TagName is not { Length: > 0 } tagName)
        {
            viewContext.ReportValidationErrors(
                blockId: nameof(SubmissionState.TagName),
                errorMessage: "Tag Name cannot be empty.");

            return;
        }

        if (!Tag.IsValidTagName(tagName))
        {
            viewContext.ReportValidationErrors(
                blockId: nameof(SubmissionState.TagName),
                errorMessage: Skill.NameErrorMessage);

            return;
        }

        var tag = new Tag
        {
            Name = tagName,
            Description = submissionState.TagDescription,
            OrganizationId = viewContext.Organization.Id,
        };

        try
        {
            await _tagRepository.CreateAsync(tag, viewContext.From);
        }
        catch (DbUpdateException e) when (e.GetDatabaseError() is UniqueConstraintError { ColumnNames: [nameof(Tag.OrganizationId), nameof(Tag.Name)] })
        {
            viewContext.ReportValidationErrors(
                blockId: nameof(SubmissionState.TagName),
                errorMessage: "There is already a tag with that name.");
        }

        // Update parent modal (if any) with the updated set of tags.
        await UpdateParentModalAsync(viewContext);
    }

    public async Task OnClosedAsync(IViewContext<IViewClosedPayload> viewContext)
    {
        // Update parent modal (if any) with the updated set of tags.
        await UpdateParentModalAsync(viewContext);
    }

    async Task UpdateParentModalAsync<TViewPayload>(IViewContext<TViewPayload> viewContext)
        where TViewPayload : IViewPayload
    {
        if (viewContext.Payload.View.PreviousViewId is not null)
        {
            var conversationId = PrivateMetadata.FromViewContext(viewContext.Payload).Require().ConversationId;
            await TagConversationModal.UpdateAsync(
                _serviceProvider,
                viewContext,
                conversationId);
        }
    }

    static ViewUpdatePayload Render(ViewState viewState)
    {
        var blocks = new List<ILayoutBlock>();
        blocks.AddRange(viewState.Tags.Select(ToSection));
        blocks.AddRange(new ILayoutBlock[] {
            new Divider(),
            new Header("New Tag"),
            new Input
            {
                Label = "Name",
                Element = new PlainTextInput
                {
                    ActionId = nameof(SubmissionState.TagName),
                    Placeholder = "Tag Name",
                },
                BlockId = nameof(SubmissionState.TagName),
                Optional = false,
            },
            new Input
            {
                Label = "Description",
                Element = new PlainTextInput
                {
                    ActionId = nameof(SubmissionState.TagDescription),
                    Placeholder = "Additional context about the tag.",
                },
                BlockId = nameof(SubmissionState.TagDescription),
                Optional = true,
            }
        });

        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<ManageTagsModal>(),
            Title = "Manage Tags",
            Submit = "Create",
            Close = "Close",
            Blocks = blocks,
            PrivateMetadata = viewState.PrivateMetadata,
            NotifyOnClose = true,
        };

        return payload;
    }

    static Section ToSection(Tag tag)
    {
        return new Section(new MrkdwnText($":label: {tag.Name}\n{tag.Description}"))
        {
            Accessory = new ButtonElement("Delete")
            {
                ActionId = "Delete",
                Value = $"{tag.Id}"
            }
        };
    }

    /// <summary>
    /// The state of the view.
    /// </summary>
    /// <param name="Tags">The set of all tags for this organization.</param>
    /// <param name="PrivateMetadata">The private metadata to pass on.</param>
    record ViewState(IEnumerable<Tag> Tags, string? PrivateMetadata);

    public record SubmissionState(string? TagName, string? TagDescription);

    // This might seem overkill for one field, but it used to be more than one, and it might be more in the future.
    record PrivateMetadata(int ConversationId)
    {
        public static PrivateMetadata? FromViewContext(IViewPayload viewPayload)
        {
            return viewPayload.View.PrivateMetadata is { Length: > 0 } privateMetadata
                ? Parse(privateMetadata)
                : null;
        }

        static PrivateMetadata? Parse(string privateMetadata)
        {
            if (int.TryParse(privateMetadata, out var conversationId))
            {
                return new PrivateMetadata(conversationId);
            }
            return null;
        }

        public override string ToString()
        {
            return $"{ConversationId}";
        }
    }
}
