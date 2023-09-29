using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Routing;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Attribute controlling the hostnames that map to routes for the TriggerController,
/// when running in a shared host (localhost or lab environments without custom domains)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class TriggerSharedHostAttribute : Attribute, IHostMetadata
{
    static readonly IReadOnlyList<string> SharedHosts =
        AllowedHosts.Trigger.Except(AllowedHosts.Web).Any()
        ? new[] { "ignore.this" }
        : AllowedHosts.Web;

    public IReadOnlyList<string> Hosts => SharedHosts;
}
