using System;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Skills;

/// <summary>
/// A skill designed to fail. Helps us test that our error logging is working.
/// </summary>
[Skill("throw-exception", Description = "Throws an exception. Used for testing.", Hidden = true)]
public sealed class FailSkill : ISkill
{
    public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("FAIL ON PURPOSE!");
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
    }
}
