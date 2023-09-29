using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Routing;
using Serious.Abbot.Services;
using Serious.Text;

namespace Serious.Abbot.Skills;

[Skill(Description =
    "The most helpful skill around. Lists the set of available skills and can be used to obtain help for individual skills.")]
public sealed class HelpSkill : ISkill
{
    readonly ISkillManifest _skillManifest;
    readonly IUrlGenerator _urlGenerator;

    public HelpSkill(ISkillManifest skillManifest, IUrlGenerator urlGenerator)
    {
        _skillManifest = skillManifest;
        _urlGenerator = urlGenerator;
    }

    public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var args = messageContext.Arguments;

        return args switch
        { { Count: 0 } => ShowAbbotHelp(messageContext), { Count: 1 } => ReplyWithHelpForSkill(args.Value, messageContext),
            _ => messageContext.SendHelpTextAsync(this)
        };
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{skill}", "Returns the usage text for the specified skill");
    }

    async Task ReplyWithHelpForSkill(string skillName, MessageContext messageContext)
    {
        var skillDescriptor = await _skillManifest.ResolveSkillAsync(skillName, messageContext.Organization, messageContext);
        if (skillDescriptor is null)
        {
            await messageContext.SendActivityAsync(
                $"The skill `{skillName}` does not exist.");
            return;
        }

        await messageContext.SendHelpTextAsync(skillDescriptor, skillName);
    }

    static readonly List<string> Preambles = new()
    {
        "Here I come to save the day!",
        "I gotchu.",
        "I can help!",
        "I am happy to help."
    };

    async Task ShowAbbotHelp(MessageContext messageContext)
    {
        var preamble = Preambles.Random();
        var bot = messageContext.Bot;
        var reply = $"{preamble} To get help for a specific skill, call `{bot} help {{skill}}`. " +
                    $"To see a list of all skills, call `{bot} skills`. To create or install skills, or otherwise manage " +
                    $"your Abbot, visit {_urlGenerator.HomePage()}.";
        await messageContext.SendActivityAsync(reply);
    }
}
