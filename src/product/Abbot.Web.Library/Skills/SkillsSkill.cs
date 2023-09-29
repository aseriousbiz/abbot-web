using System.Linq;
using System.Threading;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services;

namespace Serious.Abbot.Skills;

[Skill(Description = "Lists or searches the set of available skills.")]
public sealed class SkillsSkill : ISkill
{
    readonly ISkillManifest _skillManifest;

    public SkillsSkill(ISkillManifest skillManifest)
    {
        _skillManifest = skillManifest;
    }

    public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var (cmd, search) = messageContext.Arguments;

        return (cmd, search) switch
        {
            (IMissingArgument, IMissingArgument) => ShowListOfSkills(messageContext),
            (var skill, IMissingArgument) => SearchSkills(skill.Value, messageContext),
            var (skill, _) when skill.Value is "|" or "search" => SearchSkills(search.Value, messageContext),
            _ => messageContext.SendHelpTextAsync(this)
        };
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.AddEmptyArgsUsage("Returns a list of all the skills (including aliases and user defined skills.");
        usage.Add("{skill}", "Returns the usage text for the specified skill");
        usage.Add("| {search}", "Returns a list of skills that match the search term.");
    }

    async Task ShowListOfSkills(MessageContext messageContext)
    {
        var reply = (await _skillManifest.GetAllSkillDescriptorsAsync(messageContext.Organization, messageContext))
            .OrderBy(d => d.Name)
            .ToMarkdownList();
        await messageContext.SendActivityAsync(reply);
    }

    async Task SearchSkills(string skill, MessageContext messageContext)
    {
        var reply = (await _skillManifest.GetAllSkillDescriptorsAsync(messageContext.Organization, messageContext))
            .WhereFuzzyMatch(s => s.Name, s => s.Description, skill)
            .OrderBy(d => d.Name)
            .ToMarkdownList();
        reply = reply.Length == 0
            ? $"I did not find any skills similar to `{skill}`"
            : reply;
        await messageContext.SendActivityAsync(reply);
    }
}
