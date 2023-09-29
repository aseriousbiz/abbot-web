using MassTransit;
using MassTransit.Serialization.JsonConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Consumers;
using Serious.Abbot.Eventing.Entities;
using Serious.Abbot.Eventing.Infrastructure;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Eventing.StateMachines;

namespace Serious.Abbot.Eventing;

public static class AbbotEventingExtensions
{
    /// <summary>
    /// Registers all consumers, sagas, activities, etc. in Abbot.Eventing with the provided MassTransit bus.
    /// </summary>
    /// <param name="configurator">The <see cref="IBusRegistrationConfigurator"/> to attach the Abbot Eventing components to.</param>
    public static void AddAbbotEventingConsumers(this IBusRegistrationConfigurator configurator)
    {
        var myAssembly = typeof(AbbotEventingExtensions).Assembly;
        configurator.AddConsumers(myAssembly);
        configurator.AddSagaStateMachines(myAssembly);
        configurator.AddSagas(myAssembly);
        configurator.AddActivities(myAssembly);
    }

    /// <summary>
    /// Registers default config settings common to all participants in the Abbot Event Bus.
    /// </summary>
    /// <param name="configuration">The <see cref="IBusRegistrationConfigurator"/> to attach the Abbot Eventing components to.</param>
    public static void AddAbbotEventingConfig(this IBusRegistrationConfigurator configuration)
    {
        configuration.AddSingleton<IConfigureReceiveEndpoint, ReceiveEndpointConfiguration>();
        configuration.AddSingleton<TopologyCatalog>();
        configuration.AddSingleton<DiagnosticConsumeFilter>();
    }

    public static void AddAbbotSagaConfig(this IBusRegistrationConfigurator configuration)
    {
        configuration.AddSagaStateMachine<PlaybookRunStateMachine, PlaybookRun>()
            .EntityFrameworkRepository(cfg => {
                cfg.ConcurrencyMode = ConcurrencyMode.Optimistic;
                cfg.ExistingDbContext<AbbotContext>();
                cfg.UsePostgres();

                // Make sure we load the playbook run with it's playbook and organization
                // This isn't totally necessary because of PlaybookRunFilter.
                // All messages coming in to the State Machine are `IPlaybookRunMessage` and run through the filter, which loads the Playbook and Organization into the DbContext.
                // Even if the instance of `PlaybookRun` isn't the same, EF Core fixes up the navigation properties.
                // BUT that may change someday, and we need the Saga to always have the Playbook and Organization.
                cfg.CustomizeQuery(q => q
                    .Include(s => s.Playbook.Organization)
                    .Include(s => s.Group));
            });
    }

    public static void ConfigureAbbotTopology(this IBusFactoryConfigurator configuration,
        IBusRegistrationContext context)
    {
        configuration.UseSendFilter(typeof(DiagnosticSendPublishFilter<>), context);
        configuration.UsePublishFilter(typeof(DiagnosticSendPublishFilter<>), context);
        configuration.SendTopology.UseSessionIdFormatter<ISessionFromConversation>(ctx =>
            $"conversation:{ctx.Message.ConversationId}");
        configuration.SendTopology.UseSessionIdFormatter<ISessionFromEntity<Announcement>>(ctx =>
            $"announcement:{ctx.Message.Id}");
        configuration.SendTopology.UseSessionIdFormatter<IPlaybookRunMessage>(ctx =>
            $"playbook-run:{ctx.Message.PlaybookRunId:N}");
        configuration.SendTopology.UseSessionIdFormatter<IPlaybookRunGroupMessage>(ctx =>
            $"playbook-run-group:{ctx.Message.PlaybookRunGroupId:N}");

        // Only allow a single node to process system notifications, to handle deduplication.
        configuration.SendTopology.UseSessionIdFormatter<PublishSystemNotification>(_ =>
            "system_notifications");

        // Configure the SlackEventConsumer as a listener on all slack event queues.
        var queues = SlackEventQueueClient.BusEventRouting.Values.ToHashSet();
        foreach (var queue in queues)
        {
            configuration.ReceiveEndpoint(queue,
                x => {
                    x.ConfigureConsumer<SlackEventConsumer>(context);
                });
        }
    }

    public static void ConfigureAbbotSerialization(this IBusFactoryConfigurator configuration)
    {
        configuration.UseNewtonsoftJsonSerializer();
        configuration.ConfigureNewtonsoftJsonDeserializer(settings => {
            // MassTransit adds a converter to allow messages to be interfaces with get-only properties
            // Unfortunately this doesn't play well with our Slack types, which use interfaces heavily.
            // However, we're not using interfaces for messages, instead we use records with init-only properties.
            // So we can avoid using the converter.
            // This is a little hacky, but we have to do this by replacing the Contract Resolver that MassTransit configures
            // https://github.com/MassTransit/MassTransit/blob/a5ab00032885b58dd49bae949f759020f6702590/src/MassTransit.Newtonsoft/Serialization/NewtonsoftJsonMessageSerializer.cs
            var contractResolver = new JsonContractResolver(
                new ByteArrayConverter(),
                new CaseInsensitiveDictionaryJsonConverter(),
                new InternalTypeConverter(),
                new FilteredInterfaceProxyConverter(),
                new NewtonsoftMessageDataJsonConverter(),
                new StringDecimalConverter(),
                new ProblemDetailsConverter())
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            settings.ContractResolver = contractResolver;
            return settings;
        });

        configuration.ConfigureNewtonsoftJsonSerializer(settings => {
            // For serialization we don't have to be as aggressive as we are for deserialization.
            // We can leave all of MassTransit's default converters in place.
            // We do need to add others though, such as for ProblemDetails
            settings.Converters.Add(new ProblemDetailsConverter());
            return settings;
        });
    }
}
