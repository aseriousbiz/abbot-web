using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Settings.Organization.Integrations.Ticketing;
using Xunit;

public class TicketingIndexPageTests : PageTestBase<IndexPage>
{
    public class TheOnGetAsyncMethod : TicketingIndexPageTests
    {
        [Theory]
        [InlineData(null)] // Not set
        [InlineData("o")] // Not int
        [InlineData("0")] // Not found
        [InlineData("1")] // Not Ticketing
        [InlineData("2")] // Wrong Org
        public async Task ReturnsNotFoundGivenInvalidId(string? id)
        {
            var wrongType = await Env.Integrations.EnsureIntegrationAsync(
                Env.TestData.Organization,
                IntegrationType.Zendesk);
            Assert.Equal(1, wrongType.Id);

            var wrongOrg = await Env.Integrations.EnsureIntegrationAsync(
                Env.TestData.ForeignOrganization,
                IntegrationType.Ticketing);
            Assert.Equal(2, wrongOrg.Id);

            if (id is not null)
            {
                PageContext.RouteData.Values["id"] = id;
            }

            var (_, result) = await InvokePageAsync(p => p.OnGetAsync());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsPageGivenValidId()
        {
            var integration = await Env.Integrations.EnsureIntegrationAsync(
                Env.TestData.Organization,
                IntegrationType.Ticketing);
            Assert.Equal(1, integration.Id);

            PageContext.RouteData.Values["id"] = integration.Id.ToString();

            var (_, result) = await InvokePageAsync(p => p.OnGetAsync());

            Assert.IsType<PageResult>(result);
        }
    }
}
