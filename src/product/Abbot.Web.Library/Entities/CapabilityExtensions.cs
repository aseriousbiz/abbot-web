namespace Serious.Abbot.Entities;

public static class CapabilityExtensions
{
    public static string ToVerb(this Capability capability)
    {
        return capability.ToString().ToLowerInvariant();
    }
}
