using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

public interface ISignalRepository
{
    /// <summary>
    /// Retrieves all the distinct signal names that have skill subscriptions in the organization.
    /// </summary>
    /// <param name="organization">The organization to retrieve skill patterns for.</param>
    /// <returns>All the signal name instances for the specified organization.</returns>
    Task<IReadOnlyList<string>> GetAllAsync(Organization organization);

    /// <summary>
    /// Retrieves all the signal subscriptions and the associated skills.
    /// </summary>
    /// <param name="organization">The organization to retrieve skill patterns for.</param>
    /// <returns>All the signal name instances for the specified organization.</returns>
    Task<IReadOnlyList<SignalSubscription>> GetAllSubscriptionsAsync(Organization organization);

    /// <summary>
    /// Get the signal subscription for the skill by name.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="skill">The name of the skill.</param>
    /// <param name="organization">The organization to retrieve skill patterns for.</param>
    /// <returns>All the signal name instances for the specified organization.</returns>
    Task<SignalSubscription?> GetAsync(string name, string skill, Organization organization);

    /// <summary>
    /// Creates a pattern with the specified values.
    /// </summary>
    /// <param name="name">The name of the pattern.</param>
    /// <param name="caseSensitive"></param>
    /// <param name="skill">The <see cref="Skill"/> the pattern belongs to.</param>
    /// <param name="creator">The creator of the pattern.</param>
    /// <param name="argumentsPattern">The pattern to use, if any, for matching the signal arguments.</param>
    /// <param name="argumentsPatternType">The type of matching to use.</param>
    Task<SignalSubscription> CreateAsync(
        string name,
        string? argumentsPattern,
        PatternType argumentsPatternType,
        bool caseSensitive,
        Skill skill,
        User creator);

    /// <summary>
    /// Delete the specified pattern.
    /// </summary>
    /// <param name="subscription">The signal to remove from the skill.</param>
    /// <param name="deleteBy">The user deleting the pattern.</param>
    /// <param name="organization">The organization to remove the subscription from.</param>
    Task RemoveAsync(SignalSubscription subscription, User deleteBy, Organization organization);
}
