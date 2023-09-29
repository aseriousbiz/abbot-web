using MassTransit.Serialization.JsonConverters;
using Newtonsoft.Json;

namespace Serious.Abbot.Eventing.Infrastructure;

// We removed the original InterfaceProxyGenerator converter because it broke on our interface types.
// However, MassTransit uses interface types for internal messages, such as for SignalR support and request timeout handling.
// So this allows certain interface types to have proxies generated for them.
public class FilteredInterfaceProxyConverter :
    InterfaceProxyConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }

    protected override IConverter ValueFactory(Type objectType)
    {
        if (objectType.IsGenericType && IsAllowed(objectType.GetGenericTypeDefinition()))
        {
            return base.ValueFactory(objectType);
        }

        if (IsAllowed(objectType))
        {
            return base.ValueFactory(objectType);
        }

        return new Unsupported();
    }

    static bool IsAllowed(Type objectType) =>
        // Allow 'MassTransit.' interfaces because they are usually part of the infrastructure.
        objectType.Namespace?.StartsWith("MassTransit.", StringComparison.OrdinalIgnoreCase) == true;
}
