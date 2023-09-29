using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Infrastructure.AzurePostgres;

namespace Serious.Abbot.Pages.Staff.Tools;

public class QueryDetailsModel : StaffToolsPage
{
    readonly IQueryStoreRepository _queryStore;

    public QueryDetailsModel(IQueryStoreRepository queryStore)
    {
        _queryStore = queryStore;
    }

    public bool Enabled { get; private set; }

    [FromRoute]
    public string Db { get; set; } = null!;

    [FromQuery]
    public QueryStoreFilter Filter { get; set; } = new();

    public IReadOnlyList<QuerySummary> TopQueries { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Enabled = _queryStore.Enabled;
        TopQueries = await _queryStore.GetTopQueries(Db, Filter);
    }
}
