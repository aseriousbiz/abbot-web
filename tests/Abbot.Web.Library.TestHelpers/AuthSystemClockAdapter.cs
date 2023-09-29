using System;
using Serious;

namespace Abbot.Common.TestHelpers;

/// <summary>
/// A <see cref="Microsoft.AspNetCore.Authentication.ISystemClock"/> that pulls it's time from the active <see cref="Serious.IClock"/>.
/// </summary>
public class AuthSystemClockAdapter : Microsoft.AspNetCore.Authentication.ISystemClock
{
    readonly IClock _clock;

    public DateTimeOffset UtcNow => _clock.UtcNow;

    public AuthSystemClockAdapter(Serious.IClock clock)
    {
        _clock = clock;
    }
}
