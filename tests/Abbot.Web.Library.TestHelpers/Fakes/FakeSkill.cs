using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Messaging;
using Serious.Abbot.Skills;

namespace Serious.TestHelpers
{
    public class FakeSkill : ISkill
    {
        public FakeSkill(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
        {
            ReceivedMessageContext = messageContext;
            return Task.CompletedTask;
        }

        [MemberNotNullWhen(true, nameof(ReceivedMessageContext))]
        public bool OnMessageActivityAsyncCalled => ReceivedMessageContext is not null;

        public void BuildUsageHelp(UsageBuilder usage)
        {
        }

        public MessageContext? ReceivedMessageContext { get; private set; }
    }
}
