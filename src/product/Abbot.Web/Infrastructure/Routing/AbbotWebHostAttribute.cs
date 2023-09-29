using System.Collections.Generic;
using Microsoft.AspNetCore.Routing;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Attribute to restrict API to abbot-web domains.
/// Configured through the ABBOT_HOSTS_WEB environment variable.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class AbbotWebHostAttribute : Attribute, IHostMetadata
{
    public IReadOnlyList<string> Hosts => AllowedHosts.Web;
}
