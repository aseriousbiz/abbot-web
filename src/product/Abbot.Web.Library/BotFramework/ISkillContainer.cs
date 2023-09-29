namespace Serious.Abbot.Skills;

public interface ISkillContainer
{
    /// <summary>
    /// Used to build the help text for this skill.
    /// </summary>
    void BuildSkillUsageHelp(UsageBuilder usage);
}
