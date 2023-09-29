using System.Collections.Generic;
using Microsoft.AspNetCore.Routing;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Attribute to restrict the ingestion domain.
/// Configured through the ABBOT_HOSTS_INGESTION environment variable.
/// When running without custom API domains (localhost or other environments), this is the same
/// as <see cref="AbbotWebHostAttribute"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class IngestionHostAttribute : Attribute, IHostMetadata
{
    public IReadOnlyList<string> Hosts => AllowedHosts.Ingestion;
}
