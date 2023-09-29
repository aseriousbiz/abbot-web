using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class MessagingStartupExtensions
{
    public static void AddMessagingServices(this IServiceCollection services)
    {
        services.AddSingleton<Reactor>();
        services.AddTransient<ITurnContextTranslator, TurnContextTranslator>();
        services.AddTransient<ISlackResolver, SlackResolver>();
        services.AddTransient<IMessageFormatter, SlackMessageFormatter>();
        services.AddTransient<IProactiveMessenger, ProactiveMessenger>();
        services.AddTransient<IMessageDispatcher, SlackMessageDispatcher>();
    }
}
