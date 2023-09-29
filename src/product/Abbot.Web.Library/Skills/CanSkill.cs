using System.Linq;
using System.Threading;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;

namespace Serious.Abbot.Skills;

[Skill(Description = "Manage skill permissions.")]
public class CanSkill : ISkill
{
    readonly IPermissionRepository _permissionRepository;
    readonly ISkillRepository _skillRepository;
    readonly IUrlGenerator _urlGenerator;

    public CanSkill(
        IPermissionRepository permissionRepository,
        ISkillRepository skillRepository,
        IUrlGenerator urlGenerator)
    {
        _permissionRepository = permissionRepository;
        _skillRepository = skillRepository;
        _urlGenerator = urlGenerator;
    }

    public async Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var (mentionArg, verbArg, skillArg) = messageContext.Arguments;

        if (mentionArg.Value is "not")
        {
            (mentionArg, verbArg) = (verbArg, mentionArg);
        }

        if (mentionArg is IMissingArgument || !Argument.TryParseMention(mentionArg.Value, out var platformUserId))
        {
            await messageContext.SendHelpTextAsync(this);
            return;
        }

        var mention = messageContext.Mentions.FirstOrDefault(m => platformUserId.Equals(m.User.PlatformUserId, StringComparison.Ordinal));
        if (mention is null || mention.OrganizationId != messageContext.FromMember.OrganizationId)
        {
            await messageContext.SendActivityAsync("I did not recognize that user.");
            return;
        }

        await ((verbArg, skillArg) switch
        {
            (IMissingArgument, _) or (_, IMissingArgument) => messageContext.SendActivityAsync(GetHelpWithSkillText(messageContext)),
            var (verb, skill) => SetPermissionAndReply(
                GetCapabilityFromVerb(verb.Value),
                mention,
                skill.Value,
                messageContext)
        });

    }

    async Task SetPermissionAndReply(Capability capability, Member member, string skillName, MessageContext messageContext)
    {
        if (!messageContext.Organization.HasPlanFeature(PlanFeature.SkillPermissions))
        {
            await messageContext.SendActivityAsync($"Permissions are a Business Plan feature. Contact us at `{WebConstants.SupportEmail}` to discuss upgrading your plan.");
            return;
        }

        var skill = await _skillRepository.GetAsync(skillName, messageContext.Organization);
        if (skill is null)
        {
            await messageContext.SendActivityAsync($"The skill `{skillName}` does not exist.");
            return;
        }

        if (!skill.Restricted)
        {
            var skillUrl = _urlGenerator.SkillPage(skillName);
            await messageContext.SendActivityAsync($"The skill `{skillName}` is not a restricted skill. " +
                                                   $"Skills can be protected in the Skill Editor {skillUrl}.");
            return;
        }

        if (!await DoesCallerHavePermissionToSetPermissions(messageContext, skill))
        {
            await messageContext.SendActivityAsync("I’m sorry, but you do not have permission to set " +
                                                   "permissions on this skill.");
            return;
        }

        if (capability >= Capability.Edit && member.OrganizationId != skill.OrganizationId)
        {
            await messageContext.SendActivityAsync("I’m sorry, but you cannot grant edit or higher permissions to " +
                                                   "someone outside of your organization.");
            return;
        }

        if (member.IsAdministrator())
        {
            await messageContext.SendActivityAsync($"{member.FormatMention()} is an Administrator and I cannot change " +
                                                   "permissions for admin users.");
            return;
        }

        var previousCapability = await _permissionRepository.SetPermissionAsync(
            member,
            skill,
            capability,
            messageContext.FromMember);

        var reply = capability != Capability.None
            ? $"Ok, {member.User.FormatMention()} can {capability.ToVerb()} `{skillName}`"
            : $"Ok, {member.User.FormatMention()} no longer has any permissions for `{skillName}`";
        if (previousCapability > capability)
        {
            reply += $" _(downgraded from `{previousCapability}`)_";
        }

        await messageContext.SendActivityAsync(reply + ".");
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.AddAlternativeUsage("@Username can use {skill}", "gives user permission to use {skill}.");
        usage.AddAlternativeUsage("@Username can edit {skill}", "gives user permission to edit {skill}. Edit includes use permissions.");
        usage.AddAlternativeUsage("@Username can admin {skill}", "gives user full permissions for {skill} which includes setting permissions.");
        usage.AddAlternativeUsage("@Username can not {skill}", "removes all user's permissions for {skill}.");
        usage.AddAlternativeUsage("who can {skill}", "To find out who has permissions to {skill}.");
    }

    static Capability GetCapabilityFromVerb(string verb)
    {
        if (Enum.TryParse<Capability>(verb, true, out var result))
            return result;
        return verb.ToUpperInvariant() switch
        {
            "NOT" => Capability.None,
            "RUN" => Capability.Use,
            _ => Capability.None
        };
    }

    static string GetHelpWithSkillText(MessageContext messageContext)
    {
        return $"`{messageContext.Bot} help {messageContext.SkillName}` for help on this skill.";
    }

    async Task<bool> DoesCallerHavePermissionToSetPermissions(MessageContext messageContext, Skill skill)
    {
        if (messageContext.FromMember.IsAdministrator())
        {
            return true;
        }

        var permission = await _permissionRepository.GetCapabilityAsync(messageContext.FromMember, skill);
        return permission >= Capability.Admin;
    }
}
