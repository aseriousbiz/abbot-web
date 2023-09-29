using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Collections;

namespace Serious.Abbot.Pages.Shared.Components.ConversationList;

public class ConversationListViewComponent : ViewComponent
{
    public static DomId TagsDomId(Id<Conversation> id) =>
        new DomId("conversation-tags").WithSuffix(id.ToString());

    readonly IMessageRenderer _messageRenderer;

    public ConversationListViewComponent(IMessageRenderer messageRenderer)
    {
        _messageRenderer = messageRenderer;
    }

    public async Task<IViewComponentResult> InvokeAsync(
        IPaginatedList<Conversation> conversations,
        bool showPager,
        bool showViewAllLink)
    {
        var organization = HttpContext.RequireCurrentOrganization();
        var aiEnabled = organization.Settings.AIEnhancementsEnabled is true;

        var models = new List<ConversationViewModel>();
        foreach (var convo in conversations)
        {
            var title = await _messageRenderer.RenderMessageAsync(convo.Title, convo.Organization);
            var summary = await _messageRenderer.RenderMessageAsync(convo.Summary, convo.Organization);
            models.Add(new ConversationViewModel(convo, title, summary));
        }

        var list = new PaginatedList<ConversationViewModel>(
            models,
            conversations.TotalCount,
            conversations.PageNumber,
            conversations.PageSize);

        return View(new ViewModel(
            organization,
            list,
            showPager,
            showViewAllLink,
            aiEnabled));
    }

    public record ViewModel(
        Organization Organization,
        IPaginatedList<ConversationViewModel> Conversations,
        bool ShowPager,
        bool ShowViewAllLink,
        bool AIEnhancementsEnabled);
}
