using Serious.Abbot.Infrastructure.Security;

namespace Serious.Abbot.Extensions
{
    public static class StartupExtensions
    {
        public static void AddSkillSecretServices(this IServiceCollection services)
        {
            services.AddTransient<ISkillSecretRepository, SkillSecretRepository>();
            services.AddTransient<IAzureKeyVaultClient, AzureKeyVaultClient>();
            services.AddTransient<IAzureKeyVaultSecretClientFactory, AzureKeyVaultSecretClientFactory>();
        }
    }
}
