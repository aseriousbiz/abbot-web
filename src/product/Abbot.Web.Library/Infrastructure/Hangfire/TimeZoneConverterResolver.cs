using System;
using Hangfire;
using TimeZoneConverter;

namespace Serious.Abbot.Infrastructure.Hangfire;

// CREDIT: https://github.com/HangfireIO/Hangfire/issues/1268#issuecomment-481699210
/// <summary>
/// Creates a <see cref="TimeZoneInfo" /> from a timeZoneId whether it's Windows or IANA.
/// </summary>
public class TimeZoneConverterResolver : ITimeZoneResolver
{
    public TimeZoneInfo GetTimeZoneById(string timeZoneId)
    {
        return TZConvert.GetTimeZoneInfo(timeZoneId);
    }
}
