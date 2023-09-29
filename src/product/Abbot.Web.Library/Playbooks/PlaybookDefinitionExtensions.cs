using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Serious.Abbot.Playbooks;

public static class PlaybookDefinitionExtensions
{
    /// <summary>
    /// Attempts to retrieve an <see cref="ActionStep"/> from the playbook definition.
    /// </summary>
    /// <param name="definition">The <see cref="PlaybookDefinition"/> to search for the action.</param>
    /// <param name="actionReference">An <see cref="ActionReference"/> identifying the action to retrieve.</param>
    /// <param name="action">The <see cref="ActionStep"/> that was found, or <c>null</c> if the action could not be found.</param>
    /// <returns>A boolean indicating if the action was found. If <c>true</c>, the <paramref name="action"/> parameter is guaranteed to be non-<c>null</c></returns>
    public static bool TryGetAction(this PlaybookDefinition definition, ActionReference actionReference, [NotNullWhen(true)] out ActionStep? action)
    {
        if (!definition.Sequences.TryGetValue(actionReference.SequenceId, out var sequence))
        {
            action = null;
            return false;
        }

        action = sequence.Actions
            .FirstOrDefault(a => string.Equals(a.Id, actionReference.ActionId, StringComparison.OrdinalIgnoreCase));
        return action is not null;
    }
}
