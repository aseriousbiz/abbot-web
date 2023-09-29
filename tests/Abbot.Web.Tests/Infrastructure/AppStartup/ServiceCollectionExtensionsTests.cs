using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Services;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    public class TheRegisterAllTypesInSameAssemblyMethod
    {
        [Fact]
        public void RegistersDataSeeders()
        {
            var services = new ServiceCollection();
            services.RegisterAllTypesInSameAssembly<IDataSeeder>();

            Assert.Contains(services, s => s.ImplementationType == typeof(RecurringJobsSeeder));
            Assert.Contains(services, s => s.ImplementationType == typeof(RoleSeeder));
            Assert.Contains(services, s => s.ImplementationType == typeof(ScheduledTaskFixupSeeder));
            Assert.Contains(services, s => s.ImplementationType == typeof(SkillRecompilationSeeder));
        }
    }
}
