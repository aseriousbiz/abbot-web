namespace Serious.Abbot.Playbooks;

/// <summary>
/// Bundles a Sequence ID and Action ID together to allow for convenient lookup of a single action in a playbook.
/// Action IDs should be unique across the entire Playbook,
/// but this allows us to avoid having to search every sequence for a given action.
/// </summary>
/// <param name="SequenceId">The ID of the Sequence containing the Action.</param>
/// <param name="ActionId">The ID of the Action.</param>
/// <param name="ActionIndex">The ordinal index of the action in the Sequence.</param>
public record ActionReference(string SequenceId, string ActionId, int ActionIndex);
