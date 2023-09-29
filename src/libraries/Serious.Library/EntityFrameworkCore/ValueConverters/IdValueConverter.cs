using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Serious.EntityFrameworkCore;

/// <summary>
/// A <see cref="ValueConverter{TModel,TProvider}"/> that converts <see cref="Id{T}"/> to and from <see cref="int"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type the Id belongs to.</typeparam>
public class IdValueConverter<TEntity> : ValueConverter<Id<TEntity>, int> where TEntity : class
{
    public IdValueConverter(ConverterMappingHints? mappingHints = null) : base(
        id => id,
        value => new Id<TEntity>(value),
        mappingHints)
    {
    }

    public static readonly IdValueConverter<TEntity> Instance = new();
}
