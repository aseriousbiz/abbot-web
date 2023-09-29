using Serious.Abbot.Metadata;
using Serious.Abbot.Models;
using Serious.Abbot.Skills;

namespace Serious.TestHelpers
{
    public class FakeBuiltInSkillDescriptor : IBuiltinSkillDescriptor
    {
        public FakeBuiltInSkillDescriptor(string name, ISkill skill, string description = "", bool hidden = false, string? featureFlag = null, PlanFeature? planFeature = null)
        {
            Name = name;
            Skill = skill;
            Description = description;
            Hidden = hidden;
            FeatureFlag = featureFlag;
            PlanFeature = planFeature;
        }

        public string Name { get; }
        public string Description { get; }
        public ISkill Skill { get; }
        public bool Hidden { get; }
        public string? FeatureFlag { get; }
        public PlanFeature? PlanFeature { get; }
    }
}
