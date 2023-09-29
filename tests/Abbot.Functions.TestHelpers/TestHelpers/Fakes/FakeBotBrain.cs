using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Storage;

namespace Serious.TestHelpers
{
    public class FakeBotBrain : BotBrain
    {
        public FakeBotBrain() : this("FakeAssembly")
        {
        }

        public FakeBotBrain(string assemblyName)
            : this(new FakeBrainApiClient(), new BrainSerializer(new FakeSkillContextAccessor(assemblyName: assemblyName)))
        {

        }

        FakeBotBrain(IBrainApiClient brainApiClient, IBrainSerializer brainSerializer)
            : base(brainApiClient, brainSerializer)
        {
        }
    }
}
