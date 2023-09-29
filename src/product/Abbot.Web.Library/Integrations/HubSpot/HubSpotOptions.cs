using System.Collections.Generic;

namespace Serious.Abbot.Integrations.HubSpot;

public class HubSpotOptions
{
    public string? AppId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RequiredScopes { get; set; }

    public IDictionary<string, string> TimelineEvents { get; set; } = new Dictionary<string, string>();
}
