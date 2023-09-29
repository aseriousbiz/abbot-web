using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Messages;

namespace Serious.TestHelpers
{
    public class FakeSkillContextAccessor : SkillContextAccessor
    {
        public FakeSkillContextAccessor(int skillId) : this(new SkillRunnerInfo
        {
            SkillId = skillId
        })
        {
        }

        public FakeSkillContextAccessor(
            SkillRunnerInfo? runnerInfo = null,
            SkillInfo? skillInfo = null,
            string? assemblyName = null)
        {
            SkillContext = new FakeSkillContext(
                runnerInfo,
                skillInfo,
                assemblyName: assemblyName);
        }
    }
}
