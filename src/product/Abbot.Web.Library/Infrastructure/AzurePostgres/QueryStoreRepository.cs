using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Serious.Abbot.Infrastructure.AzurePostgres;

public class QueryStoreRepository : IQueryStoreRepository
{
    private readonly AzureSysContext _db;

    public QueryStoreRepository(AzureSysContext db)
    {
        _db = db;
    }

    public bool Enabled => true;

    public async Task<IReadOnlyList<DatabaseQuerySummary>> GetDatabaseSummaries()
    {
        return await _db.QueryStats
            .GroupBy(qs => qs.Database!.datname)
            .Select(g => new DatabaseQuerySummary
            {
                datname = g.Key,
                calls = g.Sum(qs => qs.calls),
                total_time = g.Sum(qs => qs.total_time),
                min_time = g.Min(qs => qs.min_time),
                max_time = g.Max(qs => qs.max_time),
                mean_time = g.Sum(qs => qs.total_time) / g.Sum(qs => qs.calls),
                rows = g.Sum(qs => qs.rows),
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<QuerySummary>> GetTopQueries(string db, QueryStoreFilter filter)
    {
        var query = _db.QueryStats.Where(qs => qs.Database!.datname == db);

        if (filter.user != null)
            query = query.Where(qs => qs.User!.usename == filter.user);

        if (!filter.system)
            query = query.Where(qs => qs.is_system_query != true);

        if (!string.IsNullOrEmpty(filter.include))
            query = query.Where(qs => Regex.IsMatch(qs.query_sql_text ?? "", filter.include));

        if (!string.IsNullOrEmpty(filter.exclude))
            query = query.Where(qs => !Regex.IsMatch(qs.query_sql_text ?? "", filter.exclude));

        return await query
            .Where(qs => qs.query_id != null)
            .GroupBy(qs => new { qs.User!.usename, qs.query_id, qs.query_type, qs.is_system_query })
            .Select(g => new QuerySummary
            {
                usename = g.Key.usename,
                query_id = g.Key.query_id,
                query_sql_text = g.First().query_sql_text,
                calls = g.Sum(qs => qs.calls),
                total_time = g.Sum(qs => qs.total_time),
                min_time = g.Min(qs => qs.min_time),
                max_time = g.Max(qs => qs.max_time),
                mean_time = g.Sum(qs => qs.total_time) / g.Sum(qs => qs.calls),
                rows = g.Sum(qs => qs.rows),
                query_type = g.Key.query_type,
                is_system_query = g.Key.is_system_query,
            })
            .OrderByDescending(qs => qs.total_time)
            .ThenBy(qs => qs.calls)
            .Take(filter.n ?? 100)
            .ToListAsync();
    }
}
