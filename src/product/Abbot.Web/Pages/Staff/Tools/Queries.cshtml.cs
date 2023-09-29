using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Infrastructure.AzurePostgres;

namespace Serious.Abbot.Pages.Staff.Tools
{
    public class QueriesModel : StaffToolsPage
    {
        readonly IQueryStoreRepository _queryStore;

        public QueriesModel(IQueryStoreRepository queryStore)
        {
            _queryStore = queryStore;
        }

        public bool Enabled { get; private set; }

        public IReadOnlyList<DatabaseQuerySummary> DatabaseSummaries { get; private set; } = null!;

        public async Task OnGetAsync()
        {
            Enabled = _queryStore.Enabled;
            DatabaseSummaries = await _queryStore.GetDatabaseSummaries();
        }
    }
}
