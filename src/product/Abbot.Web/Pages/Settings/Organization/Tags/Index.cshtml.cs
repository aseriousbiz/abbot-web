using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Collections;

namespace Serious.Abbot.Pages.Settings.Organization.Tags;

public class IndexPage : UserPage
{
    readonly ITagRepository _tagRepository;

    public IndexPage(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public IPaginatedList<CustomerTag> CustomerTags { get; private set; } = null!;

    public IPaginatedList<Tag> ConversationTags { get; private set; } = null!;

    public async Task OnGetAsync(int p = 1)
    {
        await InitializePage(p);
    }

    async Task InitializePage(int pageNumber)
    {
        ConversationTags = await _tagRepository.GetAllUserTagsAsync(
            Organization,
            pageNumber,
            WebConstants.LongPageSize);
    }
}
