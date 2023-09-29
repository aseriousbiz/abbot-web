using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Serious.EntityFrameworkCore.ValueConverters;

namespace Serious.Abbot.Entities;

// ReSharper disable once UnusedType.Global
public class AbbotContextFactory : IDesignTimeDbContextFactory<AbbotContext>
{
    public AbbotContext CreateDbContext(string[] args)
    {
        // Used only for EF .NET Core CLI tools (update database/migrations etc.)
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory()))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "appsettings.Development.json", optional: true, reloadOnChange: true);
        var configuration = builder.Build();
        var optionsBuilder = new DbContextOptionsBuilder<AbbotContext>()
            .UseNpgsql(configuration.GetConnectionString(AbbotContext.ConnectionStringName).Require(),
                options => {
                    options.MigrationsAssembly(WebConstants.MigrationsAssembly);
                    options.UseNetTopologySuite();
                });
        return new AbbotContext(optionsBuilder.Options, new DesignTimeDataProtectionProvider(), IClock.System);
    }
}
