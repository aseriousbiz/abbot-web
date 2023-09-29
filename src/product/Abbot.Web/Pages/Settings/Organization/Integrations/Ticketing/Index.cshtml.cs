using System.Threading.Tasks;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.Ticketing
{
    public class IndexPage : MultipleIntegrationPageBase<TicketingSettings>
    {
        public IndexPage(IIntegrationRepository integrationRepository)
            : base(integrationRepository)
        {
        }

        public string IntegrationName =>
            Settings.IntegrationName;

        public async Task OnGetAsync()
        {
        }
    }
}
