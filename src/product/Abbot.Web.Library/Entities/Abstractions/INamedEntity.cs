namespace Serious.Abbot.Entities;

/// <summary>
/// An entity with an name.
/// </summary>
public interface INamedEntity : IEntity
{
    /// <summary>
    /// The name of the entity.
    /// </summary>
    string Name { get; }
}
