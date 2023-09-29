using Azure.Storage.Files.Shares;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.Compilation.Cache.Blobs;
using Serious.Abbot.Compilation.Cache.Local;
using Serious.Abbot.Infrastructure.Compilation;
using Serious.Abbot.PageServices;
using Serious.Abbot.Storage.FileShare;

namespace Serious.Abbot.Compilation;

public static class CompilationServiceCollectionExtensions
{
    const string AbbotSkillAssemblyStorage = nameof(AbbotSkillAssemblyStorage);
    const string AbbotAssemblyFileShare = nameof(AbbotAssemblyFileShare);

    // TODO: remove 'globalConfig' parameter when everything is using the new config section.
    public static void AddSkillCompilationServices(this IServiceCollection services, IConfiguration compilationConfigSection, IConfiguration globalConfig)
    {
        services.AddTransient<ISkillCompiler, SkillCompiler>();
        services.AddTransient<ISkillEditorService, SkillEditorService>();
        services.AddTransient<ICachingCompilerService, CachingCompilerService>();
        services.AddTransient<IScriptVerifier, ScriptVerifier>();
        services.AddTransient<IAssemblyCache, AssemblyCache>();
        services.Configure<CompilationOptions>(compilationConfigSection);

        var provider = compilationConfigSection["Provider"]?.ToLowerInvariant();
        if (provider is "blobs")
        {
            // Use blob storage
            services.AddTransient<IAssemblyCacheClient, BlobStorageAssemblyCacheClient>();
        }
        else if (provider is "local")
        {
            // Use local file
            services.AddTransient<IAssemblyCacheClient, LocalAssemblyCacheClient>();
        }
        else
        {
            services.AddTransient<IAssemblyCacheClient, AzureAssemblyCacheClient>();
            services.AddTransient<IShareClient, AzureShareClient>();
            services.AddSingleton(_ => new ShareClient(
                globalConfig.GetConnectionString(AbbotSkillAssemblyStorage),
                globalConfig[AbbotAssemblyFileShare]));
        }

    }
}
