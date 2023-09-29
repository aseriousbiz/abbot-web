using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Serious.Linq.Expressions;

namespace Serious.EntityFrameworkCore;

/// <summary>
/// Extension methods of <see cref="ModelBuilder"/> useful for EF Core.
/// </summary>
public static class ModelBuilderExtensions
{
    static readonly MethodInfo SetQueryFilterMethod = typeof(ModelBuilderExtensions)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(t => t.IsGenericMethod && t.Name == nameof(SetQueryFilter));

    /// <summary>
    /// Sets the specified value converter on all properties of the specified entity type.
    /// </summary>
    /// <remarks>
    /// If the value converter has a parameterless constructor, use <see cref="DbContext.ConfigureConventions"/>
    /// instead with something like <c>configurationBuilder.Properties&lt;TClrType&gt;().HaveConversion&lt;ValueConverterType&gt;();</c>.
    /// </remarks>
    /// <param name="builder">The <see cref="ModelBuilder"/>.</param>
    /// <param name="valueConverter">The value converter to set.</param>
    /// <typeparam name="TClrType">The type of the entity property to apply the value converter to.</typeparam>
    public static void SetValueConverterOnPropertiesOfType<TClrType>(
        this ModelBuilder builder,
        ValueConverter valueConverter)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(TClrType))
                {
                    property.SetValueConverter(valueConverter);
                }
            }
        }
    }

    /// <summary>
    /// Sets the query filter on all entities that implement the the given interface.
    /// </summary>
    /// <param name="builder">The <see cref="ModelBuilder"/>.</param>
    /// <param name="filterExpression">The query filter expression.</param>
    /// <typeparam name="TEntityInterface">The interface type.</typeparam>
    public static void SetQueryFilterOnAllEntities<TEntityInterface>(
        this ModelBuilder builder,
        Expression<Func<TEntityInterface, bool>> filterExpression)
    {
        foreach (var type in builder.Model.GetEntityTypes()
                     .Where(t => t.BaseType == null)
                     .Select(t => t.ClrType)
                     .Where(t => typeof(TEntityInterface).IsAssignableFrom(t)))
        {
            builder.SetEntityQueryFilter(
                type,
                filterExpression);
        }
    }

    static void SetEntityQueryFilter<TEntityInterface>(
        this ModelBuilder builder,
        Type entityType,
        Expression<Func<TEntityInterface, bool>> filterExpression)
    {
        SetQueryFilterMethod
            .MakeGenericMethod(entityType, typeof(TEntityInterface))
            .Invoke(null, new object[] { builder, filterExpression });
    }

    static void SetQueryFilter<TEntity, TEntityInterface>(
        this ModelBuilder builder,
        Expression<Func<TEntityInterface, bool>> filterExpression)
        where TEntityInterface : class
        where TEntity : class, TEntityInterface
    {
        var concreteExpression = filterExpression
            .Convert<TEntityInterface, TEntity>();
        builder.Entity<TEntity>()
            .AppendQueryFilter(concreteExpression);
    }

    // CREDIT: This comment by magiak on GitHub https://github.com/dotnet/efcore/issues/10275#issuecomment-785916356
    static void AppendQueryFilter<T>(this EntityTypeBuilder entityTypeBuilder, Expression<Func<T, bool>> expression)
        where T : class
    {
        var parameterType = Expression.Parameter(entityTypeBuilder.Metadata.ClrType);

        var expressionFilter = ReplacingExpressionVisitor.Replace(
            expression.Parameters.Single(), parameterType, expression.Body);

        if (entityTypeBuilder.Metadata.GetQueryFilter() != null)
        {
            var currentQueryFilter = entityTypeBuilder.Metadata.GetQueryFilter();
            if (currentQueryFilter is not null)
            {
                var currentExpressionFilter = ReplacingExpressionVisitor.Replace(
                    currentQueryFilter.Parameters.Single(), parameterType, currentQueryFilter.Body);
                expressionFilter = Expression.AndAlso(currentExpressionFilter, expressionFilter);
            }
        }

        var lambdaExpression = Expression.Lambda(expressionFilter, parameterType);
        entityTypeBuilder.HasQueryFilter(lambdaExpression);
    }
}
