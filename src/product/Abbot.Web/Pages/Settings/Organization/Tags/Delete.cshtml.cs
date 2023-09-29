using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Tags;

public class DeletePage : UserPage
{
    readonly ITagRepository _tagRepository;

    public DeletePage(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public Tag Tag { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Id<Tag> id)
    {
        var tag = await InitializeState(id);

        if (tag is null || tag.Id == Viewer.Id)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Id<Tag> id)
    {
        var tag = await InitializeState(id);

        if (tag is null || tag.Id == Viewer.Id)
        {
            return NotFound();
        }

        await _tagRepository.RemoveAsync(tag, Viewer.User);
        StatusMessage = $"{tag.Name} tag deleted.";

        return RedirectToPage("Index");
    }

    async Task<Tag?> InitializeState(Id<Tag> id)
    {
        var tag = await _tagRepository.GetByIdAsync(id, Organization);
        if (tag is null)
        {
            return null;
        }

        Tag = tag;
        return tag;
    }
}
