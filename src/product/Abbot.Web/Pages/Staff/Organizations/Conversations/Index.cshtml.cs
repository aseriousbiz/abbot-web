using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.AspNetCore;

namespace Serious.Abbot.Pages.Staff.Organizations.Conversations;

public class IndexModel : OrganizationDetailPage
{
    public static readonly DomId ConversationDetailId = new("conversation-detail");
    readonly IConversationRepository _conversationRepository;
    readonly ISettingsManager _settingsManager;

    public IndexModel(AbbotContext db, IAuditLog auditLog, IConversationRepository conversationRepository, ISettingsManager settingsManager) : base(db, auditLog)
    {
        _conversationRepository = conversationRepository;
        _settingsManager = settingsManager;
    }

    [Display(Name = "Conversation ID or URL")]
    [BindProperty]
    public string? ConversationId { get; set; }

    [Display(Name = "Message ID or URL")]
    [BindProperty]
    public string? MessageId { get; set; }

    [BindProperty]
    public string? LinkType { get; set; }

    [BindProperty]
    public string? LinkedId { get; set; }

    [BindProperty]
    public string? Reason { get; set; }

    public bool AllowAIEnhancements { get; private set; }

    public bool AllowReactionResponses { get; private set; }

    public bool AllowTicketReactions { get; private set; }

    public ConversationInfoViewModel? Conversation { get; set; }

    public IReadOnlyList<SelectListItem> AllLinkTypes { get; } = GetLinkTypes().ToList();

    public IReadOnlyList<Conversation> RecentConversations { get; private set; } = null!;

    public async Task OnGet(string id)
    {
        await InitializeDataAsync(id);
    }

    public async Task<IActionResult> OnPostFindByConvoIdAsync(string id)
    {
        await InitializeDataAsync(id);

        if (ConversationId is not { Length: > 0 })
        {
            ModelState.AddModelError(nameof(ConversationId), "The conversation ID is required.");
            return Page();
        }

        // Try to parse the ID as a URL
        if (!int.TryParse(ConversationId, out var convoId))
        {
            ModelState.AddModelError(nameof(MessageId), "The conversation ID is invalid");
            return Page();
        }

        // Find the conversation
        var conversation = await GetConversationQueryable()
            .Where(c => c.OrganizationId == Organization.Id && c.Id == convoId)
            .SingleOrDefaultAsync();

        if (conversation == null)
        {
            ModelState.AddModelError(nameof(MessageId), "No conversation found with that ID");
            return Page();
        }

        return await RenderConversationDetailAsync(conversation);
    }

    public async Task<IActionResult> OnPostFindByMessageAsync(string id)
    {
        await InitializeDataAsync(id);

        if (MessageId is not { Length: > 0 })
        {
            ModelState.AddModelError(nameof(MessageId), "The message ID is required.");
            return Page();
        }

        // Try to parse the ID as a URL
        if (SlackUrl.TryParse(MessageId, out var url))
        {
            if (url is SlackMessageUrl { ThreadTimestamp: var threadTs, Timestamp: var ts })
            {
                MessageId = threadTs ?? ts;
            }
            else
            {
                ModelState.AddModelError(nameof(MessageId), "The URL is not a message URL");
                return Page();
            }
        }

        // Find the conversation
        var conversation = await GetConversationQueryable()
            .Where(c => c.OrganizationId == Organization.Id && c.FirstMessageId == MessageId)
            .SingleOrDefaultAsync();

        if (conversation == null)
        {
            ModelState.AddModelError(nameof(MessageId), "No conversation found with that message ID");
            return Page();
        }

        return await RenderConversationDetailAsync(conversation);
    }

