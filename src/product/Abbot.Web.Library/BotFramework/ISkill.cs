using System.Threading;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Skills;

/// <summary>
/// Represents an Abbot Skill that's callable by the user.
/// </summary>
public interface ISkill
{
    /// <summary>
    /// A chat skill supported by this Abbot instance such as .haack me. Skills are invoked via `.` by default
    /// (configurable per org). This method also handles interactions with UI elements within a message (as opposed
    /// to UI elements in a view).
    /// </summary>
    /// <param name="messageContext">Information about the chat message the skill will respond to.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken);

    /// <summary>
    /// Used to build the help text for this skill.
    /// </summary>
    void BuildUsageHelp(UsageBuilder usage);
}
