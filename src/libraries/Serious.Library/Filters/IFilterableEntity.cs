using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
#pragma warning disable CA1000

namespace Serious.Filters;

/// <summary>
/// Represents an entity that can be filtered. This is not required for filters to work, but by implementing this
/// interface, filtering gains some benefits.
/// </summary>
public interface IFilterableEntity<TEntity> where TEntity : IFilterableEntity<TEntity>
{
    /// <summary>
    /// Returns all the <see cref="IFilterItemQuery{TEntity}"/> that can be used to filter queries of T.
    /// </summary>
    /// <returns>The set of filter queries for this type.</returns>
    static abstract IEnumerable<IFilterItemQuery<TEntity>> GetFilterItemQueries();
}

/// <summary>
/// Represents an entity that can be filtered, but the filters require access to a DbContext.
/// This is not required for filters to work, but by implementing this interface, filtering gains some benefits.
/// </summary>
public interface IFilterableEntity<TEntity, in TContext>
    where TContext : DbContext
    where TEntity : IFilterableEntity<TEntity, TContext>
{
    /// <summary>
    /// Returns all the <see cref="IFilterItemQuery{TEntity}"/> that can be used to filter queries of T.
    /// </summary>
    /// <returns>The set of filter queries for this type.</returns>
    static abstract IEnumerable<IFilterItemQuery<TEntity>> GetFilterItemQueries(TContext dbContext);
}

/// <summary>
/// Extension methods that light up for IQueryable{T} when T implements <see cref="IFilterableEntity{T}"/>.
/// </summary>
public static class QueryableFilteringExtensions
{
    /// <summary>
    /// Applies the filter to the queryable.
    /// </summary>
    /// <param name="queryable">The query to filter.</param>
    /// <param name="filter">The set of filters to apply.</param>
    /// <param name="defaultField">The default field to query for filters with no fields.</param>
    /// <typeparam name="TFilterableEntity">The filterable entity.</typeparam>
    public static IQueryable<TFilterableEntity> ApplyFilter<TFilterableEntity>(
        this IQueryable<TFilterableEntity> queryable,
        FilterList filter,
        string? defaultField = default)
        where TFilterableEntity : IFilterableEntity<TFilterableEntity>
    {
        if (!filter.Any())
        {
            return queryable;
        }
        var queryFilter = new QueryFilter<TFilterableEntity>(TFilterableEntity.GetFilterItemQueries());
        return queryFilter.Apply(queryable, filter, defaultField: defaultField);
    }

    /// <summary>
    /// Applies the filter to the queryable.
    /// </summary>
    /// <param name="queryable">The query to filter.</param>
    /// <param name="filter">The set of filters to apply.</param>
    /// <param name="dbContext">The <see cref="DbContext"/> needed to populate the query filters.</param>
    /// <param name="defaultField">The default field to query for filters with no fields.</param>
    /// <typeparam name="TFilterableEntity">The type of filterable entity..</typeparam>
    /// <typeparam name="TContext">The type of <see cref="DbContext"/>.</typeparam>
    public static IQueryable<TFilterableEntity> ApplyFilter<TFilterableEntity, TContext>(
        this IQueryable<TFilterableEntity> queryable,
        FilterList filter,
        TContext dbContext,
        string? defaultField = default)
        where TFilterableEntity : IFilterableEntity<TFilterableEntity, TContext>
        where TContext : DbContext
    {
        if (!filter.Any())
        {
            return queryable;
        }
        var queryFilter = new QueryFilter<TFilterableEntity>(TFilterableEntity.GetFilterItemQueries(dbContext));
        return queryFilter.Apply(queryable, filter, defaultField: defaultField);
    }
}