    public async Task<IActionResult> OnPostFindByLinkAsync(string id)
    {
        await InitializeDataAsync(id);

        if (LinkType is not { Length: > 0 })
        {
            ModelState.AddModelError(nameof(LinkType), "The link type is required.");
            return Page();
        }

        if (!Enum.TryParse<ConversationLinkType>(LinkType, true, out var linkType))
        {
            ModelState.AddModelError(nameof(LinkType), "The link type is invalid.");
            return Page();
        }

        if (LinkedId is not { Length: > 0 })
        {
            ModelState.AddModelError(nameof(LinkedId), "The linked ID is required.");
            return Page();
        }

        // Find the conversation
        var conversations = await GetConversationQueryable()
            .Where(c =>
                c.OrganizationId == Organization.Id &&
#pragma warning disable CA1307
                c.Links.Any(l => l.LinkType == linkType && l.ExternalId.Contains(LinkedId)))
#pragma warning restore CA1307
            .Take(2)
            .ToListAsync();

        if (conversations is { Count: 0 })
        {
            ModelState.AddModelError(nameof(LinkedId), "No conversation found with that external ID");
            return Page();
        }
        if (conversations is { Count: > 1 })
        {
            ModelState.AddModelError(nameof(LinkedId), "Multiple conversations found with that external ID");
            return Page();
        }

        return await RenderConversationDetailAsync(conversations[0]);
    }

    protected override async Task InitializeDataAsync(Organization organization)
    {
        AllowAIEnhancements = organization.Settings.AIEnhancementsEnabled is true;
        AllowReactionResponses = await ReactionHandler.GetAllowReactionResponsesSetting(
            _settingsManager,
            organization);
        AllowTicketReactions = await ReactionHandler.GetAllowTicketReactionSetting(
            _settingsManager,
            organization);

        var query = new ConversationQuery(organization);
        RecentConversations = await _conversationRepository.QueryConversationsAsync(query, DateTime.UtcNow, 1, 10);
    }

    IQueryable<Conversation> GetConversationQueryable()
    {
        return Db.Conversations
            .Include(c => c.Events)
            .ThenInclude(e => ((AttachedToHubEvent)e).Hub)
            .Include(c => c.Links)
            .Include(c => c.StartedBy.Organization)
            .Include(c => c.StartedBy.User)
            .Include(c => c.Members)
            .ThenInclude(m => m.Member.User)
            .Include(c => c.Members)
            .ThenInclude(m => m.Member.Organization)
            .Include(c => c.Room.Organization)
            .Include(c => c.Room.Assignments)
            .Include(c => c.Hub!.Room);
    }

    static SettingsScope Scope(Conversation conversation) => SettingsScope.Conversation(conversation);

    async Task<IActionResult> RenderConversationDetailAsync(Conversation conversation)
    {
        var settings = await _settingsManager.GetAllAsync(Scope(conversation));
        Conversation = new(conversation, settings);

        // Log this for staff-only
        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Conversation", "Viewed"),
                Description = $"Viewed conversation {conversation.Id}",
                Actor = Viewer,
                Organization = Organization,
                StaffPerformed = true,
                StaffOnly = true,
                StaffReason = Reason ?? "No reason given",
            });

        return Request.IsTurboRequest()
            ? TurboUpdate(ConversationDetailId, Partial("_ConversationDetail", Conversation))
            : Page();
    }

    public async Task<IActionResult> OnPostSettingDeleteAsync(int id, string name)
    {
        var conversation = await _conversationRepository.GetConversationAsync(id);
        if (conversation is null)
        {
            return NotFound("Conversation not found");
        }

        var scope = Scope(conversation);
        if (await _settingsManager.GetAsync(scope, name) is not { })
        {
            return TurboFlash($"Setting '{name}' not found.");
        }

        await _settingsManager.RemoveWithAuditingAsync(scope, name, Viewer.User, Organization);

        var settings = await _settingsManager.GetAllAsync(scope);
        Conversation = new(conversation, settings);
        return Request.IsTurboRequest()
            ? TurboUpdate(ConversationDetailId, Partial("_ConversationDetail", Conversation))
            : Page();
    }

    static IEnumerable<SelectListItem> GetLinkTypes()
    {
        yield return new SelectListItem(string.Empty, string.Empty);

        foreach (var name in Enum.GetNames<ConversationLinkType>())
        {
            yield return new SelectListItem(name, name);
        }
    }

    public record ConversationInfoViewModel(
        Conversation Conversation,
        IReadOnlyList<Setting> Settings);
}
