using Serious.Abbot;
using Serious.Abbot.Metadata;
using Serious.Abbot.Skills;

namespace Serious.TestHelpers
{
    public class FakeResolvedSkill : IResolvedSkill
    {
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ISkill Skill { get; set; } = null!;
        public int? SkillId { get; set; }
        public SkillDataScope Scope { get; set; }
    }
}
