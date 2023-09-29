using System;
using Serious.Abbot.Clients;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeSkillRunnerRetryPolicy : SkillRunnerRetryPolicy
{
    public FakeSkillRunnerRetryPolicy() : base(3, _ => TimeSpan.Zero)
    {
    }
}
