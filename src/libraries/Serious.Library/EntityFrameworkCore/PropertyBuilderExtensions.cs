using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Serious.EntityFrameworkCore;

/// <summary>
/// Extensions to configure properties for EF Core.
/// </summary>
public static class PropertyBuilderExtensions
{
    /// <summary>
    /// Used to specify that a property is an Id property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <remarks>
    /// This won't fully work for auto-generated Identity keys until EF Core 7.0 when it can take advantage of
    /// <see href="https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#improved-value-generation">Improved Value Generation</see>
    /// </remarks>
    /// <returns>The passed in property builder.</returns>
    public static PropertyBuilder<Id<TEntity>> IsStronglyTypedId<TEntity>(this PropertyBuilder<Id<TEntity>> property)
        where TEntity : class
    {
        return property
            .HasConversion(new IdValueConverter<TEntity>())
            .ValueGeneratedOnAdd();
    }
}
