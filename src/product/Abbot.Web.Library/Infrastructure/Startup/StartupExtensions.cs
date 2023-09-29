using System.Linq;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.AppStartup;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Clients;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Middleware;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Messaging.Slack;
using Serious.Abbot.Metadata;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Rooms;
using Serious.Abbot.Routing;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Serious.Abbot.Signals;
using Serious.Abbot.Skills;
using Serious.Slack.AspNetCore;
using Serious.Slack.BotFramework;
using IBot = Microsoft.Bot.Builder.IBot;

namespace Serious.Abbot;

public static class StartupExtensions
{
    public static void AddBotServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Abstractions
        services.AddHttpClient<ISkillRunnerClient, SkillRunnerClient>();
        services.AddSingleton<ISkillRunnerRetryPolicy, SkillRunnerRetryPolicy>();

        services.AddScoped<ISlackAuthenticator, SlackAuthenticator>();
        services.AddTransient<DebugMiddleware>();
        services.AddTransient<MessageFormatMiddleware>();
        services.AddTransient<DiagnosticMiddleware>();

        services.AddDatabaseAndRepositoryServices(configuration);
        services.AddMessagingServices();
        services.AddTransient<OpenTicketMessageBuilder>();
        services.AddTransient<ISkillNotFoundHandler, SkillNotFoundHandler>();
        services.AddTransient<ISkillManifest, SkillManifest>();
        services.AddTransient<ISkillRouter, SkillRouter>();
        services.AddTransient<ISkillPatternMatcher, SkillPatternMatcher>();
        services.AddTransient<IBuiltinSkillRegistry, BuiltinSkillRegistry>();
        services.AddTransient<IApiTokenFactory, ApiTokenFactory>();
        services.AddTransient<ISignalHandler, SignalHandler>();
        services.AddTransient<ISystemSignaler, SystemSignaler>();

        // Create the Bot Framework Adapter with error handling enabled.
        services.AddScoped<IBotFrameworkHttpAdapter, BotFrameworkAdapterWithErrorHandler>();
        services.AddScoped<IBotFrameworkAdapter, SlackAdapterWithErrorHandler>();
        services.AddSingleton<SlackEventDeduplicator>();
        services.AddTransient<IEventQueueClient, SlackEventQueueClient>();
        services.AddTransient<SlackEventProcessor>();
        services.AddSlackAdapterServices(configuration);
        // Default provider can't handle custom Slack App
        services.AddScoped<ISlackOptionsProvider, CustomSlackOptionsProvider>();
        services.AddScoped<ISlackIntegration, SlackIntegration>();
        // Singleton can't depend on scoped provider
        services.AddScoped<SlackRequestVerificationFilter>();

        // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
        services.AddTransient<IBot, MetaBot>();

        services.AddConversationTracking();
        services.AddTransient<IRoomJoiner, RoomJoiner>();
    }

    public static void RegisterAllBuiltInSkills(this IServiceCollection services)
    {
        var typesFromAssemblies = ReflectionExtensions
            .GetInstantiableTypesThatImplement<ISkill>(typeof(HelpSkill).Assembly);

        foreach (var type in typesFromAssemblies)
        {
            services.Add(
                new ServiceDescriptor(type,
                    type,
                    ServiceLifetime.Transient));

            services.Add(
                new ServiceDescriptor(typeof(IBuiltinSkillDescriptor),
                    sp => new BuiltinSkillDescriptor(type, new Lazy<ISkill>(() => (ISkill)sp.GetRequiredService(type))),
                    ServiceLifetime.Transient));
        }
    }

    public static void RegisterAllHandlers(this IServiceCollection services)
    {
        services.RegisterAllTypesInSameAssembly<IHandler>();
        services.AddTransient<IPayloadHandlerRegistry, PayloadHandlerRegistry>();
        services.AddTransient<SharedChannelInviteHandler>();
        services.AddTransient<IHandlerRegistry, HandlerRegistry>();
        services.AddTransient<IHandlerDispatcher, HandlerDispatcher>();

        var typesFromAssemblies = typeof(IPayloadHandler<>)
            .Assembly
            .GetTypes()
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPayloadHandler<>)));

        foreach (var concretePayloadHandlerType in typesFromAssemblies)
        {
            // A concrete type can implement multiple `IPayloadHandler` interfaces.
            var payloadHandlerInterfaceTypes = concretePayloadHandlerType
                .GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPayloadHandler<>))
                .Select(type => type.GenericTypeArguments);

            foreach (var typeArgs in payloadHandlerInterfaceTypes)
            {
                var interfaceType = typeof(IPayloadHandler<>).MakeGenericType(typeArgs);

                services.Add(
                    new ServiceDescriptor(
                        interfaceType,
                        concretePayloadHandlerType,
                        ServiceLifetime.Transient));
            }
        }

        // Some special case handler instances. It's fine that these are double-registered (once under IHandler, once under the concrete types)
        services.AddTransient<AppHomePageHandler>();
    }
}
