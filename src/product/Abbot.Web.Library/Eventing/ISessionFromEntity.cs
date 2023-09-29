namespace Serious.Abbot.Eventing;

/// <summary>
/// Marks a message type as using the <see cref="Id"/> field as the session Id.
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface ISessionFromEntity<TEntity> where TEntity : class
{
    Id<TEntity> Id { get; }
}
