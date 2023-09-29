using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using MassTransit;
using MassTransit.SignalR;
using MassTransit.SignalR.Configuration.Definitions;
using MassTransit.SignalR.Consumers;
using MassTransit.SignalR.Contracts;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Serious.Abbot.Eventing;
using Serious.Abbot.Eventing.Entities;
using Serious.Abbot.Live;

namespace Serious.Abbot.Infrastructure;

public static class MassTransitConfig
{
    /// <summary>
    /// Configures the Abbot Worker service, either embedded within Abbot.Web, or as a standalone worker.
    /// </summary>
    /// <param name="services">The service collection to register Mass Transit in.</param>
    /// <param name="eventingSection">The "Eventing" section of the application configuration.</param>
    public static void AddMassTransitConfig(this IServiceCollection services, IConfiguration eventingSection)
    {
        services.Configure<EventingOptions>(eventingSection);

        // We can't access the IOptions<EventingOptions> until the container is built,
        // but we need it to make decisions about how to configure the container.
        // So we'll bind it directly to an object here as well.
        var eventOptions = new EventingOptions();
        eventingSection.Bind(eventOptions);

        services.Configure<MassTransitHostOptions>(options => {
            // Don't allow the host to finish starting until the Bus has started.
            options.WaitUntilStarted = true;
        });

        services.AddMassTransit(x => {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddAbbotEventingConsumers();
            x.AddAbbotEventingConfig();
            x.AddAbbotSagaConfig();

            // Here in the web app, we can register the SignalR Hubs
            x.AddSignalRHub<FlashHub>();

            // Override the group consumer definition
            x.AddSingleton<IConsumerDefinition<GroupConsumer<FlashHub>>, FlashHubConsumerDefinition>();

            switch (eventOptions.Transport)
            {
                case EventingOptions.InMemoryTransport:
                    ConfigureInMemory(x, eventOptions.Scheduler);
                    break;
                case EventingOptions.AzureServiceBusTransport:
                    ConfigureAzureServiceBus(x, eventOptions);
                    break;
                case { } t:
                    throw new UnreachableException($"Unknown transport type: '{t}'. Supported types are: InMemory, AzureServiceBus");
            }
        });
    }

    static void ConfigureAzureServiceBus(IBusRegistrationConfigurator config, EventingOptions options)
    {
        // Register the service bus client using AddAzureClients to participate in dependency injection and configuration.
        var endpointUrl = new Uri(options.Endpoint.Require("When using Azure Service Bus, the 'Eventing:Endpoint' configuration setting must be set."));
        var ns = endpointUrl.Host;
        config.AddAzureClients(builder => {
            // "WithNamespace" just means we're giving it a namespace name (to use with Managed Identity) instead of a Connection String.
            builder.AddServiceBusClientWithNamespace(ns);
            builder.AddServiceBusAdministrationClientWithNamespace(ns);
        });

        config.AddServiceBusMessageScheduler();
        config.UsingAzureServiceBus((context, cfg) => {
            cfg.UseServiceBusMessageScheduler();
            cfg.ConfigureAbbotSerialization();
            cfg.ConfigureAbbotTopology(context);

            cfg.Host(
                endpointUrl,
                context.GetRequiredService<ServiceBusClient>(),
                context.GetRequiredService<ServiceBusAdministrationClient>());

            cfg.ConfigureEndpoints(context);
            cfg.UseAbbotFilters(context);
        });
    }

    static void ConfigureInMemory(IBusRegistrationConfigurator config, bool scheduler)
    {
        config.SetInMemorySagaRepositoryProvider();

        if (scheduler)
        {
            config.AddHangfireConsumers();
        }

        config.UsingInMemory((context, cfg) => {
            if (scheduler)
            {
                cfg.UsePublishMessageScheduler();
            }

            cfg.ConfigureAbbotSerialization();
            cfg.ConfigureAbbotTopology(context);

            cfg.ConfigureEndpoints(context);
            cfg.UseAbbotFilters(context);
        });
    }

    public static void UseAbbotFilters(this IConsumePipeConfigurator cfg, IBusRegistrationContext context)
    {
        cfg.UseConsumeFilter(typeof(PlaybookRunFilter<>), context);
        cfg.UseConsumeFilter(typeof(OrganizationFilter<>), context);
    }
}

public class FlashHubConsumerDefinition : GroupConsumerDefinition<FlashHub>
{
    public FlashHubConsumerDefinition(HubConsumerDefinition<FlashHub> endpointDefinition) : base(endpointDefinition)
    {
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<GroupConsumer<FlashHub>> consumerConfigurator)
    {
        if (endpointConfigurator is IServiceBusReceiveEndpointConfigurator sbCfg)
        {
            // Flashes are transient.
            sbCfg.DefaultMessageTimeToLive = TimeSpan.FromMinutes(5);

            // Make sure we clean up the Subscription when we shut down.
            sbCfg.RemoveSubscriptions = true;
        }
    }
}
