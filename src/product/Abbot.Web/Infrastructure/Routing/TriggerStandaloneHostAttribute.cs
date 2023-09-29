using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Routing;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Attribute controlling the hostnames that map to routes for the TriggerController,
/// when running with custom domains (like abbot.run or stage.abbot.run)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class TriggerStandaloneHostAttribute : Attribute, IHostMetadata
{
    static readonly IReadOnlyList<string> StandaloneHosts =
        AllowedHosts.Trigger.Except(AllowedHosts.Web).ToArray() is [_, ..] nonWebHosts
        ? nonWebHosts
        : new[] { "ignore.this" };

    public IReadOnlyList<string> Hosts => StandaloneHosts;
}
