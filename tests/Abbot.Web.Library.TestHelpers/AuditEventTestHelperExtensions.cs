namespace Serious.Abbot.Entities;

public static class AuditEventTestHelperExtensions
{
    /// <summary>
    /// Reads the properties of an <see cref="AuditEventBase"/> as a list of (key,value) tuples sorted in ascending order by key.
    /// </summary>
    public static IReadOnlyList<(string, object)> ReadPropertiesAsTuples(this AuditEventBase auditEvent)
    {
        var props = auditEvent.ReadProperties<IDictionary<string, object>>();
        return props is null
            ? Array.Empty<(string, object)>()
            : props.Select(x => (x.Key, x.Value)).OrderBy(p => p.Key).ToArray();
    }
}
