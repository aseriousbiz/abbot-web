using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Services.DefaultResponder;

/// <summary>
/// When Abbot is mentioned, but no command is given (or the command does not match a skill),
/// this responder will be used.
/// </summary>
public interface IDefaultResponderService
{
    /// <summary>
    /// Retrieves a response to the given message.
    /// </summary>
    /// <param name="message">The message to Abbot.</param>
    /// <param name="address">The address of the person mentioning Abbot.</param>
    /// <param name="member">The member making the request.</param>
    /// <param name="organization">The message <see cref="Organization"/>.</param>
    /// <returns>A string with the default response.</returns>
    Task<string> GetResponseAsync(string message, string? address, Member member, Organization organization);
}
