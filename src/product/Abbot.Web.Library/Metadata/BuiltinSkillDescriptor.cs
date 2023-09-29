using System;
using System.Reflection;
using Serious.Abbot.Models;
using Serious.Abbot.Skills;

namespace Serious.Abbot.Metadata;

public class BuiltinSkillDescriptor : IBuiltinSkillDescriptor
{
    readonly Lazy<ISkill> _lazySkill;

    public BuiltinSkillDescriptor(Type skillType, Lazy<ISkill> lazySkill)
    {
        if (!skillType.Implements<ISkill>())
        {
            throw new ArgumentException(
                $"The passed in type {skillType} does not implement `ISkill`.",
                nameof(skillType));
        }

        var skillAttribute = GetSkillAttributeFromType(skillType);
        Name = GetSkillName(skillAttribute, skillType).ToLowerInvariant();
        Description = skillAttribute?.Description ?? string.Empty;
        Hidden = skillAttribute?.Hidden ?? false;
        FeatureFlag = skillAttribute?.RequireFeatureFlag;
        PlanFeature = skillAttribute?.RequirePlanFeature;
        _lazySkill = lazySkill;
    }

    public string Name { get; }
    public string Description { get; }
    public bool Hidden { get; }
    public string? FeatureFlag { get; }
    public PlanFeature? PlanFeature { get; }

    public ISkill Skill => _lazySkill.Value;

    static string GetSkillName(SkillAttribute? skillAttribute, MemberInfo skillType)
    {
        return skillAttribute is null || string.IsNullOrWhiteSpace(skillAttribute.Name)
            ? skillType.Name.TrimSuffix(nameof(Skill), StringComparison.Ordinal)
            : skillAttribute.Name;
    }

    static SkillAttribute? GetSkillAttributeFromType(MemberInfo type)
    {
        return Attribute.GetCustomAttribute(type, typeof(SkillAttribute)) is SkillAttribute attribute
            ? attribute
            : null;
    }
}
