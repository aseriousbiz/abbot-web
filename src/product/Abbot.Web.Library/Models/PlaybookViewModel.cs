using System.Collections.Generic;
using Serious.Abbot.Playbooks;

namespace Serious.Abbot.Entities;

public enum PlaybookPublishState
{
    NeverPublished,
    Published,
    PublishedWithUnpublishedChanges,
}

public record PlaybookListViewModel(IReadOnlyList<PlaybookViewModel> Playbooks, bool FilterApplied);

public record PlaybookViewModel(
    int Id,
    string Name,
    string? Description,
    string Slug,
    bool Enabled,
    PlaybookVersion? CurrentVersion,
    PlaybookVersion? PublishedVersion,
    PlaybookPublishState PublishState,
    Organization Organization,
    PlaybookRunGroupSummary? LastRunGroup)
{
    public static PlaybookViewModel Create(
        int id,
        string name,
        string? description,
        string slug,
        bool enabled,
        PlaybookVersion? currentVersion,
        PlaybookVersion? publishedVersion,
        Organization organization,
        PlaybookRunGroupSummary? lastRunGroup)
    {
        var publishState = (currentVersion, publishedVersion) switch
        {
            (null, _) or (_, null) => PlaybookPublishState.NeverPublished,
            var (editing, published) when editing.Version == published.Version => PlaybookPublishState.Published,
            _ => PlaybookPublishState.PublishedWithUnpublishedChanges,
        };
        return new PlaybookViewModel(id, name, description, slug, enabled, currentVersion, publishedVersion, publishState, organization, lastRunGroup);
    }

    public static PlaybookViewModel FromPlaybook(
        Playbook playbook,
        PlaybookVersion? currentVersion,
        PlaybookVersion? publishedVersion,
        PlaybookRunGroupSummary? lastRunGroup) => Create(
        playbook.Id,
        playbook.Name,
        playbook.Description,
        playbook.Slug,
        playbook.Enabled,
        currentVersion,
        publishedVersion,
        playbook.Organization,
        lastRunGroup);
}
