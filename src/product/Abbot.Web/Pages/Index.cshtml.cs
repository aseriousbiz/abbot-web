using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Shared.Components.ConversationList;
using Serious.Abbot.Pages.Staff;
using Serious.Abbot.Repositories;
using Serious.AspNetCore;
using Serious.Collections;

namespace Serious.Abbot.Pages;

public class HomePageModel : UserPage
{
    public static readonly DomId ConversationsListFrameDomId = new DomId("conversations-list-frame");

    readonly IConversationRepository _conversationRepository;
    readonly IRoomRepository _roomRepository;
    readonly ITagRepository _tagRepository;
    readonly ISettingsManager _settingsManager;

    public IPaginatedList<Conversation> Conversations { get; set; } = null!;

    public ConversationStateFilter StateFilter { get; set; } = ConversationStateFilter.Open;

    public ConversationStats Stats { get; set; } = null!;

    public Room? Room { get; set; }

    public IReadOnlyList<RoomAssignment>? FirstResponders { get; set; }

    public IReadOnlyList<SelectListItem> Rooms { get; set; } = null!;

    public IReadOnlyCollection<SelectListItem> Tags { get; set; } = null!;

    public int RefreshIntervalMilliseconds { get; private set; }

    public HomePageModel(
        IConversationRepository conversationRepository,
        IRoomRepository roomRepository,
        ITagRepository tagRepository,
        ISettingsManager settingsManager)
    {
        _conversationRepository = conversationRepository;
        _roomRepository = roomRepository;
        _tagRepository = tagRepository;
        _settingsManager = settingsManager;
    }

    public async Task<IActionResult> OnGetAsync(
        ConversationStateFilter? state,
        string? room,
        [FromQuery] int? p,
        [FromQuery] string? tag = null)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        if (state is ConversationStateFilter.New)
        {
            // We no longer show the New filter.
            return RedirectToPage(new {
                state = ConversationStateFilter.Open
            });
        }

        var refreshInterval = await _settingsManager.GetAsync(SettingsScope.Global, SettingsModel.ConversationRefreshIntervalSecondsKey);
        const int defaultRefreshIntervalMilliseconds = 15000;
        if (refreshInterval is not null)
        {
            RefreshIntervalMilliseconds = int.TryParse(refreshInterval.Value, out var interval)
                ? interval * 1000
                : defaultRefreshIntervalMilliseconds;
        }
        else
        {
            RefreshIntervalMilliseconds = defaultRefreshIntervalMilliseconds;
        }

        var isListFrameRequest = Request.IsTurboFrameRequest(ConversationsListFrameDomId);

        StateFilter = state ?? ConversationStateFilter.Open;
        var page = p ?? 1;

        var query = new ConversationQuery(Organization)
            .WithState(StateFilter)
            .WithTag(tag);

        if (room is "my" or "following") // Keep "following" for back compat.
        {
            query = query.InRoomsWhereResponder(Viewer);
        }
        else if (room is not null)
        {
            if (!Id<Room>.TryParse(room, out var roomId))
            {
                return NotFound();
            }

            Room = await _roomRepository.GetRoomAsync(roomId);

            if (Room is null || Room.OrganizationId != Organization.Id)
            {
                return NotFound();
            }

            query = query.InRooms(Room.Id);
        }

        if (isListFrameRequest)
        {
            Conversations = await _conversationRepository.QueryConversationsAsync(
                query,
                DateTime.UtcNow,
                page,
                WebConstants.LongPageSize);

            return new PartialViewResult
            {
                ViewData = new ViewDataDictionary(MetadataProvider, ModelState) { Model = Conversations },
                ViewName = "_ConversationsList",
            };
        }

        if (Room is not null)
        {
            FirstResponders = Room.Assignments.Where(a => a.Role == RoomRole.FirstResponder).ToList();
        }

        (Conversations, Stats) = await _conversationRepository.QueryConversationsWithStatsAsync(
            query,
            DateTime.UtcNow,
            page,
            WebConstants.LongPageSize);

        Rooms = await GenerateRoomListAsync(state, room, tag);
        Tags = await GenerateTagsListAsync(state, room, tag);

