namespace Serious.Abbot;

/// <summary>
/// The scope of the skill data item. The default is organization, which is what all existing skills already assume
/// - skill brain data is accessible in every skill execution regardless of who runs it or where, in a specific organization.
/// </summary>
public enum SkillDataScope
{
    Organization = 0,
    Room = 1,
    Conversation = 2,
    User = 3,
}
