using System;

namespace Serious.Abbot.Execution;

public class VersionInfo : Scripting.IVersionInfo
{
    public VersionInfo()
    {
        var metadata = typeof(VersionInfo).Assembly.GetBuildMetadata();
        ProductVersion = metadata.InformationalVersion;
        ApiVersion = metadata.Version;
    }

    public string ProductVersion { get; }

    public Version ApiVersion { get; }
}
