using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serious.Abbot.Infrastructure.AzurePostgres;

namespace Microsoft.Extensions.DependencyInjection;

public static class AzurePostgresServiceCollectionExtensions
{
    public static void AddAzurePostgresServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AzureSysContext.AzureSysContextConnection);
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<AzureSysContext>(options => {
#if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
#endif

                options.UseNpgsql(connectionString);

                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
            });

            services.AddScoped<IQueryStoreRepository, QueryStoreRepository>();
        }
        else
        {
            services.AddSingleton<IQueryStoreRepository, DisabledQueryStoreRepository>();
        }
    }
}
