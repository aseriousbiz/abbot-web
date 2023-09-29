using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serious.Abbot.Infrastructure.AzurePostgres;

#pragma warning disable IDE1006 // Naming Styles
public record QueryStoreFilter(
    string? user = null,
    bool system = false,
    string? include = null,
    string? exclude = null,
    int? n = null
)
{ }
#pragma warning restore IDE1006 // Naming Styles

public interface IQueryStoreRepository
{
    bool Enabled { get; }

    Task<IReadOnlyList<DatabaseQuerySummary>> GetDatabaseSummaries();

    Task<IReadOnlyList<QuerySummary>> GetTopQueries(string db, QueryStoreFilter filter);
}

public class DisabledQueryStoreRepository : IQueryStoreRepository
{
    public bool Enabled => false;

    public Task<IReadOnlyList<DatabaseQuerySummary>> GetDatabaseSummaries() =>
        Task.FromResult<IReadOnlyList<DatabaseQuerySummary>>(new List<DatabaseQuerySummary>());

    public Task<IReadOnlyList<QuerySummary>> GetTopQueries(string db, QueryStoreFilter filter) =>
        Task.FromResult<IReadOnlyList<QuerySummary>>(new List<QuerySummary>());
}
