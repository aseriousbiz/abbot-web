using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot;
using Serious.Abbot.Clients;
using Serious.Abbot.Events;
using Serious.Abbot.Metadata;
using Serious.Abbot.PayloadHandlers;
using Serious.TestHelpers;
using Xunit;

public class StartupExtensionsTests
{
    public class TheRegisterAllBuiltInSkillsMethod
    {
        [Fact]
        public void FindsTheBuiltInSkills()
        {
            var services = new ServiceCollection();

            services.RegisterAllBuiltInSkills();

            var provider = services.BuildServiceProvider(false);
            var skills = provider.GetServices<IBuiltinSkillDescriptor>().ToList();

            Assert.NotEmpty(skills);
            Assert.Contains(skills, skill => skill.Name == "help");
        }
    }

    public class TheRegisterAllPayloadHandlerMethod
    {
        [Fact]
        public void FindsThePayloadHandlers()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IBackgroundSlackClient>(new FakeBackgroundSlackClient());
            services.RegisterAllHandlers();

            var provider = services.BuildServiceProvider(false);
            var handler = provider.GetService<IPayloadHandler<TeamChangeEventPayload>>();
            Assert.IsType<TeamChangePayloadHandler>(handler);
        }
    }
}
