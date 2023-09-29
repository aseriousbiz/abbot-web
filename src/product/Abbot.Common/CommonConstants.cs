using System;

[assembly: CLSCompliant(false)]
namespace Serious.Abbot;

public static class CommonConstants
{
    public const string SkillApiTokenHeaderName = "X-Abbot-SkillApiToken";
    public const string UserIdHeaderName = "X-Abbot-PlatformUserId";
    public const string SkillApiTimestampHeaderName = "X-Abbot-Timestamp";
}
