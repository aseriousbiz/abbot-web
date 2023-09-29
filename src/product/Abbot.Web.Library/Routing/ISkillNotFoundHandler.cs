using System.Threading.Tasks;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Services;

/// <summary>
/// Service for handling the case when a skill is not found.
/// </summary>
public interface ISkillNotFoundHandler
{
    /// <summary>
    /// Handles the case when a skill is not found.
    /// </summary>
    /// <param name="messageContext">Information about the incoming message.</param>
    Task HandleSkillNotFoundAsync(MessageContext messageContext);
}
