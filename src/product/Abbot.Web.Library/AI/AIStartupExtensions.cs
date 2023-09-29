using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.AI.Commands;
using Serious.Abbot.AI.Responder;
using Serious.Abbot.AI.Templating;

namespace Serious.Abbot.AI;

public static class AIStartupExtensions
{
    public static void AddAbbotAIServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CognitiveServicesOptions>(configuration.GetSection("CognitiveServices"));
        services.Configure<AzureOpenAIOptions>(configuration.GetSection("AzureOpenAI"));

        services.AddSingleton<IOpenAIClient, OpenAIClient>();
        services.AddSingleton<IAzureOpenAIClient, AzureOpenAIClient>();
        services.AddSingleton<ITextAnalyticsClient, AzureCognitiveServicesTextAnalyticsClient>();

        services.AddScoped<Summarizer>();
        services.AddScoped<SummarizePromptBuilder>();
        services.AddScoped<IMessageClassifier, MessageClassifier>();
        services.AddScoped<AISettingsRegistry>();
        services.AddScoped<ArgumentRecognizer>();

        services.AddSingleton(CreateCommandRegistry());
        services.AddSingleton<CommandParser>();
        services.AddSingleton<PromptCompiler>();
        services.AddScoped<CommandExecutor>();
        services.AddScoped<MagicResponder>();
    }

    static CommandRegistry CreateCommandRegistry()
    {
        var registry = new CommandRegistry();
        registry.RegisterCommands(typeof(Command).Assembly.GetTypes());
        return registry;
    }
}