        return Page();
    }

    public int GetStateCount(ConversationStateFilter conversationStateFilter) =>
        conversationStateFilter switch
        {
            ConversationStateFilter.Open => ConversationExtensions.OpenStates.Sum(s => Stats.CountByState.GetValueOrDefault(s)),
            ConversationStateFilter.NeedsResponse => ConversationExtensions.WaitingForResponseStates.Sum(s => Stats.CountByState.GetValueOrDefault(s)),
            ConversationStateFilter.Responded => Stats.CountByState.GetValueOrDefault(ConversationState.Waiting),
            ConversationStateFilter.Closed => Stats.CountByState.GetValueOrDefault(ConversationState.Closed),
            ConversationStateFilter.All => Stats.TotalCount,
            _ => throw new UnreachableException()
        };

    /// <summary>
    /// Handles form submission for editing tags.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        ConversationStateFilter? state,
        string? room,
        int? p,
        int conversationId,
        int[] tagIds,
        string? newTagName,
        bool createNewTag)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        var conversation = await _conversationRepository.GetConversationAsync(conversationId);
        if (conversation?.OrganizationId != Organization.Id)
        {
            return NotFound();
        }

        var existingTagIds = tagIds.Where(t => t != 0);
        if (createNewTag && newTagName is not null)
        {
            var newTags = await _tagRepository.EnsureTagsAsync(new[] { newTagName }, null, Viewer, Organization);
            existingTagIds = existingTagIds.Concat(newTags.Select(t => t.Id)).ToArray();
        }

        await _tagRepository.TagConversationAsync(conversation, existingTagIds, Viewer.User);
        StatusMessage = "Tags updated.";

        if (Request.IsTurboRequest())
        {
            return TurboUpdate(
                ConversationListViewComponent.TagsDomId(conversation),
                Partial("Shared/Components/ConversationList/_Tags",
                    new ConversationViewModel(conversation, null! /* ignored */, null)));
        }

        return RedirectToPage(new { state, room, p });
    }

    public async Task<IActionResult> OnPostBulkCloseAsync(
        ConversationStateFilter? state,
        string? room,
        int? p,
        Id<Conversation>[] conversationIds)
    {
        var newState = state is ConversationStateFilter.Closed
            ? ConversationState.Archived
            : ConversationState.Closed;

        var count = await _conversationRepository.BulkCloseOrArchiveConversationsAsync(
            conversationIds,
            newState,
            Organization,
            Viewer,
            "activity page");
        var finalState = state is ConversationStateFilter.Closed
            ? "archived"
            : "closed";

        StatusMessage = $"{count.ToQuantity("Conversation")} {finalState}";

        return RedirectToPage(new { state, room, p });
    }

    async Task<IReadOnlyList<SelectListItem>> GenerateRoomListAsync(
        ConversationStateFilter? state,
        string? room,
        string? tag)
    {
        var list = (await _roomRepository.GetConversationRoomsAsync(Organization, default, 1, Int32.MaxValue))
            .Select(r => new SelectListItem(
                r.Name,
                Url.Page("/Index", new {
                    state,
                    room = r.Id,
                    tag,
                }),
                Room is not null && r.Id == Room.Id))
            .ToList();

        list.Insert(0, new SelectListItem("──────────", "", false, true));
        list.Insert(0, new SelectListItem(
            "All Rooms",
            Url.Page("/Index", new { state, tag, room = (string?)null }),
            room is null));
        list.Insert(0, new SelectListItem(
            "My Rooms",
            Url.Page("/Index", new { state, tag, room = "my" }),
            room is "my"));

        return list;
    }

    async Task<IReadOnlyCollection<SelectListItem>> GenerateTagsListAsync(
        ConversationStateFilter? state,
        string? room,
        string? tag)
    {
        var tagOptions = new List<SelectListItem>
        {
            new("No Filter", Url.Page("/Index", new { state, room, tag = (string?)null }), selected: tag is null or ""),
            new("Untagged", Url.Page("/Index", new { state, room, tag = "`untagged`" }), selected: tag is "`untagged`"),
            new("──────────", "", false, true),
        };

        var tags = await _tagRepository.GetAllAsync(Organization);
        tagOptions.AddRange(tags.Select(t => new SelectListItem(
            t.Name,
            Url.Page("/Index", new {
                state,
                room,
                tag = t.Name,
            }),
            selected: tag is not null && tag == t.Name)));
        return tagOptions;
    }
}
