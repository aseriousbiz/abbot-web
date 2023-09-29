using System.Diagnostics.CodeAnalysis;

namespace Serious.Abbot.Entities;

public enum EntityResultType
{
    Success,
    Conflict,
    NotFound,
}

public record EntityResult(EntityResultType Type, string? ErrorMessage)
{
    public static EntityResult Success() => new(EntityResultType.Success, null);
    public static EntityResult<TEntity> Success<TEntity>(TEntity value) where TEntity : class
        => new(EntityResultType.Success, value, null);
    public static EntityLookupResult<TEntity, TKey> Success<TEntity, TKey>(TEntity value, TKey key) where TEntity : class
        => new(EntityResultType.Success, key, value, null);
    public static EntityLookupResult<TEntity, TKey> NotFound<TEntity, TKey>(TKey key) where TEntity : class =>
        new(EntityResultType.NotFound, key, null, null);
    public static EntityResult Conflict(string message) => new(EntityResultType.Conflict, message);

    /// <summary>
    /// Creates a result representing a <typeparamref name="T"/> that could not be created due to a conflict.
    /// </summary>
    /// <typeparam name="T">The entity to create.</typeparam>
    /// <param name="message">The conflict reason.</param>
    /// <param name="entity">The conflicting <typeparamref name="T"/>.</param>
    public static EntityResult<T> Conflict<T>(string message, T? entity = null) where T : class => new(EntityResultType.Conflict, entity, message);

    /// <summary>
    /// Gets a boolean that indicates if the request was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSuccess => Type is EntityResultType.Success && ErrorMessage is null;
}

/// <summary>
/// Indicates the result of a request to create or retrieve an entity.
/// </summary>
/// <param name="ErrorMessage">The error message, if any.</param>
/// <typeparam name="TEntity">The entity type.</typeparam>
public record EntityResult<TEntity>(
    EntityResultType Type,
    TEntity? Entity,
    string? ErrorMessage)
    where TEntity : class
{
    /// <summary>
    /// Gets a boolean that indicates if the request was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    [MemberNotNullWhen(true, nameof(Entity))]
    public bool IsSuccess => Type is EntityResultType.Success && ErrorMessage is null && Entity is not null;

    public static implicit operator EntityResult<TEntity>(TEntity? value) =>
        value is null ? new EntityResult<TEntity>(EntityResultType.NotFound, null, null) : EntityResult.Success(value);
}

/// <summary>
/// Indicates the result of a request to create or retrieve an entity.
/// </summary>
/// <param name="ErrorMessage">The error message, if any.</param>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TKey">The key used to lookup the result.</typeparam>
public record EntityLookupResult<TEntity, TKey>(
    EntityResultType Type,
    TKey Key,
    TEntity? Entity,
    string? ErrorMessage)
    where TEntity : class
{
    /// <summary>
    /// Gets a boolean that indicates if the request was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    [MemberNotNullWhen(true, nameof(Entity))]
    public bool IsSuccess => Type is EntityResultType.Success && ErrorMessage is null && Entity is not null;
}
