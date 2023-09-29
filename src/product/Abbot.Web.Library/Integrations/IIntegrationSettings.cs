using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations;

/// <summary>
/// Represents settings for the <see cref="IntegrationType"/>.
/// </summary>
public interface IIntegrationSettings
{
    static abstract IntegrationType IntegrationType { get; }
}
