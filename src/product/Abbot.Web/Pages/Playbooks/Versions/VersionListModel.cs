using Serious.Abbot.Entities;
using Serious.Collections;

namespace Serious.Abbot.Pages.Playbooks.Versions;

public record VersionListModel(
    Playbook Playbook,
    PlaybookVersion? Latest,
    PlaybookVersion? LatestPublished,
    IPaginatedList<PlaybookVersion> Versions);
