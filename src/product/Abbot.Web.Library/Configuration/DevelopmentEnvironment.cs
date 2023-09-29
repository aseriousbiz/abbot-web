using System;
using System.Diagnostics;
using System.IO;

namespace Serious.Abbot.Configuration;

public static class DevelopmentEnvironment
{
    public static readonly string LocalStoreRoot = IdentifyLocalStorageRoot();

    public static string GetLocalStorePath(string relativePath) =>
        Path.Combine(LocalStoreRoot, relativePath);

    static string IdentifyLocalStorageRoot()
    {
        if (Environment.GetEnvironmentVariable("ABBOT_HOME") is { Length: > 0 } abbotHome)
        {
            return abbotHome;
        }

        if (Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } home)
        {
            abbotHome = Path.Combine(home, ".abbot");
            return abbotHome;
        }

        if (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } userProfile)
        {
            abbotHome = Path.Combine(userProfile, ".abbot");
            return abbotHome;
        }

        throw new UnreachableException();
    }
}
