using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class ServiceBusSubscriptionCleanupJob : IRecurringJob
{
    readonly ILogger<ServiceBusSubscriptionCleanupJob> _logger;
    readonly ServiceBusAdministrationClient? _serviceBusClient;

    // Any queues in this list will be deleted if present.
    // Use this to remove queues we no longer use.
    // Remember to include the "_error" and "_skipped" sub-queues.
    static readonly List<string> QueuesToClean = new()
    {
        "playbook-runner", // We have a "-v2" version of this queue now.
        "step-runner", // We have a "-v2" version of this queue now.
        "sync-conversation-with-hub",
        "sync-conversation-with-hub_error",
        "sync-conversation-with-hub_skipped",
        "auto-summarization", // We have a "-v2" version of this queue now.
        "auto-summarization_error",
        "auto-summarization_skipped",
        "playbook-run-state", // Old name of Playbook State Machine Saga
        "playbook-run-state_error",
        "playbook-run-state_skipped",
    };

    public static string Name => "Azure Service Bus Cleanup";

    public ServiceBusSubscriptionCleanupJob(ILogger<ServiceBusSubscriptionCleanupJob> logger, IServiceProvider services)
    {
        _logger = logger;
        _serviceBusClient = services.GetService<ServiceBusAdministrationClient>();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Only registered if configured
        if (_serviceBusClient is null)
        {
            return;
        }

        var allQueues = (await _serviceBusClient.GetQueuesAsync(cancellationToken).ToListAsync())
            .ToDictionary(q => q.Name, StringComparer.OrdinalIgnoreCase);

        var queueList = string.Join(",", allQueues.Keys.OrderBy(k => k));
        _logger.ScanningForOrphanedSubscriptions(queueList);

        await foreach (var topic in _serviceBusClient.GetTopicsAsync(cancellationToken))
        {
            await foreach (var subscription in _serviceBusClient.GetSubscriptionsAsync(topic.Name, cancellationToken))
            {
                if (subscription.ForwardTo is not { Length: > 0 })
                {
                    _logger.SkippingMissingForwardTo(subscription.SubscriptionName, topic.Name);
                    continue;
                }

                // Parse the queue name out of the forward to url
                // The ForwardTo from the _API_ is just the queue name, which is what we have in our dictionary.
                // But the SDK "conveniently" makes it in to a URL with the Service Bus endpoint in it 🙄
                if (!Uri.TryCreate(subscription.ForwardTo, UriKind.Absolute, out var forwardTo))
                {
                    _logger.SkippingInvalidForwardTo(subscription.SubscriptionName, topic.Name, subscription.ForwardTo);
                    continue;
                }

                // The last path segment should be the queue name
                var queueName = forwardTo.Segments.Last().TrimStart('/');

                // Does the queue exist?
                if (allQueues.ContainsKey(queueName))
                {
                    // Yes, it does. Nothing to do here.
                    _logger.SkippingExistingQueue(subscription.SubscriptionName, topic.Name, queueName);
                    continue;
                }

                // The queue doesn't exist, delete the subscription.
                _logger.DeletingOrphanedSubscription(subscription.SubscriptionName, topic.Name, subscription.ForwardTo);
                await _serviceBusClient.DeleteSubscriptionAsync(topic.Name, subscription.SubscriptionName, cancellationToken);
            }
        }

        foreach (var queue in QueuesToClean.Where(q => allQueues.ContainsKey(q)))
        {
            // The easiest way to "delete if exists" is to just delete, and ignore the error if it doesn't exist.
            try
            {
                await _serviceBusClient.DeleteQueueAsync(queue, cancellationToken);
                _logger.ServiceBusQueueDeleted(queue);
            }
            catch (ServiceBusException sbex) when (sbex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                // Ignore!
                _logger.ServiceBusQueueDoesNotExist(queue);
            }
        }
    }
}

static partial class ServiceBusSubscriptionCleanupJobLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Scanning for orphaned subscriptions. Existing queue names: {ExistingQueues}")]
    public static partial void ScanningForOrphanedSubscriptions(this ILogger<ServiceBusSubscriptionCleanupJob> logger,
        string existingQueues);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Deleting orphaned subscription '{SubscriptionName}' in topic '{TopicName}' referring to removed queue '{QueueName}'")]
    public static partial void DeletingOrphanedSubscription(this ILogger<ServiceBusSubscriptionCleanupJob> logger,
        string subscriptionName, string topicName, string queueName);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Skipping subscription '{SubscriptionName}' in topic '{TopicName}' with invalid ForwardTo value '{ForwardTo}'")]
    public static partial void SkippingInvalidForwardTo(this ILogger<ServiceBusSubscriptionCleanupJob> logger,
        string subscriptionName, string topicName, string forwardTo);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Skipping subscription '{SubscriptionName}' in topic '{TopicName}' with no ForwardTo value")]
    public static partial void SkippingMissingForwardTo(this ILogger<ServiceBusSubscriptionCleanupJob> logger,
        string subscriptionName, string topicName);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Skipping subscription '{SubscriptionName}' in topic '{TopicName}' referring to existing queue '{QueueName}'")]
    public static partial void SkippingExistingQueue(this ILogger<ServiceBusSubscriptionCleanupJob> logger,
        string subscriptionName, string topicName, string queueName);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Service bus queue '{QueueName}' deleted.")]
    public static partial void ServiceBusQueueDeleted(this ILogger<ServiceBusSubscriptionCleanupJob> logger, string queueName);

    [LoggerMessage(
        EventId = 7,
        // This is OK to have as informational because it's only logged if the queue doesn't exist BUT DID when we scanned for queues earlier in the job.
        // So it's rare, but benign.
        Level = LogLevel.Information,
        Message = "Service bus queue '{QueueName}' does not exist.")]
    public static partial void ServiceBusQueueDoesNotExist(this ILogger<ServiceBusSubscriptionCleanupJob> logger, string queueName);
}
