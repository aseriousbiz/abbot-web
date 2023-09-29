using System.Collections.Generic;
using Serious.Abbot.Models;

namespace Serious.Abbot.Configuration;

public class AbbotOptions
{
    /// <summary>
    /// Ignore user change events.
    /// </summary>
    public bool IgnoreUserChangeEvents { get; set; }

    /// <summary>
    /// If true, we'll publish select Slack events to the message bus instead of using the Slack Events table.
    /// </summary>
    public bool UseBusForSlackEvents { get; set; }

    /// <summary>
    /// If non-<c>null</c> Abbot will use this channel to post system notifications.
    /// Must be a Slack channel in the Team identified by <see cref="StaffOrganizationId"/>.
    /// </summary>
    public string? NotificationChannelId { get; set; }

    /// <summary>
    /// If non-<c>null</c>, Abbot will consider this team the "staff" organization.
    /// Must be a Slack team.
    /// </summary>
    public string? StaffOrganizationId { get; set; }

    /// <summary>
    /// If non-<c>null</c>, Abbot will use this value as the "public facing" hostname.
    /// Production: app.ab.bot (ABBOT_HOSTS_WEB)
    /// </summary>
    public string? PublicHostName { get; set; }

    /// <summary>
    /// If non-<c>null</c>, Abbot will use this value as the host for the CLI API.
    /// Production: api.ab.bot (ABBOT_HOSTS_API)
    /// </summary>
    public string? PublicApiHostName { get; set; }

    /// <summary>
    /// If non-<c>null</c>, Abbot will use this value as the host for ingesting Slack events.
    /// Production: in.ab.bot (ABBOT_HOSTS_INGESTION)
    /// </summary>
    public string? PublicIngestionHostName { get; set; }

    /// <summary>
    /// If non-<c>null</c>, Abbot will use this value as the host for the trigger controllers.
    /// Production: abbot.run (ABBOT_HOSTS_TRIGGER)
    /// </summary>
    public string? PublicTriggerHostName { get; set; }

    /// <summary>
    /// If non-<c>null</c>, Abbot will send feedback to this endpoint.
    /// </summary>
    public string? FeedbackEndpoint { get; set; }

    /// <summary>
    /// If non-<c>null</c>, Abbot will use it to get a default chat response when no skill matches a request.
    /// </summary>
    public string? DefaultResponderEndpoint { get; set; }

    /// <summary>
    /// The token that any monitoring tools must provide as a Bearer token in order to access protected status endpoints.
    /// </summary>
    public string? MonitoringToken { get; set; }

    /// <summary>
    /// The default plan to give new organizations.
    /// </summary>
    public PlanType DefaultPlan { get; set; } = PlanType.Free;

    /// <summary>
    /// The path to the directory where Abbot will staff-only static files.
    /// </summary>
    public string StaffAssetsPath { get; set; } = "wwwroot_staff";
}

public class SlackEventOptions
{
    /// <summary>
    /// The default processing options for Slack events.
    /// </summary>
    public SlackEventConfiguration Default { get; set; } = new();

    /// <summary>
    /// Gets or sets a list of Slack event types to ignore.
    /// </summary>
    public IDictionary<string, SlackEventConfiguration>? Overrides { get; set; }

    public SlackEventConfiguration GetEventConfiguration(string eventType)
    {
        return Overrides is not null && Overrides.TryGetValue(eventType, out var overrideValue) ? overrideValue : Default;
    }
}

public class SlackEventConfiguration
{
    /// <summary>
    /// Gets or sets a boolean indicating whether or not to ignore this event.
    /// </summary>
    public bool Ignored { get; set; }

    // TODO: Could move MassTransit delivery configuration here.
}

public class AbbotHealthCheckOptions
{
    public int MaximumHangfireJobsFailed { get; set; } = 1;
}
