using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

public record SuggestedTaskSelector : ISelector<Conversation>
{
    readonly bool _all;

    public SuggestedTaskSelector(bool all = false)
    {
        _all = all;
    }

    public static readonly SuggestedTaskSelector All = new(all: true);

    public IQueryable<Conversation> Apply(IQueryable<Conversation> queryable)
    {
        return _all
            ? queryable
            : queryable.Where(c => c.Tags.All(t => t.Tag.Name != "topic:social"))
                .Where(
                c => EF.Functions.JsonExists(c.SerializedProperties!, nameof(ConversationProperties.Conclusion))
                && !EF.Functions.JsonExists(c.SerializedProperties!, nameof(ConversationProperties.RelatedTaskItemId)));
    }
}
