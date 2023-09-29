using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Collections;

namespace Serious.Abbot.Pages.Lists;

public class IndexPage : UserPage
{
    readonly IListRepository _userListRepository;

    public IndexPage(IListRepository userListRepository)
    {
        _userListRepository = userListRepository;
    }

    public IPaginatedList<UserList> Lists { get; private set; } = null!;
    public string? Filter { get; private set; }

    public async Task OnGetAsync(int? p, string? filter)
    {
        int page = p ?? 1;
        await InitializePageAsync(page, WebConstants.LongPageSize, filter);
    }

    async Task InitializePageAsync(int pageNumber, int pageSize, string? filter)
    {
        Filter = filter;
        var queryable = _userListRepository.GetQueryable(Organization);
        Lists = await PaginatedList.CreateAsync(queryable, pageNumber, pageSize);
    }
}
