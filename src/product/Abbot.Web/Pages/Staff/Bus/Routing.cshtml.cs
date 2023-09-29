using System.Collections.Generic;
using System.Linq;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Newtonsoft.Json;
using Serious.Abbot.Eventing;

namespace Serious.Abbot.Pages.Staff.Bus;

public class RoutingModel : StaffToolsPage
{
    static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    };

    readonly TopologyCatalog _topologyCatalog;
    readonly ServiceBusAdministrationClient? _adminClient;

    public RoutingModel(TopologyCatalog topologyCatalog, IServiceProvider provider)
    {
        _topologyCatalog = topologyCatalog;
        _adminClient = provider.GetService<ServiceBusAdministrationClient>();
    }

    public required string RawJson { get; set; }

    public Uri? NodeAddress { get; set; }

    public HostInfo? Host { get; set; }

    public async Task OnGet()
    {
        var topology = _topologyCatalog.GetTopology();
        RawJson = JsonConvert.SerializeObject(topology, SerializerSettings);

        // Build up a model of the bus status
        if (TryWalkDictionary<IDictionary<string, object>>(topology.Results, "bus") is { } bus)
        {
            NodeAddress = TryWalkDictionary<Uri>(bus, "address");

            if (TryWalkDictionary<IDictionary<string, object>>(bus, "host") is { } host)
            {
                Host = await ProbeHostAsync(host);
            }
        }
    }

    // HERE BE DRAGONS
    // The "Probe" output from the Mass Transit bus is a tangled mess of dictionaries that describes _everything_ in the Bus.
    // So we walk through that dictionary to try and pull out useful things.
    // Unfortunately, the Bus is configured in a way that is optimized for _running_, and doesn't necessarily map directly to how we configured it.
    // For example, a Consumer is attached to a Queue to process messages, but the consumer is just an attribute of the "deserialize" filter that is attached to the Queue.
    // Anyway, it's messy. But we do our best.

    async Task<HostInfo> ProbeHostAsync(IDictionary<string, object> host)
    {
        var hostAddress = TryWalkDictionary<Uri>(host, "hostAddress")?.ToString();
        var type = TryWalkDictionary<string>(host, "type");

        var endpointInfos = new List<QueueInfo>();
        var exchangeInfos = new List<ExchangeInfo>();
        foreach (var endpoint in TryWalkDictionaryList(host, "receiveEndpoint"))
        {
            var (queue, exchange) = await ProbeEndpointAsync(endpoint);
            endpointInfos.Add(queue);
            if (exchange.Count > 0)
            {
                exchangeInfos.AddRange(exchange);
            }
        }

        foreach (var exchange in TryWalkDictionaryList(host, "messageFabric.exchange"))
        {
            exchangeInfos.Add(ProbeExchange(exchange));
        }

        exchangeInfos = exchangeInfos
            // Don't show the "_error" and "_skipped" exchanges unless they're bound to a queue.
            .Where(x => x.Name is null || x is ExchangeWithExchangeSinkInfo or ExchangeWithQueueSinkInfo
                                       || (!x.Name.EndsWith("_error", StringComparison.Ordinal) && !x.Name.EndsWith("_skipped", StringComparison.Ordinal)))
            // Sort "urn:message:" exchanges first. Those are the ones that used when we publish messages.
            .OrderBy(x => x.Name is { } x3 && x3.StartsWith("urn:message:", StringComparison.Ordinal)
                ? 0
                : 1)
            .ThenBy(x => x.Name)
            .ToList();

        return new(hostAddress, type, endpointInfos, exchangeInfos);
    }

    async Task<(QueueInfo, IList<ExchangeInfo>)> ProbeEndpointAsync(IDictionary<string, object> endpoint)
    {
        var name = TryWalkDictionary<string>(endpoint, "name");
        var type = TryWalkDictionary<string>(endpoint, "receiveTransport.type");

        string? address;
        var exchanges = new List<ExchangeInfo>();
        QueueMetadata? queueMetadata = null;
        if (type == "Azure Service Bus")
        {
            var queueName = TryWalkDictionary<string>(endpoint, "receiveTransport.queue.name");
            address = $"sb://.../{queueName}";

            // Load the service bus queue
            try
            {
                var queue = await _adminClient.Require().GetQueueAsync(queueName);
                queueMetadata = new ServiceBusQueueMetadata(
                    queue.Value.RequiresSession,
                    queue.Value.EnablePartitioning,
                    JsonConvert.SerializeObject(queue.Value, Formatting.Indented));
            }
            catch (ServiceBusException)
            {
                // Perhaps the queue just couldn't be found.
            }

            // Also create an Exchange from the "topic" and "queueSubscription" properties.
            var topics = TryWalkDictionaryList(endpoint, "receiveTransport.topic");
            var queueSubscriptions =
                TryWalkDictionaryList(endpoint, "receiveTransport.queueSubscription");

            foreach (var (topic, queueSubscription) in topics.Zip(queueSubscriptions))
            {
                var topicName = TryWalkDictionary<string>(topic, "name");
                var forwardTo = TryWalkDictionary<string>(queueSubscription, "forwardTo");
                exchanges.Add(new ExchangeWithQueueSinkInfo(topicName, "topic", forwardTo));
            }
        }
        else
        {
            address = TryWalkDictionary<Uri>(endpoint, "receiveTransport.address")?.ToString();
        }

        var filterInfos = new List<FilterInfo>();
        foreach (var filter in TryWalkDictionaryList(endpoint, "filters"))
        {
            if (ProbeFilter(filter) is { } f)
            {
                filterInfos.Add(f);
            }
        }

        return (new QueueInfo(name, address, filterInfos, queueMetadata), exchanges);
    }

    static ExchangeInfo ProbeExchange(IDictionary<string, object> exchange)
    {
        var name = TryWalkDictionary<string>(exchange, "name");
        var type = TryWalkDictionary<string>(exchange, "type");

        if (TryWalkDictionary<IDictionary<string, object>>(exchange, "sinks.exchange") is { } exchangeSink)
        {
            var exchangeName = TryWalkDictionary<string>(exchangeSink, "name");
            var queueName = TryWalkDictionary<string>(exchangeSink, "sinks.queue.name");
            return new ExchangeWithExchangeSinkInfo(name, type, exchangeName, queueName);
        }

        if (TryWalkDictionary<IDictionary<string, object>>(exchange, "sinks.queue") is { } queueSink)
        {
            var queueName = TryWalkDictionary<string>(queueSink, "name");
            return new ExchangeWithQueueSinkInfo(name, type, queueName);
        }

        return new(name, type);
    }

    static FilterInfo ProbeFilter(IDictionary<string, object> filter)
    {
        var type = TryWalkDictionary<string>(filter, "filterType");

        string? consumerType = null;
        string? stateMachineType = null;
        if (type == "deserialize")
        {
            // Check for the consumer type, which is in the dispatchPipe filter, if any
            var consumePipeFilters = TryWalkDictionaryList(filter, "consumePipe.filters").ToList();

            if (consumePipeFilters.FirstOrDefault(f => TryWalkDictionary<string>(f, "filterType") == "dispatchPipe") is
                { } dispatchPipeFilter)
            {
                consumerType = TryWalkDictionary<string>(dispatchPipeFilter, "consumer.type");

                var dispatchPipeFilters = TryWalkDictionaryList(dispatchPipeFilter, "filters");

                // There are going to be _several_ "saga" filters, one for each type of message that the queue may receive when processing the saga.
                // But we're just trying to dig out the state machine type, so it doesn't matter.
                if (dispatchPipeFilters.FirstOrDefault(f => TryWalkDictionary<string>(f, "filterType") == "saga") is
                    { } sagaFilter)
                {
                    stateMachineType = TryWalkDictionary<string>(sagaFilter, "stateMachine.name");
                }
            }
        }

        return new FilterInfo(type, consumerType, stateMachineType);
    }

    public record FilterInfo(string? Type, string? ConsumerType, string? StateMachineType);

    public record QueueInfo(string? Name, string? Address, IReadOnlyList<FilterInfo> Filters,
        QueueMetadata? QueueMetadata);

    public record ExchangeInfo(string? Name, string? Type);

    public record ExchangeWithExchangeSinkInfo
        (string? Name, string? Type, string? ExchangeName, string? QueueName) : ExchangeInfo(Name, Type);

    public record ExchangeWithQueueSinkInfo(string? Name, string? Type, string? QueueName) : ExchangeInfo(Name, Type);

    public record HostInfo(string? HostAddress, string? Type, IReadOnlyList<QueueInfo> Endpoints,
        IReadOnlyList<ExchangeInfo> Exchanges);

    public abstract record QueueMetadata(string RawMetadata);

    public record ServiceBusQueueMetadata
        (bool RequiresSession, bool EnablePartitioning, string RawMetadata) : QueueMetadata(RawMetadata);

    static IEnumerable<IDictionary<string, object>> TryWalkDictionaryList(IDictionary<string, object> dict, string path)
    {
        var obj = TryWalkDictionary<object>(dict, path);
        if (obj is IEnumerable<IDictionary<string, object>> list)
        {
            return list;
        }

        if (obj is IDictionary<string, object> singleDict)
        {
            return new[] { singleDict };
        }

        return Array.Empty<IDictionary<string, object>>();
    }

    static T? TryWalkDictionary<T>(IDictionary<string, object> dict, string path)
        where T : notnull
    {
        var pathSegments = path.Split('.');
        object current = dict;
        foreach (var segment in pathSegments)
        {
            if (current is IDictionary<string, object> nextDict && nextDict.TryGetValue(segment, out var next))
            {
                current = next;
            }
            else
            {
                return default;
            }
        }

        return current is T t
            ? t
            : default;
    }
}
