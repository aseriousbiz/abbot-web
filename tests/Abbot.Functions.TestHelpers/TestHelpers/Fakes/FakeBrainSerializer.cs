using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Storage;

namespace Serious.TestHelpers;

public class FakeBrainSerializer : BrainSerializer
{
    public FakeBrainSerializer(ISkillContextAccessor? accessor = null) : base(accessor ?? new FakeSkillContextAccessor())
    {
    }
}
