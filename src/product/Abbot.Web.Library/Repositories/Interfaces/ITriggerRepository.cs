using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository that manages Http and Scheduled triggers for skills. Used to create, retrieve, update, and delete
/// triggers.
/// </summary>
public interface ITriggerRepository
{
    /// <summary>
    /// Retrieves a scheduled trigger by id.
    /// </summary>
    /// <param name="scheduledTriggerId"></param>
    Task<SkillScheduledTrigger?> GetScheduledTriggerAsync(int scheduledTriggerId);

    /// <summary>
    /// Retrieves a skill http trigger by ID. This is used by the website.
    /// </summary>
    /// <param name="skillName">The name of the skill for the trigger.</param>
    /// <param name="triggerName">The name of the skill trigger.</param>
    /// <param name="organization">The organization the trigger belongs to.</param>
    Task<TTrigger?> GetSkillTriggerAsync<TTrigger>(
        string skillName,
        string triggerName,
        Organization organization)
        where TTrigger : SkillTrigger;

    /// <summary>
    /// Retrieves the <see cref="SkillHttpTrigger"/> associated with the api token and skill name. It will return
    /// triggers for deleted and disabled skills.
    /// </summary>
    /// <param name="skillName">The name of the skill.</param>
    /// <param name="apiToken">The API Token associated with the skill.</param>
    /// <returns>An instance of <see cref="SkillHttpTrigger"/> with a loaded <see cref="Skill"/> property.</returns>
    Task<SkillHttpTrigger?> GetSkillHttpTriggerAsync(string skillName, string apiToken);

    /// <summary>
    /// Creates a <see cref="SkillHttpTrigger" /> with a random API Token. This is
    /// used to connect a <see cref="Skill"/> to a chat room so that the skill can be called via
    /// an HTTP request and respond to the room.
    /// </summary>
    /// <param name="skill">The <see cref="Skill"/> to trigger.</param>
    /// <param name="description">Description of what the trigger is used for.</param>
    /// <param name="room">Information about the room this trigger is created in.</param>
    /// <param name="creator">The person that created the trigger.</param>
    Task<SkillHttpTrigger> CreateHttpTriggerAsync(
        Skill skill,
        string? description,
        IRoom room,
        Member creator);

    /// <summary>
    /// Creates a <see cref="SkillScheduledTrigger" />. This is used to connect a <see cref="Skill"/> to
    /// a chat room so that the skill can be called via a recurring schedule and respond to the room.
    /// </summary>
    /// <param name="skill">The <see cref="Skill"/> to trigger.</param>
    /// <param name="arguments">The arguments to pass to the skill.</param>
    /// <param name="description">Description of what the trigger is used for.</param>
    /// <param name="cronSchedule">A schedule for the trigger.</param>
    /// <param name="room">Information about the room this trigger is created in.</param>
    /// <param name="creator">The person that created the trigger.</param>
    Task<SkillScheduledTrigger> CreateScheduledTriggerAsync(
        Skill skill,
        string? arguments,
        string? description,
        string cronSchedule,
        IRoom room,
        Member creator);

    /// <summary>
    /// Deletes the <see cref="SkillTrigger" />.
    /// </summary>
    /// <param name="trigger">Trigger to delete.</param>
    /// <param name="user">The user that deletes it.</param>
    Task DeleteTriggerAsync(SkillTrigger trigger, User user);

    /// <summary>
    /// Updates the description for the <see cref="SkillTrigger"/>
    /// </summary>
    /// <param name="trigger">Trigger to delete.</param>
    /// <param name="description">The new description for the trigger.</param>
    /// <param name="user">The user that deletes it.</param>
    Task UpdateTriggerDescriptionAsync(SkillTrigger trigger, string? description, User user);

    /// <summary>
    /// Gets the <see cref="Playbook"/> that matches the <see cref="Playbook.Slug"/> and
    /// <see cref="PlaybookTrigger.ApiToken"/>.
    /// </summary>
    /// <param name="slug">The <see cref="Playbook.Slug"/>.</param>
    /// <param name="apiToken">The <see cref="PlaybookTrigger.ApiToken"/>.</param>
    /// <returns>The <see cref="PlaybookTrigger"/> if found, otherwise <see langword="null"/>.</returns>
    Task<Playbook?> GetPlaybookFromTriggerTokenAsync(string slug, string apiToken);
}
