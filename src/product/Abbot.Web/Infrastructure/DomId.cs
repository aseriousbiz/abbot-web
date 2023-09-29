using Serious.Abbot.Entities;

namespace Serious.Abbot;

public static class DomIdExtensions
{
    public static DomId GetDomId(this IEntity entity, string? baseName = null)
    {
        if (baseName is not { Length: > 0 })
        {
            baseName = entity.GetType().Name.ToDashCase();
        }

        return new($"{baseName}-{entity.Id}");
    }
}
