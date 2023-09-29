using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Api;
using Serious.Abbot.Entities;
using Serious.Abbot.Entities.Filters;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Shared.Filters;
using Serious.Abbot.Repositories;
using Serious.Filters;

namespace Serious.Abbot.Pages.Insights;

public class IndexPage : UserPage
{
    readonly IUserRepository _userRepository;
    readonly IRoomRepository _roomRepository;
    readonly CustomerRepository _customerRepository;
    readonly IInsightsRepository _insightsRepository;
    readonly ITagRepository _tagRepository;

    public InsightsSummaryInfo? Summary { get; private set; }

    [BindProperty(Name = "q", SupportsGet = true)]
    public FilterList Filter { get; set; }

    public CompositeFilterModel CustomerFilterModel { get; private set; } = null!;

    public IReadOnlyList<SelectListItem> FilterOptions { get; private set; } = null!;

    public IReadOnlyList<SelectListItem> TagOptions { get; private set; } = null!;

    public RoomCountsResult RoomCounts { get; private set; } = null!;

    public CustomerCounts CustomerCounts { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = null!;

    public IndexPage(
        IUserRepository userRepository,
        IRoomRepository roomRepository,
        CustomerRepository customerRepository,
        IInsightsRepository insightsRepository,
        ITagRepository tagRepository)
    {
        _userRepository = userRepository;
        _roomRepository = roomRepository;
        _customerRepository = customerRepository;
        _insightsRepository = insightsRepository;
        _tagRepository = tagRepository;
    }

    public async Task<IActionResult> OnGetAsync(string? filter = null, DateRangeOption? range = null, string? tag = null)
    {
        var currentMember = Viewer;
        var organization = currentMember.Organization;

        var allCustomers = await _customerRepository.GetAllAsync(Organization);
        var segments = await _customerRepository.GetAllCustomerSegmentsAsync(Organization);
        CustomerFilterModel = AbbotFilterModelHelpers.CreateCustomerFilterModel(
            allCustomers,
            segments,
            Filter,
            showValueInLabel: true);

        Input = new InputModel(
            filter ?? (currentMember.IsAdministrator() ? InsightsRoomFilter.All : InsightsRoomFilter.Yours), // Default filter for admins is all, for others it's theirs (See #2201)
            range ?? DateRangeOption.Week,
            tag ?? TagSelector.AllTagSelectorToken);

        var rooms = currentMember.IsAdministrator()
            ? await _insightsRepository.GetRoomFilterList(organization)
            : Array.Empty<RoomOption>();

        var members = await _userRepository.GetActiveMembersQueryable(organization)
            .ToListAsync();

        FilterOptions = CreateFilterOptions(currentMember, rooms, members).ToReadOnlyList();
        var userTagGroup = new SelectListGroup
        {
            Name = "Custom tags"
        };
        var aiTagGroup = new SelectListGroup
        {
            Name = "AI Generated tags"
        };
        TagOptions = (await _tagRepository.GetAllVisibleTagsAsync(Organization))
            .Select(t => new SelectListItem(t.Name, t.Name, Input.SelectedTagFilter == t.Name)
            {
                Group = t.Generated ? aiTagGroup : userTagGroup
            })
            .Prepend(new SelectListItem("All Tags", TagSelector.AllTagSelectorToken))
            .ToReadOnlyList();

        RoomCounts = await _roomRepository.GetPersistentRoomCountsAsync(Organization, default);
        CustomerCounts = await _customerRepository.GetCustomerCountsAsync(Organization);
        return Page();
    }

    public IEnumerable<SelectListItem> CreateFilterOptions(
        Member currentMember,
        IEnumerable<RoomOption> rooms,
        IEnumerable<Member> members)
    {
        var roomsGroup = new SelectListGroup
        {
            Name = "Rooms"
        };

        var agentsGroup = new SelectListGroup
        {
            Name = "Agents"
        };

        if (currentMember.IsAdministrator())
        {
            yield return new SelectListItem("All Rooms", InsightsRoomFilter.All, Input.SelectedFilter == InsightsRoomFilter.All);
        }

        yield return new SelectListItem("Your Rooms", InsightsRoomFilter.Yours, Input.SelectedFilter == InsightsRoomFilter.Yours);

        if (currentMember.IsAdministrator())
        {
            foreach (var member in members)
            {
                if (member.IsAgent())
                {
                    var value = member.User.PlatformUserId;
                    yield return new SelectListItem(member.DisplayName, value, Input.SelectedFilter == value)
                    {
                        Group = agentsGroup
                    };
                }
            }

            foreach ((string? name, string? value) in rooms)
            {
                yield return new SelectListItem($"#{name}", value, Input.SelectedFilter == value)
                {
                    Group = roomsGroup
                };
            }
        }
    }

    public bool ShowResponders => Viewer.IsAdministrator()
        && Input.SelectedFilter switch
        {
            InsightsRoomFilter.All => true,
            InsightsRoomFilter.Yours => false,
            { } filter when InsightsRoomFilter.GetAddressType(filter) is not ChatAddressType.User => true,
            _ => false
        };

    public bool ShowRooms =>
        Input.SelectedFilter switch
        {
            InsightsRoomFilter.All => true,
            InsightsRoomFilter.Yours => true,
            { } filter when InsightsRoomFilter.GetAddressType(filter) is not ChatAddressType.Room => true,
            _ => false
        };

    public record InputModel(string? SelectedFilter, DateRangeOption SelectedRange, string? SelectedTagFilter);
}
