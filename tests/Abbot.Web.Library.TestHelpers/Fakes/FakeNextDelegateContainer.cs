using System.Threading.Tasks;
using Microsoft.Bot.Builder;

namespace Serious.TestHelpers
{
    public class FakeNextDelegateWrapper
    {
        public FakeNextDelegateWrapper()
        {
            NextDelegate = _ => {
                Called = true;
                return Task.CompletedTask;
            };

        }

        public bool Called { get; private set; }

        public NextDelegate NextDelegate { get; }
    }
}
