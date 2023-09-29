using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Settings.Organization.Integrations;
using Xunit;

public class IntegrationsIndexPageTests
{
    public class TheOnGetAsyncMethod : PageTestBase<IndexPage>
    {
        [Fact]
        public async Task LoadsIntegrationObjectForEnabledIntegration()
        {
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync());

            Assert.Equal(1, page.Integrations.Count);
            Assert.True(page.Integrations.FirstOrDefault(i => i.Type == IntegrationType.Zendesk)?.Enabled);
        }

        [Fact]
        public async Task LoadsIntegrationObjectForDisabledIntegration()
        {
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.DisableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync());

            Assert.Equal(1, page.Integrations.Count);
            Assert.False(page.Integrations.FirstOrDefault(i => i.Type == IntegrationType.Zendesk)?.Enabled);
        }

        [Fact]
        public async Task IgnoresIntegrationTypeWithNoRecord()
        {
            var (page, _) = await InvokePageAsync(p => p.OnGetAsync());

            Assert.Empty(page.Integrations);
        }
    }
}
