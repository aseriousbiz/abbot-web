using System.Diagnostics;
using MassTransit;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;

namespace Serious.Abbot.Eventing.StateMachines;

public static class PlaybookStateMachineExtensions
{
    /// <summary>
    /// Gets the playbook definition from the saga, caching it in the <see cref="BehaviorContext{TSaga}"/>, so that other activities within a single invocation could use it.
    /// </summary>
    public static PlaybookDefinition GetPlaybookDefinition(this BehaviorContext<PlaybookRun> context)
    {
        return context.GetOrAddPayload(() => {
            // Load the playbook definition
            var def = PlaybookFormat.Deserialize(context.Saga.SerializedDefinition);
            if (PlaybookFormat.Validate(def) is { Count: > 0 } errors)
            {
                throw new UnreachableException("Invalid Playbook: " + string.Join(", ", errors));
            }

            return def;
        });
    }
}
