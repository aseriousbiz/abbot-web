using System.Threading.Tasks;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to reply with a message to open a ticket.
/// </summary>
public interface ITicketsClient
{
    /// <summary>
    /// Replies with an ephemeral message that displays a button to open a ticket for
    /// each enabled ticketing system.
    /// </summary>
    /// <returns>A Task with an <see cref="IResult"/>.</returns>
    Task<IResult> ReplyWithTicketPromptAsync();
}
