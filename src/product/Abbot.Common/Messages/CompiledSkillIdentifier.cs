using Serious.Abbot.Entities;

namespace Serious.Abbot.Messages;

/// <summary>
/// Used to uniquely identify a skill assembly.
/// </summary>
public class CompiledSkillIdentifier : ICompiledSkillIdentifier
{
    public CompiledSkillIdentifier(SkillMessage message)
    {
        var (skillInfo, runnerInfo) = message;
        PlatformId = skillInfo.PlatformId;
        SkillName = skillInfo.SkillName;

        SkillId = runnerInfo.SkillId;
        CacheKey = runnerInfo.CacheKey;
        Language = runnerInfo.Language;
    }

    public string PlatformId { get; }

    /// <summary>
    /// The id of the skill
    /// </summary>
    public int SkillId { get; }

    /// <summary>
    /// The name of the skill.
    /// </summary>
    public string SkillName { get; }

    /// <summary>
    /// The cache key for the skill.
    /// </summary>
    public string CacheKey { get; }

    public CodeLanguage Language { get; }
}
