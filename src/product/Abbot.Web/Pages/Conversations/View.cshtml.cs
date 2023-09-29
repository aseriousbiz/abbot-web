using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Conversations;

public class ViewPage : UserPage
{
    readonly IConversationRepository _conversationRepository;
    readonly IMessageRenderer _messageRenderer;

    public ViewPage(IConversationRepository conversationRepository, IMessageRenderer messageRenderer)
    {
        _conversationRepository = conversationRepository;
        _messageRenderer = messageRenderer;
    }

    public Conversation Conversation { get; set; } = null!;

    public RenderedMessage Title { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int conversationId)
    {
        if (await InitializeAsync(conversationId) is { } notFoundResult)
        {
            return notFoundResult;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostStateAsync(int conversationId, StateChangeAction action)
    {
        if (await InitializeAsync(conversationId) is { } result)
        {
            return result;
        }

        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "Your plan doesn't include conversation tracking.";
            return RedirectToReturnUrl() ?? RedirectToPage();
        }

        switch (action)
        {
            case StateChangeAction.Close:
                if (Conversation.State.IsOpen())
                {
                    await _conversationRepository.CloseAsync(Conversation, Viewer, DateTime.UtcNow, "conversation details page");
                    StatusMessage = "Conversation closed";
                }
                else
                {
                    StatusMessage = $"{WebConstants.ErrorStatusPrefix}Cannot close a conversation in the {Conversation.State} state.";
                }

                break;
            case StateChangeAction.Reopen:
                if (Conversation.State is ConversationState.Closed)
                {
                    await _conversationRepository.ReopenAsync(Conversation, Viewer, DateTime.UtcNow);
                    StatusMessage = "Conversation re-opened";
                }
                else
                {
                    StatusMessage = $"{WebConstants.ErrorStatusPrefix}Cannot re-open a conversation in the {Conversation.State} state.";
                }

                break;
            case StateChangeAction.Archive:
                if (Conversation.State is ConversationState.Closed)
                {
                    await _conversationRepository.ArchiveAsync(Conversation, Viewer, DateTime.UtcNow);
                    StatusMessage = "Conversation archived";
                }
                else
                {
                    StatusMessage = $"{WebConstants.ErrorStatusPrefix}Cannot archive a conversation in the {Conversation.State} state.";
                }

                break;
            case StateChangeAction.Unarchive:
                if (Conversation.State is ConversationState.Archived)
                {
                    await _conversationRepository.UnarchiveAsync(Conversation, Viewer, DateTime.UtcNow);
                    StatusMessage = "Conversation restored to 'Closed' state";
                }
                else
                {
                    StatusMessage = $"{WebConstants.ErrorStatusPrefix}Cannot unarchive a conversation in the {Conversation.State} state.";
                }

                break;
            case StateChangeAction.Snooze:
                await _conversationRepository.SnoozeConversationAsync(Conversation, Viewer, DateTime.UtcNow);
                StatusMessage = "Conversation snoozed";

                break;
            case StateChangeAction.Unknown:
            default:
                // Unknown action
                StatusMessage = "Unknown action";
                break;
        }

        return RedirectToReturnUrl() ?? RedirectToPage();
    }

    async Task<IActionResult?> InitializeAsync(int conversationId)
    {
        var convo = await _conversationRepository.GetConversationAsync(conversationId);

        // Catch those sneaky URL hackers
        if (convo is null || convo.OrganizationId != Organization.Id || convo.State is ConversationState.Hidden)
        {
            return NotFound();
        }

        // Load the timeline too
        // We don't need to check the return value, it should have been set on `convo.Events`.
        await _conversationRepository.GetTimelineAsync(convo);

        Conversation = convo;
        Title = await _messageRenderer.RenderMessageAsync(Conversation.Title, Organization);
        return null;
    }

    public enum StateChangeAction
    {
        Unknown = 0,
        Close,
        Reopen,
        Archive,
        Unarchive,
        Snooze,
    }
}
