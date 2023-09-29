using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Messages;

namespace Serious.TestHelpers
{
    public class FakeSkillContext : SkillContext
    {
        public FakeSkillContext(
            SkillRunnerInfo? runnerInfo = null,
            SkillInfo? skillInfo = null,
            string? apiKey = null,
            string? assemblyName = null)
            : base(
                new SkillMessage
                {
                    RunnerInfo = runnerInfo ?? new SkillRunnerInfo(),
                    SkillInfo = skillInfo ?? new SkillInfo
                    {
                        Room = new PlatformRoom("C0000001234",
                            "the-room"),
                        IsPlaybook = false,
                    }
                },
                apiKey ?? "FakeApiKey")
        {
            SetAssemblyName(assemblyName ?? "FakeAssemblyName");
        }
    }
}
