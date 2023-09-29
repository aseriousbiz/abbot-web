using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Filters;

namespace Serious.Abbot.Extensions;

/// <summary>
/// Extension methods for querying things.
/// </summary>
public static class QueryableExtensions
{
    /// <inheritdoc cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})" />
    /// <param name="query">The queryable.</param>
    /// <param name="noTracking">If <see langword="true"/>, the <typeparamref name="TEntity"/> will be queried with no tracking.</param>
    public static IQueryable<TEntity> AsNoTracking<TEntity>(this IQueryable<TEntity> query, bool noTracking)
        where TEntity : class =>
        noTracking ? query.AsNoTracking() : query;

    /// <summary>
    /// Returns a queryable that excludes our known Slack Platform IDs.
    /// </summary>
    /// <param name="query">The queryable.</param>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    public static IQueryable<TEntity> WhereNotUs<TEntity>(this IQueryable<TEntity> query)
        where TEntity : IOrganizationEntity
    {
#if DEBUG
        return query;
#else
        return query.Where(e => !WebConstants.OurSlackTeamIds.Contains(e.Organization.PlatformId));
#endif
    }

    /// <summary>
    /// Returns a single item from the query by <c>Id</c>, or null if no items match.
    /// Throws an exception if more than one item .
    /// Unlike the standard SingleOrDefaultAsync method, this method provides more details when more than one is found.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />. Must implement <see cref="IEntity"/>.</typeparam>
    /// <param name="source">An <see cref="IQueryable{TSource}" /> to return the single element of.</param>
    /// <param name="id">The <see cref="Id{TSource}"/> to find.</param>
    /// <param name="context">Additional context to provide for the error message.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The task result contains the single element of the input sequence that satisfies the condition in
    ///     <paramref name="predicate" />, or <see langword="default" /> ( <typeparamref name="TSource" /> ) if no such
    /// element is found.
    /// </returns>
    public static async Task<TSource?> SingleEntityOrDefaultAsync<TSource>(
        this IQueryable<TSource> source,
        Id<TSource>? id,
        string? context = null,
        CancellationToken cancellationToken = default) where TSource : class, IEntity
    {
        if (id == null)
        {
            return null;
        }

        return await source.SingleEntityOrDefaultAsync(x => x.Id == id, context, cancellationToken);
    }

    /// <summary>
    /// Returns a single item from the query, or null if no items match. Throws an exception if more than one item
    /// matches. Unlike the standard SingleOrDefaultAsync method, this method provides more details when .
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />. Must implement <see cref="IEntity"/>.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}" /> to return the single element of.</param>
    /// <param name="predicate">A function to test an element for a condition.</param>
    /// <param name="context">Additional context to provide for the error message.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The task result contains the single element of the input sequence that satisfies the condition in
    ///     <paramref name="predicate" />, or <see langword="default" /> ( <typeparamref name="TSource" /> ) if no such
    /// element is found.
    /// </returns>
    public static async Task<TSource?> SingleEntityOrDefaultAsync<TSource>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, bool>> predicate,
        string? context = null,
        CancellationToken cancellationToken = default) where TSource : IEntity
    {
        var result = await source.Where(predicate).Take(2).ToListAsync(cancellationToken);
        return result.Count switch
        {
            0 => default,
            1 => result[0],
            _ => throw new InvalidOperationException(
                $"More than one item matched the query with context `{context}`. {FormatMatchingEntities(result)} items matched.")
        };
    }

    /// <summary>
    /// Returns a single item from the query, or null if no items match. Throws an exception if more than one item
    /// matches. Unlike the standard SingleOrDefaultAsync method, this method provides more details when .
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />. Must implement <see cref="IEntity"/>.</typeparam>
    /// <param name="source">An <see cref="IQueryable{T}" /> to return the single element of.</param>
    /// <param name="context">Additional context to provide for the error message.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The task result contains the single element of the input sequence or <see langword="default" /> (
    /// <typeparamref name="TSource" /> ) if no such element is found.
    /// </returns>
    public static async Task<TSource?> SingleEntityOrDefaultAsync<TSource>(
        this IQueryable<TSource> source,
        string? context = null,
        CancellationToken cancellationToken = default) where TSource : IEntity
    {
        var result = await source.Take(2).ToListAsync(cancellationToken);
        return result.Count switch
        {
            0 => default,
            1 => result[0],
            _ => throw new InvalidOperationException(
                $"More than one item matched the query with context `{context}`. {FormatMatchingEntities(result)} items matched.")
        };
    }

    static string FormatMatchingEntities<TSource>(IEnumerable<TSource> entities) where TSource : IEntity
    {
        static string FormatEntity(TSource entity)
        {
            return "\t- " + entity switch
            {
                Organization organization => $"Organization (Id: {organization.Id}, PlatformId: {organization.PlatformId})",
                User user => $"User (Id: {user.Id}, PlatformUserId: {user.PlatformUserId})",
                _ => $"{entity.GetType().Name} (Id: {entity.Id})"
            };
        }

        return string.Join("\n", entities.Select(FormatEntity));
    }

    /// <summary>
    /// Builds a queryable that returns items where the predicate is true or not true depending on the value of <paramref name="where"/>.
    /// </summary>
    /// <param name="source">An <see cref="IQueryable{T}" /> to filter.</param>
    /// <param name="where">If <c>true</c> then returns items that match the predicate. Otherwise returns items where predicate is false.</param>
    /// <param name="predicate">The predicate used to filter items.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    public static IQueryable<TSource> WhereOrNot<TSource>(
        this IQueryable<TSource> source,
        bool where,
        Expression<Func<TSource, bool>> predicate)
        => where ? source.Where(predicate) : source.WhereNot(predicate);

    /// <summary>
    /// Builds a queryable where the predicate is not true.
    /// </summary>
    /// <param name="source">An <see cref="IQueryable{T}" /> to filter.</param>
    /// <param name="predicate">The predicate used to filter items.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    public static IQueryable<TSource> WhereNot<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
    {
        var notPredicate = Expression.Lambda<Func<TSource, bool>>(Expression.Not(predicate.Body), predicate.Parameters.ToArray());
        return source.Where(notPredicate);
    }
}
