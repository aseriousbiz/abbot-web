using System.Linq;
using Serious.Abbot.Configuration;
using Serious.Abbot.Helpers;
using Serious.Abbot.Live;

namespace Serious.Abbot;

public static class AllowedHosts
{
    public static void Init(AbbotOptions options, LiveOptions liveOptions)
    {
        Web = ParseAllowedHosts(options.PublicHostName, "ABBOT_HOSTS_WEB");
        Api = ParseAllowedHosts(options.PublicApiHostName, "ABBOT_HOSTS_API", Web);
        Ingestion = ParseAllowedHosts(options.PublicIngestionHostName, "ABBOT_HOSTS_INGESTION", Web);
        Live = ParseAllowedHosts(liveOptions.Host, "ABBOT_HOSTS_LIVE", Web);
        Trigger = ParseAllowedHosts(options.PublicTriggerHostName, "ABBOT_HOSTS_TRIGGER", Web);

        All = Web.Concat(Api).Concat(Ingestion).Concat(Live).Concat(Trigger)
            .Distinct().ToArray();
    }

    static string[] ParseAllowedHosts(string? publicHostName, string fallbackVariable, string[]? defaultValues = null) =>
        Environment.GetEnvironmentVariable(fallbackVariable).ParseAllowedHosts(defaultValues)
            .Concat(publicHostName is null ? Array.Empty<string>() : new[] { publicHostName })
            .Distinct().ToArray();

    /// <summary>
    /// The allowed set of hosts for Abbot pages and most controllers.
    /// Production: app.ab.bot (ABBOT_HOSTS_WEB)
    /// </summary>
#pragma warning disable CA1819
    public static string[] Web { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// The allowed set of hosts for the CLI API controllers.
    /// Production: api.ab.bot (ABBOT_HOSTS_API)
    /// </summary>
    public static string[] Api { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// The allowed set of hosts for Slack and any future ingestion.
    /// Production: in.ab.bot (ABBOT_HOSTS_INGESTION)
    /// </summary>
    public static string[] Ingestion { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// The allowed set of hosts for the SignalR Hubs.
    /// Production: live.ab.bot (ABBOT_HOSTS_LIVE)
    /// </summary>
    public static string[] Live { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// The allowed set of hosts for the trigger controllers.
    /// Production: abbot.run (ABBOT_HOSTS_TRIGGER)
    /// </summary>
    public static string[] Trigger { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// All allowed hosts.
    /// </summary>
    public static string[] All { get; private set; } = Array.Empty<string>();
#pragma warning restore CA1819
}
