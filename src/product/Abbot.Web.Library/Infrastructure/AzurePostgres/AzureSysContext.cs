using Microsoft.EntityFrameworkCore;

namespace Serious.Abbot.Infrastructure.AzurePostgres;

public class AzureSysContext : DbContext
{
    public const string AzureSysContextConnection = nameof(AzureSysContextConnection);

    public AzureSysContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<QueryStats> QueryStats => Set<QueryStats>();
}
