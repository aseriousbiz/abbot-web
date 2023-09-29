using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Abbot.Scripting.Utilities;

namespace Serious.TestHelpers
{
    public class FakeUsersClient : IUsersClient
    {
        public IUserMessageTarget GetTarget(string id) => new UserMessageTarget(id);
        public Task<AbbotResponse<IUserDetails>> GetUserAsync(string id)
        {
            throw new NotImplementedException();
        }
    }
}
