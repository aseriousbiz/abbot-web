using System.Threading.Tasks;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers;

public class FakeTickets : ITicketsClient
{
    public async Task<IResult> ReplyWithTicketPromptAsync()
    {
        return new Result();
    }
}
