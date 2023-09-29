using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Services.DefaultResponder;

namespace Serious.TestHelpers
{
    public class FakeDefaultResponderService : IDefaultResponderService
    {
        public bool GetResponseAsyncCalled { get; private set; }

#nullable enable
        public Task<string> GetResponseAsync(string message, string? address, Member member, Organization organization)
        {
            GetResponseAsyncCalled = true;
            return Task.FromResult($"You want the answer to: {message}");
        }
#nullable disable
    }
}
