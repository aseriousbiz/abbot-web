using Microsoft.Extensions.DependencyInjection;
using Refit;
using Serious.Slack;
using Xunit;

public class StartupExtensionsTests
{
    public class TheAddSlackApiClientMethod
    {
        [Fact]
        public void CreatesServiceForISlackApiClient()
        {
            var services = new ServiceCollection();
            services.AddSlackApiClient();
            var provider = services.BuildServiceProvider(false);

            var serializer = provider.GetService<ISlackApiClient>();

            Assert.NotNull(serializer);
        }

        [Fact]
        public void CreatesSettingsForISlackApiClient()
        {
            var services = new ServiceCollection();
            services.AddSlackApiClient();
            var provider = services.BuildServiceProvider(false);

            var settings = provider.GetService<SettingsFor<ISlackApiClient>>();

            Assert.NotNull(settings);
        }
    }
}
