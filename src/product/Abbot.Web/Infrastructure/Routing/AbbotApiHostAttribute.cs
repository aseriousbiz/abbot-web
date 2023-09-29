using System.Collections.Generic;
using Microsoft.AspNetCore.Routing;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Attribute to restrict API to abbot-web domains (ab.bot, for instance)
/// Configured through the ABBOT_HOSTS_API environment variable.
/// When running without custom API domains (localhost or other environments), this is the same
/// as <see cref="AbbotWebHostAttribute"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AbbotApiHostAttribute : Attribute, IHostMetadata
{
    public IReadOnlyList<string> Hosts => AllowedHosts.Api;
}
